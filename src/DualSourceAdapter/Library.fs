namespace DualSourceAdapter

open System
open System.Threading.Tasks
open System.Collections.Generic
open System.Threading

module Adapter =

    type MigrationSource = 
        | Legacy = 0 
        | New = 1

    type MigrationOption = {
            Sources: MigrationSource []
            Active: MigrationSource
            IsCompare: bool
        }
        with member this.IsSingleSource migrationSource = 
                this.Sources 
                |> Seq.tryExactlyOne
                |> Option.map (fun o -> o = migrationSource)
                |> Option.defaultValue false
        

    let secondaryTask 
        (lazySecondaryTask : 'resp -> 'resp Task) 
        (runningPrimaryTask: 'resp Task) 
        (migrateFn: 'resp -> 'resp -> unit Task) 
        (resAdaptFn: 'resp -> 'resp -> 'resp) = 
        backgroundTask {
            let! primaryResult = runningPrimaryTask
            try
                let! secondaryResult = lazySecondaryTask primaryResult
                do! migrateFn primaryResult secondaryResult
                return resAdaptFn primaryResult secondaryResult
            with e ->
                printfn "SECONDARY SECONDARY EX: %A" e.Message
                return primaryResult
        }

    let migrate 
        (opt : MigrationOption) 
        (input: 'req) 
        (legacyFunc: 'req -> 'resp Task) 
        (newFunc: 'req -> 'resp Task) 
        (migrateFn: 'resp -> 'resp -> unit Task) 
        (resAdaptFn: 'resp -> 'resp -> 'resp)
        (reqAdaptFn: 'req -> 'resp -> 'req)
            : 'resp Task = 
        task {

            if opt.Sources |> Seq.isEmpty then
                failwith "no sources specified"

            if opt.IsSingleSource MigrationSource.Legacy then
                return! legacyFunc(input) 
            else if opt.IsSingleSource MigrationSource.New then
                return! newFunc(input)
            else
                let (active,secondary) = 
                    if opt.Active = MigrationSource.Legacy then
                        legacyFunc, newFunc
                    else
                        newFunc, legacyFunc

                let activeResultTask = 
                    active(input)

                let lazySecondary = 
                    fun activeOutput -> 
                        let i = reqAdaptFn input activeOutput
                        secondary(i)

                return! secondaryTask lazySecondary 
                    activeResultTask 
                    migrateFn
                    resAdaptFn
        }
