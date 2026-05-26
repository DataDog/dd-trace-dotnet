# OTLP protobuf-generated bindings

These `*.g.cs` files are produced by `protoc` from the OTLP `.proto` files
vendored under `tracer/test/Datadog.Trace.Tests/OpenTelemetry/Traces/proto/`
(open-telemetry/opentelemetry-proto@v1.10.0).

They are committed to the repo rather than generated at build time because
`Grpc.Tools` does not ship a musl/Alpine `protoc` binary and NuGet does not
preserve the executable bit when restoring on Unix, so build-time generation
failed across all Linux/macOS CI legs.

## Regenerating

Run `protoc` (any version recent enough to understand proto3, e.g. 25.x; the
files in this directory were generated with libprotoc 25.1) from the
`tracer/test/Datadog.Trace.Tests/` directory:

```sh
protoc \
  --proto_path=OpenTelemetry/Traces/proto \
  --csharp_out=OpenTelemetry/Traces/Generated \
  --csharp_opt=file_extension=.g.cs \
  OpenTelemetry/Traces/proto/opentelemetry/proto/common/v1/common.proto \
  OpenTelemetry/Traces/proto/opentelemetry/proto/resource/v1/resource.proto \
  OpenTelemetry/Traces/proto/opentelemetry/proto/trace/v1/trace.proto \
  OpenTelemetry/Traces/proto/opentelemetry/proto/collector/trace/v1/trace_service.proto
```

If you have `Grpc.Tools` restored locally via NuGet, its bundled `protoc` works
too — e.g. on macOS x64,
`packages/grpc.tools/<ver>/tools/macosx_x64/protoc`.
