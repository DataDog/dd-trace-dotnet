namespace Datadog.Trace.TestHelpers.FSharp

module SpanValidator =
    open ValidationTypes
    open SpanModel

    let returnSpanResult result =
        match result with
        | Success _ -> (true, "")
        | Failure x -> (false, x)

    let validateSpan span =
        isSpan span
        |> returnSpanResult

    let validateRootSpan span =
        isRootSpan span
        |> returnSpanResult

    let validateAspNetCoreSpan span =
        isAspNetCore span
        |> returnSpanResult
    
    let validateAspNetCoreMvcSpan span =
        isAspNetCoreMvc span
        |> returnSpanResult