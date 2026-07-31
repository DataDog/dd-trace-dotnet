# Sensitive Configuration Analyzer Design

## Context

Configuration telemetry records most configuration values to help diagnose tracer setup. Some settings, including `DD_API_KEY` and OpenTelemetry exporter headers, contain credentials and must only produce redacted telemetry entries.

The existing configuration API already supports redacted string reads through `AsRedactedString()` and `AsRedactedStringResult()`. Adding a runtime sensitivity lookup to every telemetry write would protect the values, but it would also add work to the tracer startup hot path. This design moves enforcement to compile time instead.

## Goals

- Keep `supported-configurations.yaml` as the single source of truth for sensitive configuration keys.
- Report a build error when tracer code reads a sensitive key through an accessor that records its value.
- Redact the four OTLP header settings without a runtime sensitivity lookup.
- Mark existing credential-bearing settings, currently `DD_API_KEY`, as sensitive.
- Preserve alias fallback, parsing behavior, and public APIs.

## Non-goals

- Runtime enforcement for third-party callers or reflection-based configuration reads.
- Data-flow analysis across variables that store `ConfigurationBuilder.HasKeys` values.
- Treating every deliberately redacted value as sensitive. For example, AppSec's HTML template path is redacted because its value can be large, not because it contains a credential.
- Adding a code fix in this change.

## Architecture

### YAML metadata

Add an optional `sensitive: true` property to configuration entries. The shared `YamlReader` will parse and expose the property on `ConfigurationEntry`; omitted values default to `false`. Values are case-insensitive `true` or `false`. Other tokens are parse errors reported by the source generator as `DDSG0007` and by the analyzer as `DD0016`.

The analyzer project will link the existing shared YAML parser and its helper types. `tracer/src/Directory.Build.props` will supply `supported-configurations.yaml` to every project that receives the analyzer. During compilation start, the analyzer will require exactly one readable additional file, parse it once, and build an immutable set of canonical sensitive keys. Missing, ambiguous, unreadable, or invalid metadata produces `DD0016` instead of disabling the rule.

### Analyzer rule

Extend `ConfigurationBuilderWithKeysAnalyzer` with a new error diagnostic for unsafe sensitive-key reads.

For each `ConfigurationBuilder.WithKeys(...)` invocation, the analyzer will continue enforcing that the argument is a direct `ConfigurationKeys` or `PlatformKeys` constant. When the constant value is in the sensitive-key set, the analyzer will use Roslyn operations and symbols to inspect the immediately chained accessor. Parentheses and conversions around `WithKeys(...)` are transparent, but the accessor must resolve to an instance method on the returned `ConfigurationBuilder.HasKeys` type:

- `AsRedactedString(...)` is allowed.
- `AsRedactedStringResult(...)` is allowed.
- `AsRedactedDictionaryResult(...)` is allowed.
- `AsStringResult(..., recordValue: false)` is allowed when Roslyn can prove the argument is the constant `false`.
- Any accessor that records values, an unrecognized accessor, or storing the intermediate `HasKeys` value is rejected because the analyzer cannot prove that telemetry is redacted.

The diagnostic will be reported on the sensitive configuration-key argument and explain which redacted accessors are permitted.

This deliberately targets the fluent `ConfigurationBuilder` path already governed by the analyzer. Direct `IConfigurationSource` calls are outside this rule and are already intended to be restricted separately.

### Tracer configuration reads

Mark these registry entries as sensitive:

- `DD_API_KEY`
- `OTEL_EXPORTER_OTLP_HEADERS`
- `OTEL_EXPORTER_OTLP_LOGS_HEADERS`
- `OTEL_EXPORTER_OTLP_METRICS_HEADERS`
- `OTEL_EXPORTER_OTLP_TRACES_HEADERS`

Change the OTLP string reads in `ExporterSettings` to `AsRedactedString()`. Remove the duplicate metrics-header parsing in `TracerSettings`, leaving `ExporterSettings` as its owner.

`TracerSettings` still owns OTLP log headers as a parsed dictionary. It will use `AsRedactedDictionaryResult()` so `JsonConfigurationSource` keeps its object and array handling. The accessor passes an internal telemetry decorator to the existing dictionary API. The decorator forces string-value records to use `recordValue: false`, including parse and unexpected-type errors, and forwards every other operation unchanged. The existing default, filtering, and trimming flow remains in place. Alias fallback remains in `ConfigurationBuilder`, so the general header setting continues to work as the logs fallback.

No runtime `IsSensitive()` method or sensitivity collection will be generated.

## Data Flow

1. MSBuild supplies `supported-configurations.yaml` as a Roslyn additional file to every analyzer consumer under `tracer/src`.
2. The analyzer parses the registry once at compilation start and collects keys marked `sensitive: true`.
3. A syntax action resolves each `WithKeys()` argument to its constant value.
4. Sensitive constants must flow directly into an accessor that records telemetry with `recordValue: false`.
5. At runtime, redacted string accessors pass `recordValue: false` to configuration sources. The redacted dictionary accessor wraps telemetry internally and uses the existing dictionary interface. Both paths record redacted entries without a key lookup.

## Testing

Follow red-green TDD for each behavior:

- YAML parser tests prove mixed-case boolean values are captured, omitted values default to false, parser state resets between entries, and invalid tokens produce `DDSG0007`.
- Analyzer tests prove ordinary keys may use normal accessors.
- Analyzer tests prove sensitive keys may use both redacted accessors and an explicit constant `recordValue: false`.
- Analyzer tests prove sensitive keys fail with normal string/dictionary accessors, `recordValue: true`, non-constant values, missing accessors, and stored intermediate values.
- Analyzer tests prove missing or invalid sensitivity metadata fails closed with `DD0016`, including in a consumer assembly.
- Configuration tests prove all OTLP header values produce non-empty redacted telemetry while a nearby non-sensitive setting still records normally. Log-header coverage includes JSON objects, primary and alias strings, and unexpected object types.
- Existing API-key call sites compile under the new rule because they already use redacted accessors.
- Existing OTLP header parsing and fallback tests continue to pass after ownership consolidation.

Run the analyzer and source-generator test suites, affected tracer configuration tests, and a full `Datadog.Trace` build before publishing.

## Documentation

Update the configuration-key development guide to document `sensitive`, its compile-time enforcement, and the accepted redacted access patterns.
