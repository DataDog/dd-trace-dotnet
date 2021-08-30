# dd-trace-dotnet/shared/src

This directory (`/shared/src`) contains contains code that is shared between the different top-level projects in this repository (.NET Tracer, .NET Profiler, etc...).

The actually shared assets are contained here. The corresponding tests, usage samples, etc are contained in sibling directories (`/shared/test`, `/shared/samples`, etc...);

## Shared assets overview

```
/shared
 |- /resources
 |- /src
     |- /managed-lib
     |- /managed-src
     |- /native-lib
     |- /native-src
     |- README.md (this file)                    
 |- ...
 ```

### `/shared/resources` (shared resources)

Non-code files shared by different projects.<br />
E.g. the public assembly signing key.

### `/shared/src/managed-lib` (shared managed libraries)

Self-contained .NET libraries referenced by different top-level projects.<br />
E.g. the Managed Loader.

Shared libraries are referenced as DLLs by other projects in this repo. Modifying code contained here may affect the runtime behavior of the components, but it will typically not affect their build. (Of course, there are exceptions,e.g. if you modify public APIs exposed by a shared library.)

### `shared/src/managed-src` (shared managed sources)

Sources that are _directly_ included into different projects and built as a part of each project they are included into.<br />
E.g. Format Utils, Logging APIs, etc...

Modifying code contained here may affect the build or each project where that code is used.

There are several reasons for including the same sources into several projects. These include sharing the implementation of non-public APIs across projects; performance (e.g. in-lining of private APIs), end other reasons.

### `/shared/src/native-lib` (shared native libraries)

Self-contained native libraries referenced by different top-level projects.<br />
E.g. spdlog.

Shared native libraries may be referenced as binaries, as sources (e.g. header files) or both. They are typically self-contained entities, likely copied from a 3rd party repo and rarely modified (or not modified at all).

### `/shared/src/native-src` (shared native sources)

Native source code files that are _directly_ included into different projects and built as a part of each project they are included into.<br />
E.g.: IL Rewriter, String Utilities, etc...

In contrast to parts of libraries that may also be included as sources (e.g. library header files) these files tend to be developed by Datadog and provide specific functionality that is shared across top-level projects in this repo.

## More info

[About parent directory and repository](../README.md).

### Copyright

Copyright (c) 2020 Datadog

[https://www.datadoghq.com](https://www.datadoghq.com/)

### License

See [license information](../../LICENSE).