namespace KeyQuery

open System
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Linq.Expressions

type FieldName = string
type FieldValue = string

module Visitors =

  type PropertyVisitor() = 
    inherit ExpressionVisitor()
    
    let names : string list ref = ref []
    
    override __.VisitMember(node:MemberExpression) : Expression =
      match node.Member with
      | :? PropertyInfo as prop ->
          names := prop.Name :: !names
      | _ -> ()
      base.VisitMember(node)
      
    member __.Path
      with get () =
        let parts = !names |> Seq.toArray
        System.String.Join(".", parts)
    
  let getIndexName<'t,'r> (expression:Expression<Func<'t, 'r>>) =
      let visitor = PropertyVisitor()
      expression |> visitor.Visit |> ignore 
      visitor.Path


type IDto<'tid when 'tid : comparison> =
  abstract Id : 'tid

type IndexStore<'tid when 'tid : comparison> = ConcurrentDictionary<FieldName, ConcurrentDictionary<FieldValue, (Set<'tid>)>>

type DataStore<'tid,'t when 'tid : comparison and 't :> IDto<'tid>> =
  { Records : ConcurrentDictionary<'tid,'t>
    Indexes : IndexStore<'tid>
    IndexValueProviders : IDictionary<string, ('t -> string)> 
  }
    static member Build (indexedMembers:Expression<Func<'t, string>> array) = 
      let indexes = ConcurrentDictionary<FieldName, ConcurrentDictionary<FieldValue, (Set<'tid>)>>()
      let indexedFields = 
         indexedMembers
         |> Array.map (
              fun exp ->
                let path = Visitors.getIndexName<'t, string> exp
                path, exp.Compile().Invoke
            )
         
      for (name, _) in indexedFields do
        let v = ConcurrentDictionary<FieldValue, (Set<'tid>)>()
        indexes.TryAdd(name, v) |> ignore
      { Records = ConcurrentDictionary<'tid,'t>()
        Indexes = indexes
        IndexValueProviders = indexedFields |> dict }
      
      member __.Insert (record:'t) =
        let id = record.Id
        if __.Records.TryAdd (id, record)
        then 
          for kv in __.IndexValueProviders do
            let value = kv.Value record
            __.Index record.Id kv.Key value
      
      member __.Index id (fieldName:FieldName) (value:obj) =
        let index = __.Indexes.Item fieldName
        let fieldValue = value.ToString()
        let ids = index.GetOrAdd(fieldValue, fun _ -> Set.empty) |> Set.add id
        index.AddOrUpdate(fieldValue, ids, fun _ _ -> ids) |> ignore
        
      member __.SearchByIndex (fieldName:FieldName) (value:obj) =
        let index = __.Indexes.Item fieldName
        let fieldValue = value.ToString()
        index.GetOrAdd(fieldValue, fun _ -> Set.empty)
        |> Set.toSeq
        |> Seq.map (fun id -> __.Records.Item id)
        
      member __.RemoveIndexedValue (fieldName:FieldName) (value:obj) recordId =
        let index = __.Indexes.Item fieldName
        let fieldValue = value.ToString()
        let ids = index.GetOrAdd(fieldValue, fun _ -> Set.empty) |> Set.remove recordId
        index.AddOrUpdate(fieldValue, ids, fun _ _ -> ids) |> ignore
        
      member __.Remove id =
        match __.Records.TryRemove id with
        | false, _ -> false
        | true, record ->
            for kv in __.IndexValueProviders do
              let value = kv.Value record
              __.RemoveIndexedValue kv.Key value record.Id
            true


type Operation =
  | QueryById    of obj
  | QueryByField of FieldName * value:obj    
  | And of Operation * Operation
  | Or of Operation * Operation

[<RequireQualifiedAccess>]
module Operation =

  //https://stackoverflow.com/questions/20248006/f-intersection-of-lists
  
  let rec mem<'tid,'t when 'tid : comparison and 't :> IDto<'tid>> (list:'t list) (x:'t) = 
    match list with
    | [] -> false
    | head :: tail -> 
      if x.Id = head.Id then true else mem tail x

  let rec intersection<'tid,'t when 'tid : comparison and 't :> IDto<'tid>> (list1:'t list) (list2:'t list) = 
    match list1 with
    | head :: tail -> 
        let rest = intersection tail list2
        if mem list2 head then head::rest
        else rest
    | [] -> []

  let rec execute<'tid,'t when 'tid : comparison and 't :> IDto<'tid> and 't : equality> (store : DataStore<'tid,'t>) operation =
    let exec = execute store
    match operation with
    | QueryById idValue ->
        let id = unbox<'tid> idValue
        [store.Records.Item id]
    | QueryByField (fileName, fieldValue) ->
        store.SearchByIndex fileName fieldValue |> Seq.toList
    | And (op1,op2) ->
        let a = exec op1
        let b = exec op2
        intersection a b
    | Or (op1,op2) ->
        let a = exec op1
        let b = exec op2
        (a @ b) |> List.distinct

//
//module Test = 
//
//  type CustomerDto =
//    { Id:Guid
//      Lastname:string
//      Firstname:string
//      Birth:DateTime
//      Score:int
//      Activated:bool }
//    interface IDto<Guid> with
//      member __.Id:Guid = __.Id
//
//  let store = DataStore<Guid, CustomerDto>.Build ["Lastname"; "Firstname"; "Score"]
//  for i in 0 .. 10 do
//    let d = 1000+i*200 |> float
//    let dto =
//      { Id=Guid.NewGuid()
//        Lastname=sprintf "lastname %d" i
//        Firstname=sprintf "Firstname %d" i
//        Birth=DateTime.UtcNow - (TimeSpan.FromDays d)
//        Score=i
//        Activated=(i % 2 = 0) }
//    store.Insert dto.Id dto
//    store.Index dto.Id "Lastname" dto.Lastname
//    store.Index dto.Id "Firstname" dto.Firstname
//    store.Index dto.Id "Score" dto.Score
//
//  // store.SearchByIndex "Lastname" "lastname 4"
//
//  let record = store.Records |> Seq.head |> fun kv -> kv.Value
//
//  //getIndexName (fun r -> r.Lastname)
//
//  let someId = store.Records.Keys |> Seq.head
//  someId
//  |> box
//  |> QueryById
//  |> Operation.execute store
//
//  QueryByField ("Lastname", (box "lastname 4")) |> Operation.execute store
//
//  Or (QueryByField ("Lastname", (box "lastname 4")), QueryByField ("Lastname", (box "lastname 5"))) |> Operation.binaryExecute store
//  And (QueryByField ("Lastname", (box "lastname 4")), QueryByField ("Lastname", (box "lastname 5"))) |> Operation.binaryExecute store
//
//  And (QueryByField ("Lastname", (box "lastname 4")), QueryByField ("Firstname", (box "Firstname 4"))) |> Operation.binaryExecute store
//
//

