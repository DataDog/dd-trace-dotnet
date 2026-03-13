## Instrumentation Generator <!-- omit in toc -->

The Instrumentation Generator produces CallTarget auto-instrumentation boilerplate code from .NET assemblies. It loads an assembly via [dnlib](https://github.com/yck1509/dnlib), inspects types and methods, and generates integration classes with `[InstrumentMethod]` attributes, `OnMethodBegin`/`OnMethodEnd` handlers, and DuckType proxy definitions.

The tooling is split into three projects:

| Project | Purpose |
|---|---|
| `Datadog.AutoInstrumentation.Generator.Core` | Shared library with all generation logic (no UI dependencies) |
| `Datadog.AutoInstrumentation.Generator` | Avalonia GUI for interactive browsing and generation |
| `Datadog.AutoInstrumentation.Generator.Cli` | CLI tool for scriptable/automated generation |

- [GUI Tool](#gui-tool)
  - [Running the GUI](#running-the-gui)
  - [GUI Workflow](#gui-workflow)
- [CLI Tool](#cli-tool)
  - [Running the CLI](#running-the-cli)
  - [Basic Usage](#basic-usage)
  - [Using Nuke](#using-nuke)
  - [Method Resolution](#method-resolution)
  - [Generation Flags](#generation-flags)
  - [Configuration Overrides](#configuration-overrides)
  - [File-Based Configuration](#file-based-configuration)
  - [Discovering Available Keys](#discovering-available-keys)
  - [Output Options](#output-options)
  - [Auto-Detection](#auto-detection)
  - [JSON Output](#json-output)
  - [Configuration Precedence](#configuration-precedence)
- [Two-Tool Workflow with dotnet-inspect](#two-tool-workflow-with-dotnet-inspect)
- [Architecture](#architecture)
  - [Core Library](#core-library)
  - [Generation Configuration](#generation-configuration)

### GUI Tool

The GUI provides an interactive tree view for browsing assemblies and a live code preview that updates as you toggle options.

#### Running the GUI

```bash
# Via Nuke
./tracer/build.ps1 RunInstrumentationGenerator    # Windows
./tracer/build.sh RunInstrumentationGenerator      # Linux/macOS

# Via dotnet directly
dotnet run --project tracer/src/Datadog.AutoInstrumentation.Generator/ --framework net10.0
```

#### GUI Workflow

1. Click the file icon to open an assembly (`.dll`)
2. Browse the tree: Assembly > Module > Namespace > Type > Method
3. Select a method to see generated code in the editor
4. Toggle options (duck typing, async handlers, etc.) in the sidebar
5. Copy the generated code to your integration file

### CLI Tool

The CLI exposes the same generation logic as a command-line tool, suitable for scripting, CI, and AI-assisted workflows.

#### Running the CLI

```bash
# Via dotnet run
dotnet run --project tracer/src/Datadog.AutoInstrumentation.Generator.Cli/ --framework net10.0 -- \
  generate <assembly-path> --type <type> --method <method> [options]

# Or install as a global tool
dotnet pack tracer/src/Datadog.AutoInstrumentation.Generator.Cli/
dotnet tool install --global --add-source ./tracer/src/Datadog.AutoInstrumentation.Generator.Cli/bin/Debug dd-autoinstrumentation
dd-autoinstrumentation generate <assembly-path> --type <type> --method <method> [options]
```

#### Basic Usage

Generate an integration for a specific method and write it to the correct location:

```bash
dd-autoinstrumentation generate path/to/MyLib.dll \
  --type MyLib.MyClass \
  --method DoSomething \
  --output tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MyLib/MyClassDoSomethingIntegration.cs
```

The tool prints the generated source code to stdout by default, or writes to a file with `--output`.

#### Using Nuke

The CLI is integrated into the Nuke build system:

```bash
# Windows
.\tracer\build.cmd RunInstrumentationGeneratorCli ^
  --assembly-path "path/to/MyLib.dll" ^
  --type-name "MyLib.MyClass" ^
  --method-name "DoSomething" ^
  --output-path "src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MyLib/MyClassDoSomethingIntegration.cs"

# Linux/macOS
./tracer/build.sh RunInstrumentationGeneratorCli \
  --assembly-path "path/to/MyLib.dll" \
  --type-name "MyLib.MyClass" \
  --method-name "DoSomething" \
  --output-path "src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MyLib/MyClassDoSomethingIntegration.cs"
```

Overload disambiguation is available as direct Nuke parameters:

```bash
.\tracer\build.cmd RunInstrumentationGeneratorCli ^
  --assembly-path "path/to/MyLib.dll" ^
  --type-name "MyLib.MyClass" ^
  --method-name "DoSomething" ^
  --overload-index 0
```

Additional CLI flags (--set, --config-file, JSON output, etc.) can be passed via `--generator-args` as a single space-separated string:

```bash
.\tracer\build.cmd RunInstrumentationGeneratorCli ^
  --assembly-path "path/to/MyLib.dll" ^
  --type-name "MyLib.MyClass" ^
  --method-name "DoSomething" ^
  --generator-args "--set createDucktypeInstance=true --json"
```

Nuke parameters:

| Parameter | Required | Description |
|---|---|---|
| `--assembly-path` | Yes | Path to the .NET assembly (.dll) |
| `--type-name` | Yes | Fully qualified type name |
| `--method-name` | Yes | Method name to instrument |
| `--output-path` | No | Output file path (prints to stdout if omitted) |
| `--overload-index` | No | 0-based overload index for method disambiguation |
| `--parameter-types` | No | Parameter type full names (space-separated) for disambiguation |
| `--generator-args` | No | Additional flags passed through to the CLI (space-separated string) |

#### Method Resolution

When a type has multiple overloads of the same method, use one of these to disambiguate:

```bash
# By parameter type signatures
dd-autoinstrumentation generate lib.dll \
  -t MyLib.MyClass -m Send \
  --parameter-types System.String System.Int32

# By 0-based overload index
dd-autoinstrumentation generate lib.dll \
  -t MyLib.MyClass -m Send \
  --overload-index 1
```

If disambiguation is needed but not provided, the tool lists available overloads with their signatures.

#### Generation Flags

Three shortcut flags are available for the most common toggles:

| Flag | Description |
|---|---|
| `--no-method-begin` | Skip `OnMethodBegin` handler |
| `--no-method-end` | Skip `OnMethodEnd` handler |
| `--async-method-end` | Generate `OnAsyncMethodEnd` handler |
| `--no-auto-detect` | Disable smart defaults (see [Auto-Detection](#auto-detection)) |

#### Configuration Overrides

For fine-grained control over all generation options, use `--set key=value`. This replaces the previous 25+ individual duck typing flags with a single, repeatable mechanism:

```bash
# Enable instance duck typing with methods
dd-autoinstrumentation generate lib.dll \
  -t MyLib.MyClass -m DoWork \
  --set createDucktypeInstance=true --set ducktypeInstanceMethods=true

# Disable what auto-detect enabled
dd-autoinstrumentation generate lib.dll \
  -t MyLib.MyClass -m DoWork \
  --set createOnAsyncMethodEnd=false --set createOnMethodEnd=true

# Use DuckCopy structs
dd-autoinstrumentation generate lib.dll \
  -t MyLib.MyClass -m DoWork \
  --set useDuckCopyStruct=true --set createDucktypeInstance=true
```

Keys use camelCase names matching the JSON output from `--json`. Use `--list-keys` to see all available keys.

#### File-Based Configuration

Use `--config-file` to load configuration from a JSON file. This is useful for reusable templates or for round-tripping with `--json` output:

```bash
# Save configuration to a file
dd-autoinstrumentation generate lib.dll -t MyLib.MyClass -m DoWork --json > config.json

# Re-run with saved config, optionally with tweaks
dd-autoinstrumentation generate lib.dll -t MyLib.MyClass -m DoWork \
  --config-file config.json --set ducktypeInstanceMethods=true
```

The `--config-file` option accepts either:
- A bare configuration object (just the boolean flags)
- A full `--json` output envelope (the tool auto-extracts the `configuration` block)

For inline JSON (useful for AI agents), use `--config`:

```bash
dd-autoinstrumentation generate lib.dll -t MyLib.MyClass -m DoWork \
  --config '{"createDucktypeInstance": true, "ducktypeInstanceProperties": true}'
```

Note: `--config` and `--config-file` are mutually exclusive.

#### Discovering Available Keys

Use `--list-keys` to see all available configuration keys with their types and default values:

```bash
dd-autoinstrumentation generate --list-keys
```

This prints a table of all keys that can be used with `--set`, `--config`, or `--config-file`.

#### Output Options

| Flag | Description |
|---|---|
| `--json` | Output structured JSON (see [JSON Output](#json-output)) |
| `-o, --output <path>` | Write to file instead of stdout |

#### Auto-Detection

By default, the CLI applies the same smart defaults as the GUI:

- **Async methods** (returning `Task` or `ValueTask`): generates `OnAsyncMethodEnd` instead of `OnMethodEnd`
- **Static methods**: disables instance duck typing
- **Void methods**: uses the void variant of `OnMethodEnd`

Use `--no-auto-detect` to start from a blank configuration and control everything explicitly.

#### JSON Output

The `--json` flag produces structured output for machine consumption:

```json
{
  "success": true,
  "fileName": "MyClassDoSomething",
  "sourceCode": "// <copyright ...\n...",
  "metadata": {
    "assemblyName": "MyLib",
    "typeName": "MyLib.MyClass",
    "methodName": "DoSomething",
    "returnTypeName": "ClrNames.Void",
    "parameterTypeNames": "[ClrNames.String, ClrNames.Int32]",
    "minimumVersion": "1.0.0",
    "maximumVersion": "1.*.*",
    "integrationName": "nameof(IntegrationId.MyLib)",
    "integrationClassName": "MyClassDoSomething",
    "isInterface": false
  },
  "configuration": {
    "createOnMethodBegin": true,
    "createOnMethodEnd": true,
    "createOnAsyncMethodEnd": false
  }
}
```

#### Configuration Precedence

Configuration is applied in layers (lowest to highest precedence):

1. **Auto-detect** (`CreateForMethod`) — unless `--no-auto-detect`
2. **Base config** — `--config-file` OR `--config` (mutually exclusive; replaces layer 1 entirely)
3. **Shortcut flags + `--set` overrides** — applied on top of the base config

### Two-Tool Workflow with dotnet-inspect

The CLI is designed to pair with [`dotnet-inspect`](https://github.com/richlander/dotnet-inspect) for a complete workflow. `dotnet-inspect` handles assembly/package browsing (finding the right type and method), while `dd-autoinstrumentation` handles code generation.

```bash
# 1. Browse the target library to find the method
dotnet-inspect member MyClass --package MyLib@2.0.0 --oneline

# 2. Find the local DLL path
dotnet-inspect library --package MyLib@2.0.0

# 3. Generate the instrumentation
dd-autoinstrumentation generate ~/.nuget/packages/mylib/2.0.0/lib/net6.0/MyLib.dll \
  --type MyLib.MyClass \
  --method DoSomething \
  --output tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MyLib/MyClassDoSomethingIntegration.cs
```

### Architecture

#### Core Library

`Datadog.AutoInstrumentation.Generator.Core` contains all generation logic with zero UI dependencies:

| Type | Purpose |
|---|---|
| `InstrumentationGenerator` | Stateless engine: takes a `MethodDef` + `GenerationConfiguration`, returns a `GenerationResult` |
| `GenerationConfiguration` | POCO with all boolean flags controlling generation |
| `GenerationResult` | Output: source code, file name, namespace, structured metadata |
| `InstrumentMethodMetadata` | Structured `[InstrumentMethod]` attribute values |
| `AssemblyBrowser` | Thin dnlib wrapper for loading assemblies and resolving methods |
| `EditorHelper` | Type name conversion, duck type proxy generation, version extraction |
| `ResourceLoader` | Loads embedded code templates (`Integration.cs`, `OnMethodBegin.cs`, etc.) |

Both the GUI and CLI depend on Core. The GUI adds Avalonia UI and ReactiveUI for the interactive experience. The CLI adds System.CommandLine for argument parsing.

#### Generation Configuration

`GenerationConfiguration.CreateForMethod(methodDef)` applies the same smart defaults as the GUI:

```csharp
// Auto-detects async, static, void and sets appropriate defaults
var config = GenerationConfiguration.CreateForMethod(methodDef);

// Or start from scratch
var config = new GenerationConfiguration
{
    CreateOnMethodBegin = true,
    CreateOnAsyncMethodEnd = true,
    CreateDucktypeInstance = true,
    DucktypeInstanceProperties = true,
};

var generator = new InstrumentationGenerator();
var result = generator.Generate(methodDef, config);
```
