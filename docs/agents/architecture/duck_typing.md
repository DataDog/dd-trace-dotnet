# Duck Typing Mechanism

## Purpose

Interact with external types across versions without adding hard dependencies. Provides fast, strongly-typed access via generated proxies.

## Core Concepts

### Shape Interfaces

Define minimal contracts (properties/methods) you need, e.g., `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AWS/Kinesis/IPutRecordsRequest.cs` and `IAmazonKinesisRequestWithStreamName.cs`.

### Interface + IDuckType Constraints

In CallTarget integrations, use `where TReq : IMyShape, IDuckType`. Example: `PutRecordsIntegration.OnMethodBegin` in `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AWS/Kinesis/PutRecordsIntegration.cs`.

## Creating Proxies

### Generic Constraints (Automatic)
Generic constraints let the woven callsite pass a proxy automatically:
```csharp
where TReq : IMyShape, IDuckType
```

### At Runtime by Type
```csharp
DuckType.GetOrCreateProxyType(typeof(IMyShape), targetType)
CreateInstance(...)
```
See `tracer/src/Datadog.Trace/OTelMetrics/OtlpMetricsExporter.cs`.

### From an Instance
```csharp
obj.DuckCast<IMyShape>()
```
For nested values. See `GetRecordsIntegration` `DuckCast<IRecord>` in `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/AWS/Kinesis/GetRecordsIntegration.cs`.

## Accessing Originals

All proxies implement `IDuckType` (`tracer/src/Datadog.Trace/DuckTyping/IDuckType.cs`) exposing `Instance` and `Type`.

## Binding Rules

Name/signature matching; support for properties/fields/methods. Use attributes in `tracer/src/Datadog.Trace/DuckTyping/` to control binding:
- `[DuckField]`
- `[DuckPropertyOrField]`
- `[DuckIgnore]`
- `[DuckInclude]`
- `[DuckReverseMethod]`
- `[DuckCopy]`
- `[DuckAsClass]`
- `[DuckType]`
- `[DuckTypeTarget]`

## Visibility

Proxies can access non-public members; the library emits IL and uses `IgnoresAccessChecksToAttribute` if present (`tracer/src/Datadog.Trace/DuckTyping/IgnoresAccessChecksToAttribute.cs`).

## Performance

Enable `#nullable` in new files; proxies are cached per (shape,target) for low overhead. See core implementation `tracer/src/Datadog.Trace/DuckTyping/DuckType.cs` and partials.

## Best Practices

- Prefer interface shapes
- Avoid vendor types in signatures
- Check for `proxy.Instance != null` before use
- Keep shapes stable across upstream versions

## Deep Dive

For comprehensive documentation, read `docs/development/DuckTyping.md`.
