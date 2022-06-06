namespace Datadog.Trace.TestHelpers.FSharp

module SpanModelHelpers =
    open ValidationTypes
    open Datadog.Trace.TestHelpers

    // Extraction functions that take a span and return a tuple:
    // (propertyName, value)
    let traceId (span: MockSpan) =
        ( (nameof span.TraceId), span.TraceId.ToString() )

    let spanId (span: MockSpan) =
        ( (nameof span.SpanId), span.SpanId.ToString() )

    let resource (span: MockSpan) =
        ( (nameof span.Resource), span.Resource )
    
    let name (span: MockSpan) =
        ( (nameof span.Name), span.Name )

    let service (span: MockSpan) =
        ( (nameof span.Service), span.Service )

    let ``type`` (span: MockSpan) =
        ( (nameof span.Type), span.Type )

    let tagIsPresent tagName (span: MockSpan) =
        match span.GetTag tagName with
        | null -> Failure $"Tag \"{tagName}\" was expected to be present, but the tag is missing"
        | _ -> Success span

    let tagIsOptional tagName (span: MockSpan) =
        match span.GetTag tagName with
        | _ -> Success span
            
    let tagMatches tagName expectedValue (span: MockSpan) =
        match span.GetTag tagName with
        | null -> Failure $"Tag \"{tagName}\" was expected to have value \"{expectedValue}\", but the tag is missing"
        | value when value <> expectedValue -> Failure $"Tag \"{tagName}\" was expected to have value \"{expectedValue}\", but the tag value is \"{value}\""
        | _ -> Success span

    let metricIsPresent metricName (span: MockSpan) =
        match span.GetMetric metricName with
        | metric when not metric.HasValue -> Failure $"Metric \"{metricName}\" was expected to be present, but the metric is missing"
        | _ -> Success span

    let metricMatches metricName expectedValue (span: MockSpan) =
        match span.GetMetric metricName with
        | metric when not metric.HasValue -> Failure $"Metric \"{metricName}\" was expected to be present"
        | metric when metric.Value <> expectedValue -> Failure $"Metric \"{metricName}\" was expected to to have value \"{expectedValue}\", but the tag value is \"{metric.Value}\""
        | _ -> Success span

    let isPresentAndNonZero extractProperty (span: MockSpan) =
        match extractProperty span with
        | (propertyName, "") -> Failure $"Property \"{propertyName}\" was expected to be present and non-zero, but the property is empty"
        | (propertyName, null) -> Failure $"Property \"{propertyName}\" was expected to be present and non-zero, but the property is null"
        | (propertyName, "0") -> Failure $"Property \"{propertyName}\" was expected to be present and non-zero, but the property is 0"
        | (_, _) -> Success span
    
    let isPresent extractProperty (span: MockSpan) =
        match extractProperty span with
        | (propertyName, "") -> Failure $"Property \"{propertyName}\" was expected to be present, but the property is empty"
        | (propertyName, null) -> Failure $"Property \"{propertyName}\" was expected to be present, but the property is null"
        | (_, _) -> Success span

    let matches extractProperty (expectedValue: string) (span:MockSpan) =
        match extractProperty span with
        | (propertyName, value) when value <> expectedValue -> Failure $"Property \"{propertyName}\" was expected to have value \"{expectedValue}\", but the property is \"{value}\""
        | (_, _) -> Success span