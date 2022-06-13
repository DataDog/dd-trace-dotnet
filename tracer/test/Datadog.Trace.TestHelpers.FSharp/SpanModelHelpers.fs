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

    // Helper functions
    let surroundStringWithQuotes (value: string) = $"\"{value}\""

    let stringJoinWithComma (values: string[]) = System.String.Join(", ", values)

    // Comparison functions
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

    let tagMatchesOneOf tagName (expectedValueArray: string[]) (span: MockSpan) =
        let value = span.GetTag tagName
        match expectedValueArray |> Array.tryFind (fun elm -> elm = value) with
            | Some result -> Success span
            | None -> Failure ($"Tag \"{tagName}\" has value \"{value}\" but was expected to have one of the following values: " + (expectedValueArray |> Array.map surroundStringWithQuotes |> stringJoinWithComma))

    let isPresent extractProperty (span: MockSpan) =
        match extractProperty span with
        | (propertyName, "") -> Failure $"Property \"{propertyName}\" was expected to be present, but the property is empty"
        | (propertyName, null) -> Failure $"Property \"{propertyName}\" was expected to be present, but the property is null"
        | (_, _) -> Success span

    let matches extractProperty (expectedValue: string) (span:MockSpan) =
        match extractProperty span with
        | (propertyName, value) when value <> expectedValue -> Failure $"Property \"{propertyName}\" was expected to have value \"{expectedValue}\", but the property is \"{value}\""
        | (_, _) -> Success span

    let matchesOneOf extractProperty (expectedValueArray: string[]) (span:MockSpan) =
        let (propertyName, value) = extractProperty span
        match expectedValueArray |> Array.tryFind (fun elm -> elm = value) with
        | Some result -> Success span
        | None -> Failure ($"Property \"{propertyName}\" has value \"{value}\" but was expected to have one of the following values: " + (expectedValueArray |> Array.map surroundStringWithQuotes |> stringJoinWithComma))