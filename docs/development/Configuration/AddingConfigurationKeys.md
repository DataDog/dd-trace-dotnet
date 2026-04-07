# Adding New Configuration Keys

This guide explains how to add new configuration keys to the .NET Tracer. Configuration keys are automatically generated from a single YAML source file using source generators.

## Table of Contents

- [Overview](#overview)
- [Step-by-Step Guide](#step-by-step-guide)
  - [1. Add the Configuration Key Definition](#1-add-the-configuration-key-definition)
  - [2. (Optional) Add Aliases](#2-optional-add-aliases)
  - [3. (Optional) Override Constant Name](#3-optional-override-constant-name)
  - [4. Build to Generate Code](#4-build-to-generate-code)
  - [5. Use the Generated Key](#5-use-the-generated-key)
  - [6. Add to Telemetry Normalization Rules](#6-add-to-telemetry-normalization-rules)
  - [7. Test Your Changes](#7-test-your-changes)
- [Configuration Key Organization](#configuration-key-organization)
- [Examples](#examples)
- [Troubleshooting](#troubleshooting)

## Overview

Configuration keys in the .NET Tracer are defined in a single source file:

- **`tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml`** - Defines the configuration keys, their
  environment variable names, types, defaults, optional aliases, constant name overrides, and XML documentation.

Two source generators read this file at build time:

1. **`ConfigurationKeysGenerator`** - Generates the configuration key constants:
   - `ConfigurationKeys.g.cs` - Main configuration keys class with all constants
   - `ConfigurationKeys.<Product>.g.cs` - Product-specific partial classes (e.g., `ConfigurationKeys.OpenTelemetry.g.cs`)

2. **`ConfigurationKeyMatcherGenerator`** - Generates the fallback/alias resolution logic:
   - `ConfigurationKeyMatcher.g.cs` - Handles key lookups with fallback chain support

## Step-by-Step Guide

### 1. Add the Configuration Key Definition

Add your new configuration key to `tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml`, specifying
an implementation string (`A` being the default one, as shown below) and specifying the product if required. Any product name
is allowed, but try to reuse the existing ones (see [Common products](#common-products)) if it makes sense, as they will create another partial class, ie
ConfigurationKeys.ProductName.cs. Without a product name, the keys will go in the main class, ConfigurationKeys.cs.

**Required fields (mandatory):**
- `implementation`: The implementation identifier
  - `A` being the default one, it needs to match the registry implementation with the same type and default values
- `type`: The type of the configuration value (for example `string`, `boolean`, `int`, `decimal`)
- `default`: The default value applied by the tracer when the env var is not set. Use `null` if there is no default.

**Optional fields:**
- `product`: Groups the key into a product-specific partial class (e.g., `OpenTelemetry`)
- `aliases`: A list of fallback environment variable names checked in order when the primary key is not found
- `const_name`: Overrides the auto-generated PascalCase constant name (useful for backward compatibility)
- `documentation`: XML documentation for the key (supports `<see>`, `<seealso>`, `<c>` tags; do **not** include `<summary>` tags)

These fields are mandatory to keep the configuration registry complete and to ensure consistent behavior and documentation across products.

**Example:**
```yaml
version: '2'
supportedConfigurations:
  DD_TRACE_SAMPLE_RATE:
  - implementation: A
    type: decimal
    default: null
    documentation: |-
      Configuration key for setting the global sampling rate.
      Value should be between 0.0 and 1.0.
  OTEL_EXPORTER_OTLP_TIMEOUT:
  - implementation: A
    type: int
    default: null
    product: OpenTelemetry
    documentation: |-
      Configuration key for the general OTLP export timeout in milliseconds.
      Default value is 10000ms.
```

This generates:
- `ConfigurationKeys.TraceSampleRate` (no product)
- `ConfigurationKeys.OpenTelemetry.ExporterOtlpTimeout` (with product)

### 2. (Optional) Add Aliases

Configuration keys can have **aliases** that are checked in order of appearance when the primary key is not found. Add them to the `aliases` property of the configuration entry in `supported-configurations.yaml`:

```yaml
supportedConfigurations:
  OTEL_EXPORTER_OTLP_LOGS_TIMEOUT:
  - implementation: A
    type: int
    default: null
    product: OpenTelemetry
    aliases:
    - OTEL_EXPORTER_OTLP_TIMEOUT
    documentation: |-
      Configuration key for the timeout in milliseconds for OTLP logs export.
      Falls back to <see cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpTimeoutMs"/> if not set.
      Default value is 10000ms (10 seconds).
      <seealso cref="Datadog.Trace.Configuration.TracerSettings.OtlpLogsTimeoutMs"/>
```

**How it works:**
1. The configuration system first looks for `OTEL_EXPORTER_OTLP_LOGS_TIMEOUT`
2. If not found, it automatically checks `OTEL_EXPORTER_OTLP_TIMEOUT` (the alias)
3. If still not found, it uses the default value

The `ConfigKeyAliasesSwitcherGenerator` source generator automatically generates the alias resolution logic from the `aliases` field. No additional code is needed - just use the primary configuration key constant and the aliases are handled transparently.

**Use cases:**
- **Specific → General fallback:** A specific key (e.g., logs timeout) falls back to a general key (e.g., overall timeout)
- **Backward compatibility:** Renamed keys can fall back to their old names to maintain compatibility
- **Hierarchical configuration:** More specific settings fall back to broader settings

### 3. (Optional) Override Constant Name

By default, the source generator automatically converts environment variable names to PascalCase constant names:
- `DD_TRACE_ENABLED` → `TraceEnabled`
- `OTEL_EXPORTER_OTLP_TIMEOUT` → `ExporterOtlpTimeout`

If you need to explicitly control the constant name (e.g., for backward compatibility), add a `const_name` field to the configuration entry in `supported-configurations.yaml`:

```yaml
supportedConfigurations:
  DD_YOUR_CUSTOM_KEY:
  - implementation: A
    type: string
    default: null
    const_name: YourPreferredConstantName
    documentation: Your documentation here.
```

**Note:** The `const_name` field exists primarily for backward compatibility with existing constant names. For new 
keys, it's recommended to let the generator automatically deduce the name from the environment variable, unless the 
result is not acceptable.

### 4. Build to Generate Code

Build the `Datadog.Trace` project to run the source generator, either using Nuke or by building the project directly from the command line or your IDE:

```bash
# From repository root
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj
```

The generator will create/update files in:
- `tracer/src/Datadog.Trace/Generated/<tfm>/Datadog.Trace.SourceGenerators/ConfigurationKeysGenerator/`

**Generated files:**
- `ConfigurationKeys.g.cs` - Main file with all keys
- `ConfigurationKeys.<Product>.g.cs` - Product-specific partial classes (if using `product` field)

### 5. Use the Generated Key

After building, you can use the generated constant in your code:

```csharp
// Without product grouping
var enabled = source.GetBool(ConfigurationKeys.TraceEnabled);

// With product grouping
var timeout = source.GetInt32(ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsTimeout);
```

**Note:** The generated constants are in the `Datadog.Trace.Configuration` namespace.

### Syntax Analyzers

The codebase includes Roslyn analyzers that enforce the use of configuration keys from the `ConfigurationKeys` classes:

#### 1. ConfigurationBuilderWithKeysAnalyzer

- **`ConfigurationBuilderWithKeysAnalyzer`** - Enforces that `ConfigurationBuilder.WithKeys()` method calls only accept string constants from `ConfigurationKeys` or `PlatformKeys` classes, not hardcoded strings or variables.

##### Diagnostic rules:
- **DD0007**: Triggers when hardcoded string literals are used instead of configuration key constants
- **DD0008**: Triggers when variables or expressions are used instead of configuration key constants

#### 2. EnvironmentGetEnvironmentVariableAnalyzer

- **`EnvironmentGetEnvironmentVariableAnalyzer`** - Enforces that `EnvironmentHelpers.GetEnvironmentVariable()` and related methods only accept constants from `ConfigurationKeys` or `PlatformKeys` classes.

##### Diagnostic rules:
- **DD0011**: Triggers when hardcoded string literals are used instead of configuration key constants
- **DD0012**: Triggers when variables or expressions are used instead of configuration key constants

#### 3. Banned API Analyzer

- **Banned API Analyzer** - Uses Microsoft's `BannedApiAnalyzers` package to prevent direct usage of `System.Environment.GetEnvironmentVariable()` throughout the codebase.

**Configuration:**
- **`BannedSymbols.txt`** (`tracer/src/Datadog.Trace.Tools.Analyzers/ConfigurationAnalyzers/BannedSymbols.txt`) - Defines banned APIs with custom error messages
- **`.editorconfig`** - Configures RS0030 diagnostic severity as error, with exceptions for vendored code and `EnvironmentConfigurationSource.cs`

##### Diagnostic rules:
- **RS0030**: Triggers when banned APIs are used (e.g., `System.Environment.GetEnvironmentVariable()`)

These analyzers help prevent typos and ensure consistency across the codebase by enforcing compile-time validation of configuration keys.

### 6. Add to Telemetry Normalization Rules

Configuration keys are reported in telemetry with normalized names. Add your key to the normalization rules:

**File:** `tracer/test/Datadog.Trace.Tests/Telemetry/config_norm_rules.json`

```json
{
  "YOUR_ENV_VAR_NAME": "normalized_telemetry_name"
}
```

**Example:**
```json
{
  "OTEL_EXPORTER_OTLP_LOGS_TIMEOUT": "otel_exporter_otlp_logs_timeout"
}
```

**Important:** The `config_norm_rules.json` file is a copy from the [dd-go repository](https://github.com/DataDog/dd-go). After updating this file locally, you must also submit a PR to update the canonical version in the dd-go repository to keep the normalization rules synchronized across all Datadog tracers.

### 7. Test Your Changes

1. **Verify generation:** Check that your key appears in the generated files
2. **Telemetry tests:** Ensure telemetry normalization tests pass in `tracer/test/Datadog.Trace.Tests/Telemetry/`
3. **Integration tests:** Test the configuration key in real scenarios where it's used
4. **Documentation:** Verify the `documentation` field renders correctly in the generated XML docs

## Configuration Key Organization

### Product Grouping

Use the `product` field to organize related keys into nested classes:

```yaml
supportedConfigurations:
  OTEL_EXPORTER_OTLP_ENDPOINT:
  - implementation: A
    type: string
    default: null
    product: OpenTelemetry
    documentation: Configuration key for the OTLP exporter endpoint.
```

Generates: `ConfigurationKeys.OpenTelemetry.ExporterOtlpEndpoint`

#### Common products

- `OpenTelemetry` - OpenTelemetry-related keys
- `CIVisibility` - CI Visibility keys
- `Telemetry` - Telemetry configuration
- `AppSec` - Application Security
- `Debugger` - Dynamic Instrumentation
- `Iast` - Interactive Application Security Testing
- `FeatureFlags` - Feature flag toggles
- `Proxy` - Proxy configuration
- `Debug` - Debug/diagnostic keys

## Examples

### Example 1: Simple Configuration Key

**supported-configurations.yaml:**
```yaml
supportedConfigurations:
  DD_TRACE_SAMPLE_RATE:
  - implementation: A
    type: decimal
    default: null
    documentation: |-
      Configuration key for setting the global sampling rate.
      Value should be between 0.0 and 1.0.
      Default value is 1.0 (100% sampling).
      <seealso cref="Datadog.Trace.Configuration.TracerSettings.GlobalSamplingRate"/>
```

**Usage:**
```csharp
var rate = source.GetDouble(ConfigurationKeys.GlobalSamplingRate);
```

### Example 2: Configuration Key with Aliases

**supported-configurations.yaml:**
```yaml
supportedConfigurations:
  OTEL_EXPORTER_OTLP_LOGS_TIMEOUT:
  - implementation: A
    type: int
    default: null
    product: OpenTelemetry
    aliases:
    - OTEL_EXPORTER_OTLP_TIMEOUT
    documentation: |-
      Configuration key for the timeout in milliseconds for OTLP logs export.
      Falls back to <see cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpTimeout"/> if not set.
      Default value is 10000ms.
      <seealso cref="Datadog.Trace.Configuration.TracerSettings.OtlpLogsTimeoutMs"/>
  OTEL_EXPORTER_OTLP_TIMEOUT:
  - implementation: A
    type: int
    default: null
    product: OpenTelemetry
    documentation: |-
      Configuration key for the general OTLP export timeout in milliseconds.
      Used as alias for specific timeout configurations.
      Default value is 10000ms.
```

**Usage:**
```csharp
// Reads OTEL_EXPORTER_OTLP_LOGS_TIMEOUT, automatically falls back to OTEL_EXPORTER_OTLP_TIMEOUT
var timeout = source.GetInt32(ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsTimeout);
```

### Example 3: Feature Flag

**supported-configurations.yaml:**
```yaml
supportedConfigurations:
  DD_TRACE_128_BIT_TRACEID_GENERATION_ENABLED:
  - implementation: A
    type: boolean
    default: 'true'
    product: FeatureFlags
    documentation: |-
      Enables generating 128-bit trace ids instead of 64-bit trace ids.
      Note that a 128-bit trace id may be received from an upstream service or from
      an Activity even if we are not generating them ourselves.
      Default value is <c>true</c> (enabled).
```

**Usage:**
```csharp
var enabled = source.GetBool(ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled);
```

## Troubleshooting

### Generated files are not updated

**Solution:** Clean and rebuild:
```bash
dotnet clean tracer/src/Datadog.Trace/Datadog.Trace.csproj
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj
```

### Key not found in generated code

**Check:**
1. YAML key matches the environment variable name exactly (case-sensitive)
2. YAML syntax is valid (proper indentation, correct field names)
3. Build succeeded without errors
4. Looking in the correct namespace/product class

### Telemetry tests failing

**Check:**
1. Added key to `config_norm_rules.json`
2. Normalized name matches `telemetry_name` in JSON
3. All tests in `tracer/test/Datadog.Trace.Tests/Telemetry/` pass

### Documentation not appearing

**Check:**
1. The `documentation` field is present in the configuration entry
2. YAML syntax is correct (proper indentation, pipe `|` or `|-` for multi-line)
3. XML tags are properly closed
4. Rebuild after YAML changes

### Aliases not working

**Check:**
1. Alias key exists as its own entry in `supportedConfigurations`
2. `aliases` list is in correct order (first alias is tried first)
3. Both `ConfigurationKeysGenerator` and `ConfigKeyAliasesSwitcherGenerator` ran successfully during build

## Related Files

- **Source generators:**
  - `tracer/src/Datadog.Trace.SourceGenerators/Configuration/ConfigurationKeysGenerator.cs` - Generates configuration key constants
  - `tracer/src/Datadog.Trace.SourceGenerators/Configuration/ConfigKeyAliasesSwitcherGenerator.cs` - Generates alias resolution logic
- **Configuration source:** `tracer/src/Datadog.Trace/Configuration/supported-configurations.yaml` - Single source of truth for all configuration keys, aliases, constant name overrides, and documentation
- **Telemetry rules:** `tracer/test/Datadog.Trace.Tests/Telemetry/config_norm_rules.json`
- **Generated output:** `tracer/src/Datadog.Trace/Generated/<tfm>/Datadog.Trace.SourceGenerators/`
