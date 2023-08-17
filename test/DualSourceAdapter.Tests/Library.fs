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
    type GetOrderResponse = { Content: string }

module Legacy =

    let legacyDB = new Dictionary<int, string>()

    open FakeContracts
    let writeOrder createOrderRequest = task {
        printfn $"OLD WRITE: {createOrderRequest.Test}"
        let id = Utils.rnd.Next()
        legacyDB.Add(id, createOrderRequest.Test)
        return { IdResult = id }
    }

    let getOrder getOrderRequest = task {
        printfn $"OLD READ: {getOrderRequest.Id}"
        return { Content = legacyDB[getOrderRequest.Id] }
    }

module New =
    open FakeContracts

    let newDB = new Dictionary<int, string>()

    let writeOrder createOrderRequest = task {
        printfn $"NEW WRITE: {createOrderRequest.Test}"
        let id = Utils.rnd.Next()
        newDB.Add(id, createOrderRequest.Test)
        return { IdResult = id }
    }

    let getOrder getOrderRequest = task {
        printfn $"NEW READ: {getOrderRequest.Id}"
        return { Content = newDB[getOrderRequest.Id] }
    }

module TestMigration =

    open FakeContracts

    let compareFn a b =
            printfn "COMPARE!!!!"
    let writeMigrate (option: MigrationOption) input = 
        migrate option input Legacy.writeOrder New.writeOrder compareFn

    let readMigrate (option: MigrationOption) input = 
        migrate option input Legacy.getOrder New.getOrder compareFn


    let migration option =
        task {

            let createOrder = { Test = "test" }

            let! writeResult = writeMigrate option createOrder

            let! readResult = readMigrate option { Id = writeResult.IdResult }

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

    [<Fact>]
    let ``test sources picker``() =
            
        Options.Legacy.IsSingleSource MigrationSource.Legacy |> Assert.True
        Options.New.IsSingleSource MigrationSource.New |> Assert.True
        Options.LegacyActiveDual.IsSingleSource MigrationSource.New |> Assert.False
        Options.NewActiveDual.IsSingleSource MigrationSource.New |> Assert.False

    [<Fact>]
    let ``test migration adapter when legacy active and dual, ok``() =
        task {

            let! res = TestMigration.migration Options.LegacyActiveDual

            Assert.NotEmpty(res.Content)

        }
