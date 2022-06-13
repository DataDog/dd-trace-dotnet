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
    let isOptional result propertyKind propertyName (span: MockSpan) =
        match result with
        | _ -> Success span

    let isPresent result propertyKind propertyName (span: MockSpan) =
        match result with
        | null -> Failure $"{propertyKind} \"{propertyName}\" was expected to be present, but the {propertyKind} value is null"
        | _ -> Success span

    let matches result propertyKind propertyName expectedValue (span: MockSpan) =
        match result with
        | value when value <> expectedValue -> Failure $"{propertyKind} \"{propertyName}\" was expected to have value \"{expectedValue}\", but the {propertyKind} value is \"{value}\""
        | _ -> Success span

    let matchesOneOf result propertyKind propertyName (expectedValueArray: string[]) (span: MockSpan) =
        match expectedValueArray |> Array.tryFind (fun elm -> elm = result) with
            | Some _ -> Success span
            | None -> Failure ($"{propertyKind} \"{propertyName}\" was expected to have one of the following values [" + (expectedValueArray |> Array.map surroundStringWithQuotes |> stringJoinWithComma) + $"], but the {propertyKind} value is \"{result}\"")
    
    // DSL functions for easier parsing and documentation generation
    let propertyIsPresent extractProperty (span: MockSpan) =
        let (propertyName, result) = extractProperty span
        isPresent result "property" propertyName span

    let propertyMatches extractProperty (expectedValue: string) (span:MockSpan) =
        let (propertyName, result) = extractProperty span
        matches result "property" propertyName expectedValue span

    let propertyMatchesOneOf extractProperty (expectedValueArray: string[]) (span:MockSpan) =
        let (propertyName, result) = extractProperty span
        matchesOneOf result "property" propertyName expectedValueArray span

    let tagIsOptional tagName (span: MockSpan) =
        isOptional (span.GetTag tagName) "tag" tagName span

    let tagIsPresent tagName (span: MockSpan) =
        isPresent (span.GetTag tagName) "tag" tagName span

    let tagMatches tagName (expectedValue: string) (span:MockSpan) =
        matches (span.GetTag tagName) "tag" tagName expectedValue span

    let tagMatchesOneOf tagName (expectedValueArray: string[]) (span: MockSpan) =
        matchesOneOf (span.GetTag tagName) "tag" tagName expectedValueArray span