namespace KeyQuery

open System
open System.Reflection
open System.Collections.Generic
open System.Collections.Concurrent
open System.Linq.Expressions
open System.Threading
open System.Threading.Tasks
open FSharp.Control.Tasks

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

type IAsyncKeyValueStore<'k,'v> =
  abstract AddOrUpdate : 'k -> 'v -> ('k -> 'v -> 'v) -> 'v Task
  abstract GetOrAdd : 'k -> ('k -> 'v) -> 'v Task
  abstract TryAdd : 'k -> 'v -> bool Task
  abstract TryRemove : 'k -> (bool * 'v) Task
  abstract Get : 'k -> 'v Task
  abstract AllKeys : unit -> 'k ICollection Task
  abstract AllValues : unit -> 'v ICollection Task

type InMemoryKeyValueStore<'k,'v> () =
  let memory = ConcurrentDictionary<'k,'v>()
  
  interface IAsyncKeyValueStore<'k,'v> with
    member __.AddOrUpdate key addValue updateValueFactory = 
      memory.AddOrUpdate(key, addValue, updateValueFactory) |> Task.FromResult
    member __.GetOrAdd key valueFactory = 
      memory.GetOrAdd(key, valueFactory) |> Task.FromResult
    member __.TryAdd key value = 
      memory.TryAdd(key, value) |> Task.FromResult
    member __.TryRemove key =
      key |> memory.TryRemove |> Task.FromResult
    member __.Get key =
      memory.Item key |> Task.FromResult
    member __.AllKeys () =
      memory.Keys |> Task.FromResult
    member __.AllValues () =
      memory.Values |> Task.FromResult

type IndexStore<'tid when 'tid : comparison> = ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, (Set<'tid>)>>

type DataStore<'tid,'t when 'tid : comparison and 't :> IDto<'tid>> =
  { Records : IAsyncKeyValueStore<'tid,'t>
    Indexes : IndexStore<'tid>
    IndexValueProviders : IDictionary<string, ('t -> string)> }
  
  static member Build 
    (buildPersistence:Func<IAsyncKeyValueStore<'tid,'t>>) 
    (buildFieldIndexPersistence:Func<string, Task<IAsyncKeyValueStore<FieldValue, (Set<'tid>)>>>)
    (indexedMembers:Expression<Func<'t, string>> array) =
  
    let indexes = ConcurrentDictionary<FieldName, IAsyncKeyValueStore<FieldValue, (Set<'tid>)>>()
    let indexedFields = 
       indexedMembers
       |> Array.map (
            fun exp ->
              let path = Visitors.getIndexName<'t, string> exp
              path, exp.Compile().Invoke
          )
       
    for (name, _) in indexedFields do
      let v = (buildFieldIndexPersistence.Invoke name).Result
      indexes.TryAdd(name, v) |> ignore
      
    { Records = buildPersistence.Invoke()
      Indexes = indexes
      IndexValueProviders = indexedFields |> dict }
    
    member __.Insert (record:'t) =
      let id = record.Id
      task {
        let! rs = __.Records.TryAdd id record
        if rs
        then 
          for kv in __.IndexValueProviders do
            let value = kv.Value record
            do! __.Index record.Id kv.Key value
          return true
        else
          return true
      }
      
    member __.Index id (fieldName:FieldName) (value:obj) =
      let index = __.Indexes.Item fieldName
      let fieldValue = value.ToString()
      task {
        let! currentIds = index.GetOrAdd fieldValue (fun _ -> Set.empty)
        let ids = currentIds |> Set.add id
        index.AddOrUpdate fieldValue ids (fun _ _ -> ids) |> ignore
      }
      
    member __.SearchByIndex (fieldName:FieldName) (value:obj) : ('t array) Task =
      let index = __.Indexes.Item fieldName
      let fieldValue = value.ToString()
      task {
        let! items = index.GetOrAdd fieldValue (fun _ -> Set.empty)
        return! 
          items
          |> Set.toSeq
          |> Seq.map (fun id -> __.Records.Get id)
          |> Seq.toArray
          |> Task.WhenAll
      }
      
    member __.RemoveIndexedValue (fieldName:FieldName) (value:obj) recordId =
      let index = __.Indexes.Item fieldName
      let fieldValue = value.ToString()
      task {
        let! current = index.GetOrAdd fieldValue (fun _ -> Set.empty)
        let ids = current |> Set.remove recordId
        index.AddOrUpdate fieldValue ids (fun _ _ -> ids) |> ignore
      }
      
    member __.Remove id =
      task {
        let! rs = __.Records.TryRemove id
        match rs with
        | false, _ -> return false
        | true, record ->
            for kv in __.IndexValueProviders do
              let value = kv.Value record
              do! __.RemoveIndexedValue kv.Key value record.Id
            return true
      }

type Operation =
  | QueryById    of obj
  | QueryByField of FieldName * value:obj    
  | And of Operation * Operation
  | Or of Operation * Operation

[<RequireQualifiedAccess>]
module Operation =

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
    task {
      match operation with
      | QueryById idValue ->
          let id = unbox<'tid> idValue
          let! rs = store.Records.Get id
          return [rs]
      | QueryByField (fileName, fieldValue) ->
          let! rs = store.SearchByIndex fileName fieldValue
          return rs |> Seq.toList
      | And (op1,op2) ->
          let! a = exec op1
          let! b = exec op2
          return intersection a b
      | Or (op1,op2) ->
          let! a = exec op1
          let! b = exec op2
          return (a @ b) |> List.distinct
    }

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

