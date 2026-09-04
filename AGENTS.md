# Repository Instructions

- Do not manually edit generated files (including files with `.g.` in the name); follow the file header's regeneration instructions.
- The tracer supports older .NET runtimes. Avoid APIs and types unavailable on supported target frameworks.
- The tracer runs inside customer processes. Treat startup and hot-path changes as performance-sensitive, and avoid unnecessary allocations.
- In customer-facing logs, avoid the ambiguous term "Profiler." Use the specific component name, such as "Instrumentation," "Continuous Profiler," "Datadog SDK," or `Datadog.Trace.dll`.
