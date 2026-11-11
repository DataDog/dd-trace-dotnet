# OTLP metrics protobuf-generated bindings

`Metrics.g.cs` is produced by `protoc` from the OTLP metrics `.proto` vendored
under `tracer/test/Datadog.Trace.Tests/OpenTelemetry/Metrics/proto/`
(open-telemetry/opentelemetry-proto@v1.10.0). It is committed for the same
reasons as the trace bindings; see
[`../../Traces/Generated/README.md`](../../Traces/Generated/README.md).

## Regenerating

`metrics.proto` imports `common.proto` and `resource.proto`, which are vendored
under `OpenTelemetry/Traces/proto/`, so both proto roots are needed. Run
`protoc` (libprotoc 25.1 was used) from the `tracer/test/Datadog.Trace.Tests/`
directory:

```sh
protoc \
  --proto_path=OpenTelemetry/Metrics/proto \
  --proto_path=OpenTelemetry/Traces/proto \
  --csharp_out=OpenTelemetry/Metrics/Generated \
  --csharp_opt=file_extension=.g.cs \
  OpenTelemetry/Metrics/proto/opentelemetry/proto/metrics/v1/metrics.proto
```
