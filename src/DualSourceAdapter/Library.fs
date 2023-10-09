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
        

    let secondaryTask lazySecondaryTask runningPrimaryTask migrateFn resAdaptFn = 
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
        (input: 'a) 
        (legacyFunc: 'a -> 'b Task) 
        (newFunc: 'a -> 'b Task) 
        (migrateFn: 'b -> 'b -> unit Task) 
        (resAdaptFn: 'b -> 'b -> 'b)
        (reqAdaptFn: 'a -> 'b -> 'a)
            : 'b Task = 
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
