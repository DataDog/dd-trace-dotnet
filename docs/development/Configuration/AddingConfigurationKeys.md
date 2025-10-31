# Adding New Configuration Keys

This guide explains how to add new configuration keys to the .NET Tracer. Configuration keys are automatically generated from JSON and YAML source files using source generators.

## Table of Contents

- [Overview](#overview)
- [Step-by-Step Guide](#step-by-step-guide)
  - [1. Add the Configuration Key Definition](#1-add-the-configuration-key-definition)
  - [2. Add Documentation](#2-add-documentation)
  - [3. (Optional) Add Fallback Keys](#3-optional-add-fallback-keys-aliases)
  - [4. (Optional) Override Constant Name](#4-optional-override-constant-name)
  - [5. Build to Generate Code](#5-build-to-generate-code)
  - [6. Use the Generated Key](#6-use-the-generated-key)
  - [7. Add to Telemetry Normalization Rules](#7-add-to-telemetry-normalization-rules)
  - [8. Test Your Changes](#8-test-your-changes)
- [Configuration Key Organization](#configuration-key-organization)
- [Examples](#examples)
- [Troubleshooting](#troubleshooting)

## Overview

Configuration keys in the .NET Tracer are defined in two source files:

- **`tracer/src/Datadog.Trace/Configuration/supported-configurations.json`** - Defines the configuration keys, their environment variable names, and fallback chains
- **`tracer/src/Datadog.Trace/Configuration/supported-configurations-docs.yaml`** - Contains XML documentation for each key

Two source generators read these files at build time:

1. **`ConfigurationKeysGenerator`** - Generates the configuration key constants:
   - `ConfigurationKeys.g.cs` - Main configuration keys class with all constants
   - `ConfigurationKeys.<Product>.g.cs` - Product-specific partial classes (e.g., `ConfigurationKeys.OpenTelemetry.g.cs`)

2. **`ConfigurationKeyMatcherGenerator`** - Generates the fallback/alias resolution logic:
   - `ConfigurationKeyMatcher.g.cs` - Handles key lookups with fallback chain support

## Step-by-Step Guide

### 1. Add the Configuration Key Definition

Add your new configuration key to `tracer/src/Datadog.Trace/Configuration/supported-configurations.json`.

**Example:**
```json
{
  "supportedConfigurations": {
    "DD_TRACE_SAMPLE_RATE": {
      "version": ["A"]
    },
    "OTEL_EXPORTER_OTLP_TIMEOUT": {
      "version": ["A"],
      "product": "OpenTelemetry"
    }
  }
}
```

This generates:
- `ConfigurationKeys.TraceSampleRate` (no product)
- `ConfigurationKeys.OpenTelemetry.ExporterOtlpTimeout` (with product)

### 2. Add Documentation

Add XML documentation for your key in `tracer/src/Datadog.Trace/Configuration/supported-configurations-docs.yaml`.

**Format:**
```yaml
OTEL_EXPORTER_OTLP_LOGS_TIMEOUT: |
  Configuration key for the timeout in milliseconds for OTLP logs export.
  Falls back to <see cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpTimeoutMs"/> if not set.
  Default value is 10000ms (10 seconds).
  <seealso cref="Datadog.Trace.Configuration.TracerSettings.OtlpLogsTimeoutMs"/>
```

**Important Notes:**
- The YAML key must **exactly match** the JSON key (environment variable name)
- **Do NOT include `<summary>` tags** - the source generator automatically wraps your documentation in `<summary>` tags
- You can include `<seealso>` and `<see>` tags directly in the content - the source generator will extract `<seealso>` tags and place them outside the `<summary>` section as needed

### 3. (Optional) Add Fallback Keys (Aliases)

Configuration keys can have **fallback keys** (also called aliases) that are checked in order of appearance when the primary key is not found. Add them to the `fallbacks` section in `supported-configurations.json`:

```json
{
  "fallbacks": {
    "OTEL_EXPORTER_OTLP_LOGS_TIMEOUT": [
      "OTEL_EXPORTER_OTLP_TIMEOUT"
    ]
  }
}
```

**How it works:**
1. The configuration system first looks for `OTEL_EXPORTER_OTLP_LOGS_TIMEOUT`
2. If not found, it checks `OTEL_EXPORTER_OTLP_TIMEOUT` (the fallback)
3. If still not found, it uses the default value

**Use cases:**
- **Specific → General fallback:** A specific key (e.g., logs timeout) falls back to a general key (e.g., overall timeout)
- **Backward compatibility:** Renamed keys can fall back to their old names to maintain compatibility
- **Hierarchical configuration:** More specific settings fall back to broader settings

### 4. (Optional) Override Constant Name

By default, the source generator automatically converts environment variable names to PascalCase constant names:
- `DD_TRACE_ENABLED` → `TraceEnabled`
- `OTEL_EXPORTER_OTLP_TIMEOUT` → `ExporterOtlpTimeout`

If you need to explicitly control the constant name (e.g., for backward compatibility), add an entry to `tracer/src/Datadog.Trace/Configuration/configuration_keys_mapping.json`:

```json
{
  "DD_YOUR_CUSTOM_KEY": "YourPreferredConstantName"
}
```

**Note:** This mapping file exists primarily for backward compatibility with existing constant names. For new keys, it's recommended to let the generator automatically deduce the name from the environment variable.

### 5. Build to Generate Code

Build the `Datadog.Trace` project to run the source generator:

```bash
# From repository root
dotnet build tracer/src/Datadog.Trace/Datadog.Trace.csproj
```

The generator will create/update files in:
- `tracer/src/Datadog.Trace/Generated/<tfm>/Datadog.Trace.SourceGenerators/ConfigurationKeysGenerator/`

**Generated files:**
- `ConfigurationKeys.g.cs` - Main file with all keys
- `ConfigurationKeys.<Product>.g.cs` - Product-specific partial classes (if using `product` field)

### 6. Use the Generated Key

After building, you can use the generated constant in your code:

```csharp
// Without product grouping
var enabled = source.GetBool(ConfigurationKeys.TraceEnabled);

// With product grouping
var timeout = source.GetInt32(ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsTimeout);
```

**Note:** The generated constants are in the `Datadog.Trace.Configuration` namespace.

### 7. Add to Telemetry Normalization Rules

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

### 8. Test Your Changes

1. **Verify generation:** Check that your key appears in the generated files
2. **Telemetry tests:** Ensure telemetry normalization tests pass in `tracer/test/Datadog.Trace.Tests/Telemetry/`
3. **Integration tests:** Test the configuration key in real scenarios where it's used

## Configuration Key Organization

### Product Grouping

Use the `product` field to organize related keys into nested classes:

```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": {
    "product": "OpenTelemetry"
  }
}
```

Generates: `ConfigurationKeys.OpenTelemetry.ExporterOtlpEndpoint`

**Common products:**
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

**supported-configurations.json:**
```json
{
  "supportedConfigurations": {
    "DD_TRACE_SAMPLE_RATE": {
      "version": ["A"]
    }
  }
}
```

**supported-configurations-docs.yaml:**
```yaml
DD_TRACE_SAMPLE_RATE: |
  Configuration key for setting the global sampling rate.
  Value should be between 0.0 and 1.0.
  Default value is 1.0 (100% sampling).
  <seealso cref="Datadog.Trace.Configuration.TracerSettings.GlobalSamplingRate"/>
```

**Usage:**
```csharp
var rate = source.GetDouble(ConfigurationKeys.GlobalSamplingRate);
```

### Example 2: Configuration Key with Fallback

**supported-configurations.json:**
```json
{
  "supportedConfigurations": {
    "OTEL_EXPORTER_OTLP_LOGS_TIMEOUT": {
      "version": ["A"],
      "product": "OpenTelemetry"
    },
    "OTEL_EXPORTER_OTLP_TIMEOUT": {
      "version": ["A"],
      "product": "OpenTelemetry"
    }
  },
  "fallbacks": {
    "OTEL_EXPORTER_OTLP_LOGS_TIMEOUT": [
      "OTEL_EXPORTER_OTLP_TIMEOUT"
    ]
  }
}
```

**supported-configurations-docs.yaml:**
```yaml
OTEL_EXPORTER_OTLP_LOGS_TIMEOUT: |
  Configuration key for the timeout in milliseconds for OTLP logs export.
  Falls back to <see cref="ConfigurationKeys.OpenTelemetry.ExporterOtlpTimeoutMs"/> if not set.
  Default value is 10000ms.
  <seealso cref="Datadog.Trace.Configuration.TracerSettings.OtlpLogsTimeoutMs"/>

OTEL_EXPORTER_OTLP_TIMEOUT: |
  Configuration key for the general OTLP export timeout in milliseconds.
  Used as fallback for specific timeout configurations.
  Default value is 10000ms.
```

**Usage:**
```csharp
// Reads OTEL_EXPORTER_OTLP_LOGS_TIMEOUT, falls back to OTEL_EXPORTER_OTLP_TIMEOUT
var timeout = source.GetInt32(
    ConfigurationKeys.OpenTelemetry.ExporterOtlpLogsTimeout,
    ConfigurationKeys.OpenTelemetry.ExporterOtlpTimeout);
```

### Example 3: Feature Flag

**supported-configurations.json:**
```json
{
  "supportedConfigurations": {
    "DD_TRACE_128_BIT_TRACEID_GENERATION_ENABLED": {
      "version": ["A"],
      "product": "FeatureFlags"
    }
  }
}
```

**supported-configurations-docs.yaml:**
```yaml
DD_TRACE_128_BIT_TRACEID_GENERATION_ENABLED: |
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
1. JSON key matches YAML key exactly (case-sensitive)
2. JSON is valid (no trailing commas, proper escaping)
3. Build succeeded without errors
4. Looking in the correct namespace/product class

### Telemetry tests failing

**Check:**
1. Added key to `config_norm_rules.json`
2. Normalized name matches `telemetry_name` in JSON
3. All tests in `tracer/test/Datadog.Trace.Tests/Telemetry/` pass

### Documentation not appearing

**Check:**
1. YAML key exactly matches JSON key
2. YAML syntax is correct (proper indentation, pipe `|` for multi-line)
3. XML tags are properly closed
4. Rebuild after YAML changes

### Fallback not working

**Check:**
1. Fallback key is defined in `configurations` section
2. Fallback array is in correct order (first fallback is tried first)
3. Using the correct overload of `GetXxx()` method that accepts fallback keys

## Related Files

- **Source generator:** `tracer/src/Datadog.Trace.SourceGenerators/Configuration/ConfigurationKeysGenerator.cs`
- **Configuration source:** `tracer/src/Datadog.Trace/Configuration/supported-configurations.json`
- **Documentation source:** `tracer/src/Datadog.Trace/Configuration/supported-configurations-docs.yaml`
- **Telemetry rules:** `tracer/test/Datadog.Trace.Tests/Telemetry/config_norm_rules.json`
- **Generated output:** `tracer/src/Datadog.Trace/Generated/<tfm>/Datadog.Trace.SourceGenerators/ConfigurationKeysGenerator/`
