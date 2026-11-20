# Inferred proxy services

When an upstream proxy sits in front of an instrumented application, the tracer can create an
*inferred proxy span* that represents the proxy itself and parents the server span. The proxy does
not run the tracer, so the span is reconstructed entirely from request headers that the proxy is
configured to inject.

The feature is opt-in:

```
DD_TRACE_INFERRED_PROXY_SERVICES_ENABLED=true
```

With it disabled (the default), none of the code below runs — `ExtractAndCreateInferredProxyScope`
returns before reading any header.

## Supported proxies

| `x-dd-proxy` value | Operation name    | Injects a timestamp? |
| ------------------ | ----------------- | -------------------- |
| `aws-apigateway`   | `aws.apigateway`  | yes                  |
| `azure-apim`       | `azure.apim`      | yes                  |
| `azure-fd`         | `azure.frontdoor` | **no**               |

The value is matched case-insensitively. An unrecognized value is logged at debug and no span is
created.

## Headers

| Header                       | Maps to                               | Required |
| ---------------------------- | ------------------------------------- | -------- |
| `x-dd-proxy`                 | selects the proxy (table above)        | yes      |
| `x-dd-proxy-request-time-ms` | span start time, Unix epoch ms         | see below |
| `x-dd-proxy-domain-name`     | service name, and the host in `http.url` | no     |
| `x-dd-proxy-httpmethod`      | `http.method`, and the resource name prefix | no  |
| `x-dd-proxy-path`            | `http.route`, and the path in `http.url` | no     |
| `x-dd-proxy-region`          | `region`                               | no       |
| `x-dd-proxy-stage`           | `stage`                                | no       |

Only `x-dd-proxy` is required for every proxy. `x-dd-proxy-request-time-ms` is required for
`aws-apigateway` and `azure-apim` — without a parseable value those extractors fail and no span is
created at all; `azure-fd` synthesizes it instead (see below). Absent optional headers leave the
corresponding field unset rather than empty.

Not every proxy reads every header: `aws-apigateway` does not read `x-dd-proxy-region`, `azure-apim`
does not read `x-dd-proxy-stage`, and `azure-fd` reads both. Other SDKs also read
`x-dd-proxy-resource-path` (a route *template*, used for `http.route`) plus `-account-id`, `-api-id`
and `-user`; none of those is read here yet, so .NET derives `http.route` from `x-dd-proxy-path`
instead. Treat the table above as what this tracer implements, not as the cross-SDK contract — other
SDKs additionally accept `aws-httpapi` and `azure-gw`.

Two further divergences affect tag *values* rather than which headers are read. `azure-fd` emits
`http.url` with an `https://` scheme, matching the other SDKs and the cross-SDK system-tests
validator; the `aws-apigateway` and `azure-apim` factories still omit it. And .NET lower-cases and
id-quantizes `http.route` and the resource name, where other SDKs emit the route template verbatim.

## Azure Front Door

Front Door is configured through [rule sets][ruleset]. Add a *Modify request header* action per
header, using [server variables][servervars] in `{variable}` form:

| Header                   | Value          |
| ------------------------ | -------------- |
| `x-dd-proxy`             | `azure-fd`     |
| `x-dd-proxy-domain-name` | `{hostname}`   |
| `x-dd-proxy-httpmethod`  | `{http_method}` |
| `x-dd-proxy-path`        | `{url_path}`   |

Server variables are only available on the Standard and Premium tiers.

Three Front Door behaviours differ from the other proxies and are worth knowing when reading the
code or a trace:

- **No timestamp.** Front Door's rule set exposes no server variable for the current time, so
  `x-dd-proxy-request-time-ms` cannot be populated. `AzureFrontDoorExtractor` therefore synthesizes
  the start time from the tracer's own clock (`TraceClock`) at the moment the headers are read. The inferred span
  consequently does **not** measure time spent inside Front Door — it begins when the request
  reaches the application. If a value *is* present and parses, it is used instead, so this needs no
  change should Front Door gain such a variable.

  Because Front Door forwards client headers by default, any value in that header arrived from the
  caller rather than from the proxy. An unusable value is therefore treated as "not provided" and
  logged at debug, rather than failing extraction or logging a warning on every request.

- **`{url_path}` has no leading slash.** It is "the part of the request URI without the arguments or
  leading slash", so `/api/foo` arrives as `api/foo`. The span factory prepends a slash, trimming
  any existing one first so a future change to absolute paths would not double it. A request to the
  site root yields an *empty* value, which is treated as the root path `/` rather than as a missing
  path.

- **No region variable.** There is no server variable for the Front Door edge region
  (`{geo_country}` is the *client's* country, not the proxy's), so `x-dd-proxy-region` is normally
  omitted and the `region` tag is absent.

## Code layout

Under `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/Proxy/`:

- `InferredProxySpanHelper` — entry point; reads `x-dd-proxy` and dispatches to a coordinator.
- `IInferredProxyExtractor` / `IInferredSpanFactory` — one implementation pair per proxy.
- `InferredProxyCoordinator` — pairs an extractor with a factory and creates the scope.
- `InferredProxyData` — the extracted values. Normalizes the HTTP method to upper case.

Inferred proxy spans carry `InferredProxyTags`, including the `_dd.inferred_span` metric. The agent
payload path uses that metric to recognize them (`SpanMessagePackFormatter`), but
`AzureFunctionsCommon` matches operation names instead — see below, and note that a newly added proxy
must be listed there as well as in `InferredProxySpanHelper`.

### Interaction with Azure Functions

`AzureFunctionsCommon` writes the "serverless" metadata (`aas.function.*`, span type, resource name)
onto a span above the function span. When an inferred proxy span is the trace root it must not be
overwritten, because it represents the proxy rather than the function. The metadata is written to
the proxy's nearest descendant instead — for both `azure.frontdoor` and `azure.apim`. See
`GetFunctionMetadataTarget`.

## Testing

Unit tests live in `tracer/test/Datadog.Trace.ClrProfiler.Managed.Tests/Serverless/`, with one
extractor and one span-factory suite per proxy plus `InferredProxySpanHelperTests` for dispatch. The
Azure Functions interaction above is covered separately, in
`tracer/test/Datadog.Trace.Tests/ClrProfiler/AutoInstrumentation/Azure/Functions/AzureFunctionsCommonTests.cs`.

End-to-end coverage runs through the Azure Functions sample, which injects the headers itself:

```
./tracer/build.ps1 BuildAndRunWindowsAzureFunctionsTests
```

`Samples.AzureFunctions.V4Isolated` calls its own HTTP trigger with proxy headers when
`DD_TEST_APIM_ENABLED` or `DD_TEST_FRONTDOOR_ENABLED` is set; the matching test classes are
`IsolatedRuntimeV4AzureApim` and `IsolatedRuntimeV4AzureFrontDoor`. These require Windows and the
Azure Functions host, and assert span shape through Verify snapshots in `tracer/test/snapshots/`,
which are generated by running the test and accepting the `.received.txt` it produces.

[ruleset]: https://learn.microsoft.com/en-us/azure/frontdoor/front-door-rules-engine
[servervars]: https://learn.microsoft.com/en-us/azure/frontdoor/rule-set-server-variables
