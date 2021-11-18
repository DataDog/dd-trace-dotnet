namespace Datadog.Trace.TestHelpers.FSharp

module Say =
    let hello name =
        printfn "Hello %s" name

    // I shouldn't need this but I'm not yet familiar enough with F# to fix the error:
    // "error FS0988: Main module of program is empty: nothing will happen when it is run"
    [<EntryPoint>] 
    let main argv =
        0
