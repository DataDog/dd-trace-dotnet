# dd-autoinstrumentation CLI — LLM Reference

> This document is a complete reference for AI agents using the `dd-autoinstrumentation` CLI to generate CallTarget auto-instrumentation code. It covers all commands, options, JSON schemas, error codes, and recommended workflows.

## Invocation

```bash
# From repo root
dotnet run --project tracer/src/Datadog.AutoInstrumentation.Generator.Cli/ --framework net10.0 -- <command> [options]

# Or, if installed as a global tool
dd-autoinstrumentation <command> [options]
```

Always pass `--json` for structured output. `--json` is a global flag and can appear anywhere on the command line.

## Commands

### `inspect` — Discover types and methods

#### List types

```bash
dd-autoinstrumentation inspect <assembly-path> --list-types --json
```

Response:
```json
{
  "success": true,
  "command": "inspect",
  "data": {
    "types": [
      {
        "fullName": "MyLib.HttpClient",
        "namespace": "MyLib",
        "name": "HttpClient",
        "isPublic": true,
        "isInterface": false,
        "isAbstract": false,
        "isSealed": false,
        "isValueType": false,
        "methodCount": 12,
        "nestedTypes": 0
      }
    ]
  }
}
```

Notes:
- Includes ALL access levels (not just public) — CallTarget hooks any visibility
- Excludes compiler-generated types (e.g., `<>c__DisplayClass`, async state machines)
- Excludes the `<Module>` type

#### List methods

```bash
dd-autoinstrumentation inspect <assembly-path> --list-methods <type-full-name> --json
```

Response:
```json
{
  "success": true,
  "command": "inspect",
  "data": {
    "type": "MyLib.HttpClient",
    "methods": [
      {
        "name": "SendAsync",
        "fullName": "System.Threading.Tasks.Task`1<MyLib.Response> MyLib.HttpClient::SendAsync(MyLib.Request,System.Threading.CancellationToken)",
        "returnType": "System.Threading.Tasks.Task`1<MyLib.Response>",
        "isPublic": true,
        "isStatic": false,
        "isVirtual": true,
        "isAsync": true,
        "parameters": [
          { "name": "request", "type": "MyLib.Request", "index": 0 },
          { "name": "cancellationToken", "type": "System.Threading.CancellationToken", "index": 1 }
        ],
        "overloadIndex": 0,
        "overloadCount": 2
      }
    ]
  }
}
```

Notes:
- Excludes compiler-generated methods
- Includes constructors (`.ctor`), property accessors (`get_*`, `set_*`), event accessors, and operators — CallTarget can instrument all of these
- `overloadIndex` and `overloadCount` tell you upfront if disambiguation is needed
- `isAsync` is true when the return type is exactly `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>` (not `TaskScheduler`, `TaskCompletionSource`, etc.)

### `generate` — Generate instrumentation code

```bash
dd-autoinstrumentation generate <assembly-path> \
  --type <type-full-name> \
  --method <method-name> \
  [--overload-index <n>] \
  [--parameter-types <type1> <type2> ...] \
  [--json]
```

Required arguments: `assembly-path`, `--type` (`-t`), `--method` (`-m`).

#### Overload disambiguation

If a method has multiple overloads, you must provide one of:
- `--overload-index <n>` — 0-based index (matches `overloadIndex` from inspect output)
- `--parameter-types <type1> <type2>` — full type names of each parameter

#### Generation flags

| Flag | Effect |
|---|---|
| `--no-method-begin` | Skip `OnMethodBegin` handler |
| `--no-method-end` | Skip `OnMethodEnd` handler |
| `--no-auto-detect` | Disable smart defaults |

Async methods are auto-detected: when the inspect output shows `isAsync: true`, the generator emits `OnAsyncMethodEnd` (and skips `OnMethodEnd`) without any extra flag. To force `OnAsyncMethodEnd` on a non-async method, use `--set createOnAsyncMethodEnd=true`.

#### Configuration overrides

```bash
--set key=value          # Repeatable, e.g., --set createDucktypeInstance=true
--config '{"key": val}'  # Inline JSON configuration object
--config-file path.json  # Load from file
```

`--config` and `--config-file` are mutually exclusive. Both are overridden by `--set`.

#### List available keys

```bash
dd-autoinstrumentation generate --list-keys --json
```

Response:
```json
{
  "success": true,
  "command": "generate",
  "data": {
    "configurationKeys": [
      { "key": "createDucktypeInstance", "type": "Boolean", "defaultValue": "False" },
      { "key": "createOnMethodBegin", "type": "Boolean", "defaultValue": "True" }
    ]
  }
}
```

#### Success response

```json
{
  "success": true,
  "fileName": "HttpClientSendAsyncIntegration",
  "sourceCode": "// generated C# source code...",
  "metadata": {
    "assemblyName": "MyLib",
    "typeName": "MyLib.HttpClient",
    "methodName": "SendAsync",
    "returnTypeName": "...",
    "parameterTypeNames": "...",
    "minimumVersion": "1.0.0",
    "maximumVersion": "1.*.*",
    "integrationName": "nameof(IntegrationId.MyLib)",
    "integrationClassName": "HttpClientSendAsyncIntegration",
    "isInterface": false
  },
  "configuration": {
    "createOnMethodBegin": true,
    "createOnAsyncMethodEnd": true
  }
}
```

The `sourceCode` field contains the complete, ready-to-save C# file.

#### Output to file

```bash
dd-autoinstrumentation generate ... --output path/to/Integration.cs
```

Creates directories as needed. Prints confirmation to stdout.

## Error Handling

When `--json` is active, all errors are returned as structured JSON on stdout (not stderr):

```json
{
  "success": false,
  "command": "generate",
  "errorCode": "AMBIGUOUS_OVERLOAD",
  "errorMessage": "human-readable description",
  "data": { }
}
```

### Error codes and recovery

| Error Code | Meaning | Recovery |
|---|---|---|
| `FILE_NOT_FOUND` | Assembly path doesn't exist | Fix the path |
| `TYPE_NOT_FOUND` | Type not in assembly | Use `inspect --list-types` to find correct name |
| `METHOD_NOT_FOUND` | Method not on type (0 overloads) | Use `inspect --list-methods` to find correct name |
| `AMBIGUOUS_OVERLOAD` | Multiple overloads, no disambiguation | Read `data.overloads`, pick one, retry with `--overload-index` |
| `INVALID_ARGUMENT` | Missing required args | Add missing `assembly-path`, `--type`, or `--method` |
| `INVALID_CONFIG` | Bad JSON in `--config` or `--config-file` | Fix JSON syntax |
| `UNKNOWN_KEY` | Bad key in `--set` | Use `--list-keys` to see valid keys |
| `GENERATION_ERROR` | Code generation failed | Check error message for details |
| `BAD_ASSEMBLY` | File is not a valid .NET assembly | Verify the file is a .NET DLL (not native, not corrupted) |

### AMBIGUOUS_OVERLOAD detail

This is the most common error. The response includes the overload list so you can immediately retry:

```json
{
  "success": false,
  "command": "generate",
  "errorCode": "AMBIGUOUS_OVERLOAD",
  "errorMessage": "Error: Could not resolve method 'Send' on type 'MyLib.HttpClient'. Found 2 overload(s):...",
  "data": {
    "overloads": [
      {
        "index": 0,
        "fullName": "System.Void MyLib.HttpClient::Send(System.String)",
        "parameters": [{ "name": "url", "type": "System.String" }]
      },
      {
        "index": 1,
        "fullName": "System.Void MyLib.HttpClient::Send(System.String,System.Int32)",
        "parameters": [
          { "name": "url", "type": "System.String" },
          { "name": "timeout", "type": "System.Int32" }
        ]
      }
    ]
  }
}
```

Retry: `dd-autoinstrumentation generate ... --overload-index 0 --json`

## Recommended LLM Workflow

### Step 1 — Find the assembly

Locate the target library's DLL. Common locations:
- NuGet cache: `~/.nuget/packages/<name>/<version>/lib/<tfm>/<name>.dll`
- Build output: `bin/Debug/<tfm>/`

### Step 2 — Discover types

```bash
dd-autoinstrumentation inspect <dll> --list-types --json
```

Filter by `isPublic`, `isInterface`, `methodCount` to find interesting types.

### Step 3 — Discover methods

```bash
dd-autoinstrumentation inspect <dll> --list-methods <type> --json
```

Look at `isAsync`, `isVirtual`, `overloadCount` to inform generation choices.

### Step 4 — Generate

```bash
dd-autoinstrumentation generate <dll> -t <type> -m <method> --json [--overload-index <n>]
```

- If `overloadCount > 1` in step 3, pass `--overload-index` immediately
- Parse `success` — if false, check `errorCode` and handle accordingly

### Step 5 — Save the output

Extract `sourceCode` from the JSON response and write to the correct path:
```
tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/<Area>/<IntegrationClassName>.cs
```

The `metadata.integrationClassName` field provides the suggested file name.

### Error recovery loop

```
if errorCode == "AMBIGUOUS_OVERLOAD":
    pick overload from data.overloads
    retry with --overload-index

if errorCode == "TYPE_NOT_FOUND":
    run inspect --list-types, pick correct type, retry

if errorCode == "METHOD_NOT_FOUND":
    run inspect --list-methods, pick correct method, retry
```

## Configuration Precedence

Applied in order (highest wins):

1. **Auto-detect** from method signature (unless `--no-auto-detect`)
2. **Base config** from `--config-file` or `--config` (replaces layer 1)
3. **Shortcut flags** (`--no-method-begin`, `--no-method-end`)
4. **`--set` overrides** (applied last, highest priority)
