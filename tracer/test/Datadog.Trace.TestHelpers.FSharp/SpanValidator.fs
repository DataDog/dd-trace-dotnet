namespace Datadog.Trace.TestHelpers.FSharp

module SpanValidator =
    open ValidationTypes
    open TracingIntegrationRules

    let returnSpanResult result =
        match result with
        | Success _ -> (true, "")
        | Failure x -> (false, x)

    let validateRule rule span =
        rule span
        |> returnSpanResult

    let validateAspNetCoreSpan span =
        isAspNetCore span
        |> returnSpanResult
    
    let validateAspNetCoreMvcSpan span =
        isAspNetCoreMvc span
        |> returnSpanResult

    let validateWcfServerSpan span =
        ``isWcf (server)`` span
        |> returnSpanResult