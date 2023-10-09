namespace DualSourceAdapter

open System
open System.Threading.Tasks
open System.Collections.Generic
open System.Threading

module Adapter =

    type MigrationSource = 
        | Legacy = 0 
        | New = 1

    [<CLIMutable>]
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

    type PrimaryResponse<'res> = |Primary of 'res

    type SecondaryResponse<'res> = |Secondary of 'res
        
    let private migration 
        (lazySecondaryTask : 'resp PrimaryResponse -> 'resp SecondaryResponse Task) 
        (runningPrimaryTask: 'resp PrimaryResponse Task) 
        (migrateFn: 'resp PrimaryResponse -> 'resp SecondaryResponse -> unit Task) 
        (resAdaptFn: 'resp PrimaryResponse -> 'resp SecondaryResponse -> 'resp PrimaryResponse) = 
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

    /// migration function for F# code, curried version
    let migrationTaskBuilderFn 
        (opt : MigrationOption) 
        (input: 'req) 
        (legacyFunc: 'req -> 'resp Task) 
        (newFunc: 'req -> 'resp Task) 
        (migrateFn: 'resp PrimaryResponse -> 'resp SecondaryResponse -> unit Task) 
        (resAdaptFn: 'resp PrimaryResponse -> 'resp SecondaryResponse -> 'resp PrimaryResponse)
        (reqAdaptFn: 'req -> 'resp PrimaryResponse -> 'req)
            : 'resp Task = 
        task {

            if opt.Sources |> Seq.isEmpty then
                failwith "no sources specified"

            if opt.IsSingleSource MigrationSource.Legacy then
                let! r = legacyFunc(input) 
                return r
            else if opt.IsSingleSource MigrationSource.New then
                let! r = newFunc(input)
                return r
            else
                let (active,secondary) = 
                    if opt.Active = MigrationSource.Legacy then
                        legacyFunc, newFunc
                    else
                        newFunc, legacyFunc

                let runningPrimaryTask = 
                    task {
                        let! a = active(input)
                        return a |> PrimaryResponse.Primary
                    }

                let lazySecondary activeOutput = 
                    task {
                        let i = reqAdaptFn input activeOutput
                        let! s = secondary(i)
                        return s |> SecondaryResponse.Secondary
                    }

                let! primaryResp = migration lazySecondary runningPrimaryTask migrateFn resAdaptFn

                let (Primary unwrapped) = primaryResp

                return unwrapped
        }

    /// migration builder for C# code
    type MigrationTaskBuilder<'req,'resp>(
        legacyFunc, 
        newFunc, 
        migrateFn: Func<'resp PrimaryResponse, 'resp SecondaryResponse, Task>, 
        resAdaptFn: Func<'resp PrimaryResponse, 'resp SecondaryResponse, 'resp PrimaryResponse>, 
        reqAdaptFn: Func<'req, 'resp PrimaryResponse, 'req>) =
        member this.MigrationTaskAsync opt (input : 'req) : 'resp Task =
            let l = legacyFunc |> FuncConvert.FromFunc<_,_>
            let n = newFunc |> FuncConvert.FromFunc<_,_>

            // TODO: smarter way to turn this to unit Task?
            let m req res = task { 
                let mm = migrateFn |> FuncConvert.FromFunc
                let! r = mm req res
                return r
            }
       
            let resAdapt = resAdaptFn |> FuncConvert.FromFunc
            let reqAdapt = reqAdaptFn |> FuncConvert.FromFunc
            migrationTaskBuilderFn opt input l n m resAdapt reqAdapt