# Datadog.Trace.Trimming NuGet package

This package allows users to use Datadog libraries when using trimmed apps. The only requirement is to reference this package into your project.

> Note: if you don't use automatic instrumentation this package isn't required.
> Note: Supports .NET 6+ apps only

## How does it work

`Datadog.Trace.Trimming` package guides the linker with the types used by Datatadog assemblies. It relies on [descriptors](https://github.com/dotnet/runtime/blob/main/docs/tools/illink/data-formats.md) that are used to direct the trimmer to always keep some items in the assembly, regardless of if the trimmer can find any references to them.
