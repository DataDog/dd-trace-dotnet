namespace Datadog.Trace.TestHelpers.FSharp

module SpanValidator =
    open ValidationTypes
    open TracingIntegrationRules

    let returnSpanResult result =
        match result with
        | Success _ -> (true, "")
        | Failure x -> (false, x)

    let validateAspNetCoreSpan span =
        isAspNetCore span
        |> returnSpanResult
    
    let validateAspNetCoreMvcSpan span =
        isAspNetCoreMvc span
        |> returnSpanResult

    let validateHttpMessageHandlerSpan span =
        isHttpMessageHandler span
        |> returnSpanResult