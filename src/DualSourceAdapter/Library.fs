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
        

    let secondaryTask lazySecondaryTask runningPrimaryTask compareFunction  = 
        backgroundTask {
            printfn "BACKGROUND TASK START"
            try
                let! primaryResult = runningPrimaryTask
                let! secondaryResult = lazySecondaryTask()
                compareFunction primaryResult secondaryResult
                ()
                printfn "BACKGROUND TASK STOP"
            with e ->
                printfn "BACKGROUND SECONDARY EX: %A" e.Message
                ()
        }

    let migrate 
        (opt : MigrationOption) 
        (input: 'a) 
        (legacyFunc: 'a -> 'b Task) 
        (newFunc: 'a -> 'b Task) 
        (compareFunc: 'b -> 'b -> unit) 
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
                
                if opt.IsCompare then
                    let activeResultTask = active(input)
                    let lazySecondary = fun () -> secondary(input)
                    let! _ = secondaryTask lazySecondary activeResultTask compareFunc
                    return! activeResultTask
                else
                    return! active(input)
        }
