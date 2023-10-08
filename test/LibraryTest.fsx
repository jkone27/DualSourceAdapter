#load "../src/DualSourceAdapter/Library.fs"

open System
open System.Threading.Tasks
open System.Collections.Generic
open System.Threading
open DualSourceAdapter.Adapter

module Utils =
    let rnd = new Random()

module FakeContracts =

    type CreateOrderRequest = { Test: string }
    type CreateOrderResponse = { IdResult: int }

    type GetOrderRequest = { Id: int }
    type GetOrderResponse = { Id: int; Content: string }

module Legacy =

    let legacyDB = new Dictionary<int, string>()

    open FakeContracts
    let writeOrder createOrderRequest = task {
        printfn $"OLD WRITE: {createOrderRequest.Test}"
        let id = Utils.rnd.Next()
        legacyDB.Add(id, createOrderRequest.Test)
        printfn $"DB created: {id}"
        return { IdResult = id }
    }

    let getOrder (getOrderRequest: GetOrderRequest) = task {
        printfn $"OLD READ: {getOrderRequest.Id}"
        return { Id = getOrderRequest.Id; Content = legacyDB[getOrderRequest.Id] }
    }

module New =
    open FakeContracts

    let newDB = new Dictionary<int, string>()

    let writeOrder createOrderRequest = task {
        printfn $"NEW WRITE: {createOrderRequest.Test}"
        let id = Utils.rnd.Next()
        newDB.Add(id, createOrderRequest.Test)
        printfn $"API created: {id}"
        return { IdResult = id }
    }

    let getOrder (getOrderRequest : GetOrderRequest) = task {
        printfn $"NEW READ: {getOrderRequest.Id}"
        return { Id = getOrderRequest.Id ; Content = newDB[getOrderRequest.Id] }
    }

module TestMigration =

    open FakeContracts

    let readCompareFn a b =
        task {
            if a.Id <> b.Id || a.Content <> b.Content then 
                printfn $"%A{a} \r\nvs\r\n %A{b}"
        }

    let CreateOrderIdAdapter (a: CreateOrderResponse) (b: CreateOrderResponse) : CreateOrderResponse =
        { b with IdResult = a.IdResult }

    let ReadOrderIdAdapter (a: GetOrderResponse) (b: GetOrderResponse) : GetOrderResponse =
        { a with Id = b.Id }

    let migrationDictionary = new Dictionary<int,int>()

    let writeMigrate (option: MigrationOption) (input: CreateOrderRequest) = 
        migrate option 
            input 
            Legacy.writeOrder 
            New.writeOrder
            (fun a b -> 
                task { 
                    migrationDictionary.Add(a.IdResult, b.IdResult)
                } )
            CreateOrderIdAdapter
            (fun a b -> a)

    let readMigrate (option: MigrationOption) (input: GetOrderRequest) = 
        migrate option 
            input 
            Legacy.getOrder 
            New.getOrder 
            readCompareFn 
            ReadOrderIdAdapter
            (fun a b -> 
                {
                    a with Id = migrationDictionary[b.Id]
                }
                )


    let migration option =
        task {

            let createOrder = { Test = "test" }

            let! writeResult = 
                writeMigrate option createOrder

            let! readResult = 
                readMigrate option { Id = writeResult.IdResult }

            return readResult

        }


module Options =

    let LegacyActiveDual = { 
        Sources = [| MigrationSource.Legacy ; MigrationSource.New |]
        Active = MigrationSource.Legacy
        IsCompare = true
    }

    let NewActiveDual = { 
        Sources = [| MigrationSource.Legacy ; MigrationSource.New |]
        Active = MigrationSource.New
        IsCompare = true
    }

    let Legacy = { 
        Sources = [| MigrationSource.Legacy |]
        Active = MigrationSource.New
        IsCompare = false
    }

    let New = { 
        Sources = [| MigrationSource.New |]
        Active = MigrationSource.New
        IsCompare = false
    }


module Tests = 

    let a p  = 
        if p then 
            () 
        else 
            failwith $"assert failed"

    Options.Legacy.IsSingleSource MigrationSource.Legacy |> a 
    Options.New.IsSingleSource MigrationSource.New |> a 
    Options.LegacyActiveDual.IsSingleSource MigrationSource.New |> (a << not)
    Options.NewActiveDual.IsSingleSource MigrationSource.New |> (a << not)

    let ``run migration with `` (options : MigrationOption list)  =
            task {

                let! res = 
                    options 
                    |> List.map (fun o -> 
                        printfn $"{o}"
                        TestMigration.migration o 
                    )
                    |> Task.WhenAll
                    

                for r in res do
                    let isNotWhiteSpace = 
                        r.Content  
                        |> (String.IsNullOrWhiteSpace >> not)
        
                    isNotWhiteSpace |> a
            }

// tests
[
    //Options.Legacy
    //Options.New
    Options.LegacyActiveDual
    Options.NewActiveDual 
]
|> Tests.``run migration with ``
|> fun f -> f.Result