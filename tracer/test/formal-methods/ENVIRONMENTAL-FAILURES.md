# Environmental failure injection

The `EnvironmentalFaultInjection` tests exercise the real `AgentWriter`, trace
serialization, `Api`, `HttpClientRequestFactory`, loopback TCP stack, and a
recording intake. They do not replace the trace API with a mock.

Run them with:

```bash
dotnet test tracer/test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj \
  -f net10.0 -m:1 -nodeReuse:false \
  --filter 'Category=EnvironmentalFaultInjection'
```

## Current scenarios

| Schedule | Result | Evidence |
|---|---|---|
| `PUBLISH -> upload complete -> response blocked -> FLUSH` | Flush remains pending until HTTP 200 | Positive barrier evidence |
| `PUBLISH -> upload complete -> response blocked -> CLOSE` | Close remains pending until HTTP 200 | Positive close evidence |
| `PUBLISH -> HTTP 500 -> HTTP 200` | Identical MessagePack body is retried once | Positive retry evidence |
| `PUBLISH -> upload complete -> TCP reset before response` | No retry; payload is dropped | Finding 16 |

The reset scenario uses a raw `TcpListener`, reads the entire HTTP body, and
sets zero-time linger before closing the socket. This avoids the ambiguous
behavior of `HttpListenerResponse.Abort`, which can appear successful to
`HttpClient` after request upload.

These tests establish controlled reachability, not production frequency. The
next transport cases should cover connection refusal followed by recovery,
HTTP 408/413/429 terminal policy, timeout during response, and endpoint
replacement during an active send.
