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

    // TODO: use types
    type PrimaryReq<'req> = |Req of 'req
    type PrimaryResponse<'res> = |Resp of 'res
    type SecondaryReq<'req> = |Req of 'req
    type SecondaryResponse<'res> = |Resp of 'res
        
    let private secondaryTask 
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

    /// migration function for F# code, curried version
    let migrationTaskBuilderFn 
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

    /// migration builder for C# code
    type MigrationTaskBuilder<'req,'resp>(
        legacyFunc, 
        newFunc, 
        migrateFn: Func<'resp, 'resp, Task>, 
        resAdaptFn: Func<'resp, 'resp, 'resp>, 
        reqAdaptFn: Func<'req, 'resp, 'req>) =
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