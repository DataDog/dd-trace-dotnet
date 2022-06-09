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