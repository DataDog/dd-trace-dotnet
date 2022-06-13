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
    let surroundStringWithBraces (value: string) = sprintf "[%s]" value

    let surroundStringWithQuotes (value: string) = sprintf "\"%s\"" value

    let stringJoinWithComma (values: string[]) = System.String.Join(", ", values)

    let sprintPresentFailure propertyKind propertyName = sprintf "%s \"%s\" was expected to be present" propertyKind propertyName

    let sprintMatchesFailure propertyKind propertyName expectedValue actualValue = sprintf "%s \"%s\" was expected to have value %s, but the value is \"%s\"" propertyKind propertyName expectedValue actualValue

    let sprintMatchesOneOfFailure propertyKind propertyName expectedValue actualValue = sprintf "%s \"%s\" was expected to have one of the following values %s, but the value is \"%s\"" propertyKind propertyName expectedValue actualValue

    // Comparison functions
    let isOptional result span =
        match result with
        | _ -> Success span

    let isPresent result failureString span =
        match result with
        | null -> Failure failureString
        | _ -> Success span

    let matches expectedValue result sprintFailureString span =
        match result with
        | actualValue when actualValue <> expectedValue -> Failure (sprintFailureString actualValue)
        | _ -> Success span

    let matchesOneOf (expectedValueArray: string[]) result sprintFailureString span =
        match expectedValueArray |> Array.tryFind (fun elm -> elm = result) with
            | Some _ -> Success span
            | None -> Failure (sprintFailureString result)
    
    // DSL functions for easier parsing and documentation generation
    let propertyIsPresent extractProperty (span: MockSpan) =
        let (propertyName, result) = extractProperty span
        let failureString = sprintPresentFailure "property" propertyName
        isPresent result failureString span

    let propertyMatches extractProperty (expectedValue: string) (span:MockSpan) =
        let (propertyName, result) = extractProperty span
        let sprintFailureWithActualValue = sprintMatchesFailure "property" propertyName expectedValue
        matches expectedValue result sprintFailureWithActualValue span

    let propertyMatchesOneOf extractProperty (expectedValueArray: string[]) (span:MockSpan) =
        let (propertyName, result) = extractProperty span
        let expectedValueArrayString = expectedValueArray |> Array.map surroundStringWithQuotes |> stringJoinWithComma |> surroundStringWithBraces
        let sprintFailureWithActualValue = sprintMatchesFailure "property" propertyName expectedValueArrayString
        matchesOneOf expectedValueArray result sprintFailureWithActualValue span

    let tagIsOptional tagName (span: MockSpan) =
        isOptional (span.GetTag tagName) span

    let tagIsPresent tagName (span: MockSpan) =
        let result = (span.GetTag tagName)
        let failureString = sprintPresentFailure "tag" tagName
        isPresent result failureString span

    let tagMatches tagName (expectedValue: string) (span:MockSpan) =
        let result = span.GetTag tagName
        let sprintFailureWithActualValue = sprintMatchesFailure "tag" tagName expectedValue
        matches expectedValue result sprintFailureWithActualValue span

    let tagMatchesOneOf tagName (expectedValueArray: string[]) (span: MockSpan) =
        let result = span.GetTag tagName
        let expectedValueArrayString = expectedValueArray |> Array.map surroundStringWithQuotes |> stringJoinWithComma |> surroundStringWithBraces
        let sprintFailureWithActualValue = sprintMatchesOneOfFailure "tag" tagName expectedValueArrayString
        matchesOneOf expectedValueArray result sprintFailureWithActualValue span