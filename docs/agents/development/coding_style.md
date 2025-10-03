# Coding Style & Naming Conventions

## C# Style

- See `.editorconfig` for complete rules (4-space indent, `System.*` first, prefer `var`)
- Types/methods: PascalCase
- Locals: camelCase
- When a "using" directive is missing in a file, add it instead of using fully-qualified type names
- Use modern C# syntax, but avoid syntax that requires types not available in older runtimes (for example, don't use syntax that requires ValueTuple because is not included in .NET Framework 4.6.1)
- Prefer modern collection expressions (`[]`)

## StyleCop

See `tracer/stylecop.json`; address warnings before pushing.

## C/C++ Style

See `.clang-format`; keep consistent naming.

## Logging Guidelines

Use clear, customer-facing terminology in log messages to avoid confusion. `Profiler` is ambiguous—it can refer to the .NET profiling APIs we use internally or the Continuous Profiler product.

### Customer-facing terminology (high-level logs)

- **Datadog SDK** — When disabling the entire product or referring to the whole monitoring solution
  - Example: `"The Datadog SDK has been disabled"`
- **Instrumentation** or **Instrumentation component** — For the native tracer auto-instrumentation
  - Example: `"Instrumentation has been disabled"` or `"The Instrumentation component failed to initialize"`
- **Continuous Profiler** — Always use full name for the profiling product
  - Example: `"The Continuous Profiler has been disabled"`
- **Datadog.Trace.dll** — For the managed tracer assembly (avoid "managed profiler")
  - Example: `"Unable to initialize: Datadog.Trace.dll was not yet loaded into the App Domain"`

### Internal/technical naming (still valid)

- Native loader, Native tracer, Managed tracer loader, Managed tracer, Libdatadog, Continuous Profiler
- `CorProfiler` / `ICorProfiler` / `COR Profiler` for runtime components

### Reference

See PR 7467 for examples of consistent terminology in native logs.
