# Sensitive Configuration Analyzer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enforce at compile time that configuration keys marked `sensitive: true` in `supported-configurations.yaml` are read only through telemetry-redacting accessors, and migrate OTLP headers to those accessors.

**Architecture:** The existing shared YAML reader will expose a `Sensitive` flag. `ConfigurationBuilderWithKeysAnalyzer` will parse the YAML additional file once per compilation, resolve each `WithKeys()` constant, and reject sensitive keys unless the immediately chained accessor proves `recordValue` is false. Runtime configuration code will use existing redacted string accessors, so no startup-path sensitivity lookup is added.

**Tech Stack:** C# 12, Roslyn analyzers, incremental source-generator support types, xUnit, FluentAssertions, .NET 10 test target.

## Global Constraints

- All changes stay in the existing `brian.marks/sensitive-config-analyzer` worktree and branch.
- `supported-configurations.yaml` remains the single source of truth; do not generate runtime sensitivity metadata.
- Preserve alias fallback, OTLP header parsing, and public APIs.
- `DD_API_KEY` and the four `OTEL_EXPORTER_OTLP*_HEADERS` keys are sensitive.
- Do not mark AppSec's HTML template path sensitive; it is redacted for payload-size reasons.
- Every production behavior change follows a witnessed red-green TDD cycle.
- Before push, run `pre-push-review`, amend fixes into the relevant existing commit, and rerun verification.
- Create the PR as a draft with the `AI Generated` label and the repository PR template.

---

### Task 1: Parse sensitivity metadata from YAML

**Files:**
- Modify: `tracer/build/_build/NativeValidation/YamlReader.cs`
- Create: `tracer/test/Datadog.Trace.SourceGenerators.Tests/YamlReaderTests.cs`

**Interfaces:**
- Consumes: `YamlReader.ParseSupportedConfigurations(string)` and `ConfigurationEntry`.
- Produces: `ConfigurationEntry.Sensitive` as a `bool`, defaulting to `false` and reset for every new configuration entry.

- [x] **Step 1: Write failing parser tests**

Add three behavior assertions using a hand-written YAML fixture:

```csharp
[Fact]
public void ParsesSensitiveMetadata()
{
    var parsed = YamlReader.ParseSupportedConfigurations(YamlWithSensitiveAndOrdinaryEntries);

    parsed.Configurations["DD_API_KEY"].Sensitive.Should().BeTrue();
    parsed.Configurations["DD_TRACE_ENABLED"].Sensitive.Should().BeFalse();
    parsed.Configurations["DD_SERVICE"].Sensitive.Should().BeFalse();
}
```

The fixture places an ordinary entry after a sensitive entry so the final assertion catches failure to reset parser state.

- [x] **Step 2: Run the parser test and verify RED**

Run:

```bash
dotnet test tracer/test/Datadog.Trace.SourceGenerators.Tests/Datadog.Trace.SourceGenerators.Tests.csproj -c Release -f net10.0 --filter FullyQualifiedName~YamlReaderTests --disable-build-servers -m:1
```

Expected: compilation fails because `ConfigurationEntry` has no `Sensitive` property.

- [x] **Step 3: Implement minimal YAML parsing**

In `YamlReader.ParseSupportedConfigurations`:

```csharp
var currentSensitive = false;
```

Recognize `sensitive` as a property that terminates documentation, parse it case-insensitively, pass it into every `ConfigurationEntry` construction, and reset it when a new key begins:

```csharp
case "sensitive":
    currentSensitive = propValue.Equals("true", StringComparison.OrdinalIgnoreCase);
    break;
```

Extend the entry model and its equality contract:

```csharp
public ConfigurationEntry(
    string key,
    string? product,
    string? documentation,
    string? constName,
    string[]? scope,
    string[]? aliases = null,
    bool sensitive = false)
{
    // Existing assignments remain unchanged.
    Sensitive = sensitive;
}

public bool Sensitive { get; }
```

Include `Sensitive` in `Equals()` and `GetHashCode()` so incremental-generator caching notices registry changes.

- [x] **Step 4: Run parser and source-generator suites and verify GREEN**

Run the filtered command from Step 2, followed by:

```bash
dotnet test tracer/test/Datadog.Trace.SourceGenerators.Tests/Datadog.Trace.SourceGenerators.Tests.csproj -c Release -f net10.0 --disable-build-servers -m:1
```

Expected: all source-generator tests pass with no warnings.

- [x] **Step 5: Commit parser support**

```bash
git add tracer/build/_build/NativeValidation/YamlReader.cs tracer/test/Datadog.Trace.SourceGenerators.Tests/YamlReaderTests.cs
git commit -m "[Configuration] Parse sensitive config metadata"
```

### Task 2: Enforce sensitive reads in the analyzer

**Files:**
- Modify: `tracer/src/Datadog.Trace.Tools.Analyzers/Datadog.Trace.Tools.Analyzers.csproj`
- Modify: `tracer/src/Datadog.Trace.Tools.Analyzers/ConfigurationAnalyzers/ConfigurationBuilderWithKeysAnalyzer.cs`
- Modify: `tracer/test/Datadog.Trace.Tools.Analyzers.Tests/ConfigurationAnalyzers/AnalyzerTestHelper.cs`
- Modify: `tracer/test/Datadog.Trace.Tools.Analyzers.Tests/ConfigurationAnalyzers/ConfigurationBuilderWithKeysAnalyzerTests.cs`

**Interfaces:**
- Consumes: `ConfigurationEntry.Sensitive`, Roslyn `AdditionalText`, and direct `ConfigurationKeys` constants passed to `ConfigurationBuilder.WithKeys(string)`.
- Produces: diagnostic `DD0015` when a sensitive key is not immediately consumed by a provably redacted accessor.

- [x] **Step 1: Link the shared parser into the analyzer project**

Add linked compile items for the shared parser and its dependencies:

```xml
<Compile Include="..\..\build\_build\NativeValidation\YamlReader.cs" Link="ConfigurationAnalyzers\YamlReader.cs" />
<Compile Include="..\..\build\_build\NativeValidation\EquatableArray.cs" Link="ConfigurationAnalyzers\EquatableArray.cs" />
<Compile Include="..\Datadog.Trace.SourceGenerators\Helpers\HashCode.cs" Link="ConfigurationAnalyzers\HashCode.cs" />
```

Build the analyzer project once to prove the shared types compile under `netstandard2.0`.

- [x] **Step 2: Add failing analyzer tests for unsafe reads**

Teach `AnalyzerTestHelper` to attach a literal `supported-configurations.yaml` additional file. Add focused tests where `DD_API_KEY` is marked sensitive and `DD_SERVICE` is not:

```csharp
builder.WithKeys({|#0:ConfigurationKeys.ApiKey|}).AsString();
builder.WithKeys(ConfigurationKeys.ServiceName).AsString();
```

Expect `DD0015` only at marker `#0`. Add separate failing cases for:

```csharp
builder.WithKeys({|#0:ConfigurationKeys.ApiKey|}).AsDictionaryResult();
builder.WithKeys({|#0:ConfigurationKeys.ApiKey|}).AsStringResult(null, null, recordValue: true);
var sensitive = builder.WithKeys({|#0:ConfigurationKeys.ApiKey|});
```

Each test names the unsafe branch it catches; do not combine unrelated failures into one assertion.

- [x] **Step 3: Run unsafe-read tests and verify RED**

Run:

```bash
dotnet test tracer/test/Datadog.Trace.Tools.Analyzers.Tests/Datadog.Trace.Tools.Analyzers.Tests.csproj -c Release -f net10.0 --filter FullyQualifiedName~ConfigurationBuilderWithKeysAnalyzerTests --disable-build-servers -m:1
```

Expected: the new tests fail because `DD0015` is not reported.

- [x] **Step 4: Implement YAML loading and the unsafe-read rule**

At compilation start, locate `supported-configurations.yaml`, parse it once, and collect canonical sensitive keys:

```csharp
private static ImmutableHashSet<string> GetSensitiveKeys(AnalyzerOptions options, CancellationToken cancellationToken)
{
    var file = options.AdditionalFiles.FirstOrDefault(
        x => Path.GetFileName(x.Path).Equals(SupportedConfigurationsFileName, StringComparison.OrdinalIgnoreCase));
    var content = file?.GetText(cancellationToken)?.ToString();
    if (string.IsNullOrEmpty(content))
    {
        return ImmutableHashSet<string>.Empty;
    }

    try
    {
        return YamlReader.ParseSupportedConfigurations(content!)
                         .Configurations
                         .Where(x => x.Value.Sensitive)
                         .Select(x => x.Key)
                         .ToImmutableHashSet(StringComparer.Ordinal);
    }
    catch
    {
        return ImmutableHashSet<string>.Empty;
    }
}
```

Add `DD0015` as an error diagnostic. After the existing constant validation, get the field's constant string value. For sensitive values, accept only a direct chain to:

```csharp
AsRedactedString
AsRedactedStringResult
```

For `AsStringResult`, obtain `IInvocationOperation`, find the argument whose bound parameter is named `recordValue`, and require `argument.Value.ConstantValue` to equal `false`. Reject all other accessors and non-chained/stored `HasKeys` values.

Keep missing/malformed YAML silent because the source generator already owns registry diagnostics.

- [x] **Step 5: Run unsafe-read tests and verify GREEN**

Run the filtered analyzer command from Step 3.

Expected: all `ConfigurationBuilderWithKeysAnalyzerTests` pass.

- [x] **Step 6: Add passing tests for allowed redacted reads**

Add separate no-diagnostic tests for:

```csharp
builder.WithKeys(ConfigurationKeys.ApiKey).AsRedactedString();
builder.WithKeys(ConfigurationKeys.ApiKey).AsRedactedStringResult();
builder.WithKeys(ConfigurationKeys.ApiKey).AsStringResult(null, null, recordValue: false);
```

Also prove a malformed or missing YAML additional file does not create `DD0015`; source-generator diagnostics remain authoritative.

- [x] **Step 7: Run the entire analyzer suite**

```bash
dotnet test tracer/test/Datadog.Trace.Tools.Analyzers.Tests/Datadog.Trace.Tools.Analyzers.Tests.csproj -c Release -f net10.0 --disable-build-servers -m:1
```

Expected: 0 failures and no new warnings.

- [x] **Step 8: Commit analyzer enforcement**

```bash
git add tracer/src/Datadog.Trace.Tools.Analyzers tracer/test/Datadog.Trace.Tools.Analyzers.Tests/ConfigurationAnalyzers
git commit -m "[Configuration] Enforce redaction for sensitive keys"
```

### Task 3: Mark and redact credential-bearing configuration

**Files:**
- Modify: `tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml`
- Modify: `tracer/src/Datadog.Trace/Configuration/ExporterSettings.cs`
- Modify: `tracer/src/Datadog.Trace/Configuration/TracerSettings.cs`
- Modify: `tracer/test/Datadog.Trace.Tests/Configuration/ExporterSettingsTests.cs`
- Modify: `tracer/test/Datadog.Trace.Tests/Configuration/TracerSettingsTests.cs`

**Interfaces:**
- Consumes: `AsRedactedString()`, `StringConfigurationSource.ParseCustomKeyValues(string?, bool, char)`, and existing configuration alias fallback.
- Produces: redacted telemetry for `DD_API_KEY` and all OTLP header settings, while preserving parsed header values for exporters.

- [x] **Step 1: Add failing telemetry regression tests**

In `ExporterSettingsTests`, configure distinct sentinel secrets for the general, metrics, and traces header keys. Construct `ExporterSettings` with a real `ConfigurationTelemetry` and assert each matching entry is `Redacted` with a null `StringValue`. Assert the endpoint entry remains a normal string entry.

In `TracerSettingsTests`, configure a logs-header sentinel and assert:

```csharp
settings.OtlpLogsHeaders.Should().Contain(new KeyValuePair<string, string>("dd-api-key", logsSentinel));
entries.Where(x => x.Key == ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsHeaders)
       .Should()
       .OnlyContain(x => x.Type == ConfigurationTelemetryEntryType.Redacted && x.StringValue is null);
```

- [x] **Step 2: Run affected tracer tests and verify RED**

```bash
dotnet test tracer/test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj -c Release -f net10.0 --filter "FullyQualifiedName~ExporterSettingsTests|FullyQualifiedName~TracerSettingsTests" --disable-build-servers -m:1
```

Expected: sentinel values are recorded as string telemetry, so the new assertions fail.

- [x] **Step 3: Mark sensitive YAML entries**

Add `sensitive: true` to `DD_API_KEY` and the four OTLP header entries. Do not add sensitivity metadata to other header-named settings.

- [x] **Step 4: Convert OTLP runtime reads to redacted accessors**

In `ExporterSettings.RawSettings`, replace the three OTLP header `AsString()` calls with `AsRedactedString()`.

Remove `TracerSettings.OtlpMetricsHeaders`, its duplicate parse block, and its now-redundant parsing test. `ExporterSettings` remains the metrics-header owner.

For log headers, preserve the parsed dictionary while redacting the source read:

```csharp
var rawOtlpLogsHeaders = config
                        .WithKeys(ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsHeaders)
                        .AsRedactedString();

OtlpLogsHeaders = (StringConfigurationSource.ParseCustomKeyValues(rawOtlpLogsHeaders, allowOptionalMappings: false, separator: '=')
                ?? new Dictionary<string, string>())
                  .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                  .ToDictionary(kvp => kvp.Key.Trim(), kvp => kvp.Value?.Trim() ?? string.Empty);
```

- [x] **Step 5: Run affected tracer tests and verify GREEN**

Run the filtered command from Step 2. Also run the existing OTLP parsing theories without filters broad enough to skip them.

Expected: all affected tests pass, sentinels never appear in string telemetry, and parsing/fallback results are unchanged.

- [x] **Step 6: Build Datadog.Trace with the analyzer enabled**

```bash
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj -c Release --disable-build-servers -m:1
```

Expected: all target frameworks compile with no `DD0015` violations. Existing `DD_API_KEY` reads already use redacted accessors.

- [x] **Step 7: Commit runtime migration**

```bash
git add tracer/src/Datadog.Trace/Configuration tracer/test/Datadog.Trace.Tests/Configuration
git commit -m "[Configuration] Redact sensitive configuration reads"
```

### Task 4: Document and verify the complete change

**Files:**
- Modify: `docs/development/Configuration/AddingConfigurationKeys.md`
- Modify: `docs/superpowers/plans/2026-07-31-sensitive-configuration-analyzer.md` only to check completed steps during execution.

**Interfaces:**
- Consumes: the implemented YAML property and analyzer behavior.
- Produces: contributor guidance and fresh verification evidence for publishing.

- [x] **Step 1: Update contributor documentation**

Document that `sensitive: true` marks credential-bearing values, that aliases inherit redaction through normal fallback, and that sensitive keys must use `AsRedactedString*` or an explicit compile-time `recordValue: false` path.

- [x] **Step 2: Run focused suites**

```bash
dotnet test tracer/test/Datadog.Trace.SourceGenerators.Tests/Datadog.Trace.SourceGenerators.Tests.csproj -c Release -f net10.0 --disable-build-servers -m:1
dotnet test tracer/test/Datadog.Trace.Tools.Analyzers.Tests/Datadog.Trace.Tools.Analyzers.Tests.csproj -c Release -f net10.0 --disable-build-servers -m:1
dotnet test tracer/test/Datadog.Trace.Tests/Datadog.Trace.Tests.csproj -c Release -f net10.0 --filter "FullyQualifiedName~ExporterSettingsTests|FullyQualifiedName~TracerSettingsTests" --disable-build-servers -m:1
```

Expected: 0 failures in all three commands.

- [x] **Step 3: Run the full tracer build and repository checks**

```bash
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj -c Release --disable-build-servers -m:1
git diff --check master...HEAD
```

Expected: build exit code 0 for every target framework and no whitespace errors.

- [x] **Step 4: Commit documentation and plan completion**

```bash
git add docs/development/Configuration/AddingConfigurationKeys.md docs/superpowers/plans/2026-07-31-sensitive-configuration-analyzer.md
git commit -m "[Configuration] Document sensitive config enforcement"
```

- [ ] **Step 5: Run mandatory pre-push review**

Invoke the repository's `pre-push-review` skill. Apply valid findings, amend them into the commit that introduced the issue, and rerun every affected verification command.

- [ ] **Step 6: Publish a draft PR**

Read `.github/pull_request_template.md`, push with the `bm1549` public-repository account, and create a draft PR against `master` with the `AI Generated` label. The description must explain the compile-time design, lack of runtime lookup, and exact test evidence.

- [ ] **Step 7: Babysit CI**

Invoke `dd:pr-babysit` and monitor until every real correctness check is green. Ignore `devflow/mergegate` and any aggregator blocked only by that gate.
