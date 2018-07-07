namespace KeyQuery

open System
open System.Collections.Generic
open System.Collections.Concurrent

type FieldName = string
type FieldValue = string

type IndexStore<'tid when 'tid : comparison> = ConcurrentDictionary<FieldName, ConcurrentDictionary<FieldValue, (Set<'tid>)>>

type DataStore<'tid,'t when 'tid : comparison> =
    { Records : IDictionary<'tid,'t>
      Indexes : IndexStore<'tid> }
      static member Build (indexedFields:FieldName list) = 
        let indexes = ConcurrentDictionary<FieldName, ConcurrentDictionary<FieldValue, (Set<'tid>)>>()
        for name in indexedFields do
            let v = ConcurrentDictionary<FieldValue, (Set<'tid>)>()
            indexes.TryAdd(name, v) |> ignore
        { Records = Dictionary<'tid,'t>()
          Indexes = indexes }
        member __.Insert id dto =
            __.Records.Add(id, dto)
        member __.Index id (fieldName:FieldName) (value:obj) =
            let index = __.Indexes.Item fieldName
            let fieldValue = value.ToString()
            let offsets =
                index.GetOrAdd(fieldValue, fun _ -> Set.empty)
                |> Set.add id
            index.AddOrUpdate(fieldValue, offsets, fun _ _ -> offsets) |> ignore
        member __.SearchByIndex (fieldName:FieldName) (value:obj) =
            let index = __.Indexes.Item fieldName
            let fieldValue = value.ToString()
            index.GetOrAdd(fieldValue, fun _ -> Set.empty)
            |> Set.toSeq
            |> Seq.map (fun id -> __.Records.Item id)


type Operation =
    | QueryById    of obj
    | QueryByField of FieldName * value:obj
    
type BinaryOperation =    
    | And of Operation * Operation
    | Or of Operation * Operation

type IDto<'tid when 'tid : comparison> =
    abstract Id : 'tid
    
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

    let execute<'tid,'t when 'tid : comparison> (store : DataStore<'tid,'t>) =
        function
        | QueryById idValue ->
            let id = unbox<'tid> idValue
            [store.Records.Item id]
        | QueryByField (fileName, fieldValue) ->
            store.SearchByIndex fileName fieldValue |> Seq.toList

    let binaryExecute<'tid,'t when 'tid : comparison and 't :> IDto<'tid> and 't : equality> (store : DataStore<'tid,'t>) operation =
        let exec = execute store
        match operation with
        | And (op1,op2) ->
            let a = exec op1
            let b = exec op2
            intersection a b
        | Or (op1,op2) ->
            let a = exec op1
            let b = exec op2
            (a @ b) |> List.distinct

module Test = 

  type CustomerDto =
      { Id:Guid
        Lastname:string
        Firstname:string
        Birth:DateTime
        Score:int
        Activated:bool }
      interface IDto<Guid> with
          member __.Id:Guid = __.Id

  let store = DataStore<Guid, CustomerDto>.Build ["Lastname"; "Firstname"; "Score"]
  for i in 0 .. 10 do
      let d = 1000+i*200 |> float
      let dto =
          { Id=Guid.NewGuid()
            Lastname=sprintf "lastname %d" i
            Firstname=sprintf "Firstname %d" i
            Birth=DateTime.UtcNow - (TimeSpan.FromDays d)
            Score=i
            Activated=(i % 2 = 0) }
      store.Insert dto.Id dto
      store.Index dto.Id "Lastname" dto.Lastname
      store.Index dto.Id "Firstname" dto.Firstname
      store.Index dto.Id "Score" dto.Score

  // store.SearchByIndex "Lastname" "lastname 4"

  let someId = store.Records.Keys |> Seq.head
  someId
  |> box
  |> QueryById
  |> Operation.execute store

  QueryByField ("Lastname", (box "lastname 4")) |> Operation.execute store

  Or (QueryByField ("Lastname", (box "lastname 4")), QueryByField ("Lastname", (box "lastname 5"))) |> Operation.binaryExecute store
  And (QueryByField ("Lastname", (box "lastname 4")), QueryByField ("Lastname", (box "lastname 5"))) |> Operation.binaryExecute store

  And (QueryByField ("Lastname", (box "lastname 4")), QueryByField ("Firstname", (box "Firstname 4"))) |> Operation.binaryExecute store



