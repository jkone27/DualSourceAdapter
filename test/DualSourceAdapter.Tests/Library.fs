namespace DualSourceAdapter.Tests

open Xunit
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
            
            let (Secondary b) = b
            let (Primary a) = a

            if a.Id <> b.Id || a.Content <> b.Content then 
                printfn $"%A{a} \r\nvs\r\n %A{b}"
        }

    let CreateOrderIdAdapter (a: CreateOrderResponse PrimaryResponse) (b: CreateOrderResponse SecondaryResponse) : CreateOrderResponse PrimaryResponse =
        let (Secondary b) = b
        let (Primary a) = a
        { b with IdResult = a.IdResult } |> Primary

    let ReadOrderIdAdapter (a: GetOrderResponse PrimaryResponse) (b: GetOrderResponse SecondaryResponse) : GetOrderResponse PrimaryResponse =
        let (Secondary b) = b
        let (Primary a) = a
        { a with Id = b.Id } |> Primary

    let migrationDictionary = new Dictionary<int,int>()

    let writeMigrate (option: MigrationOption) (input: CreateOrderRequest) = 
        migrationTaskBuilderFn option 
            input 
            Legacy.writeOrder 
            New.writeOrder
            (fun a b -> 
                task { 
                    let (Primary a) = a
                    let (Secondary b) = b
                    migrationDictionary.Add(a.IdResult, b.IdResult)
                } )
            CreateOrderIdAdapter
            (fun a b -> a)

    let readMigrate (option: MigrationOption) (input: GetOrderRequest) = 
        migrationTaskBuilderFn option 
            input 
            Legacy.getOrder 
            New.getOrder 
            readCompareFn 
            ReadOrderIdAdapter
            (fun a primary -> 
                    let (Primary p) = primary
                    {
                        a with Id = migrationDictionary[p.Id]
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
        
                    Assert.True isNotWhiteSpace
            }


    [<Fact>]
    let ``test sources picker``() =
            
        Options.Legacy.IsSingleSource MigrationSource.Legacy |> Assert.True
        Options.New.IsSingleSource MigrationSource.New |> Assert.True
        Options.LegacyActiveDual.IsSingleSource MigrationSource.New |> Assert.False
        Options.NewActiveDual.IsSingleSource MigrationSource.New |> Assert.False

    [<Fact>]
    let ``test migration adapter with different possible configs, ok``() =
        [
            Options.Legacy
            Options.New
            Options.LegacyActiveDual
            Options.NewActiveDual 
        ]
        |> ``run migration with ``

        
