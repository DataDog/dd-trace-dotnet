# dd-trace-dotnet/shared/src/managed-src

This directory (`/shared/src/managed-src`) contains sources that are directly included into different projects and built as a part of each project they are included into.

Modifying code contained here may affect the build or each project where that code is used.

There are several reasons for including the same sources into several projects. These include sharing the implementation of non-public APIs across projects; performance (e.g. in-lining of private APIs), end other reasons.

The subdirectories here carry the name of the respective namespaces.
E.g. all classes in a namespace `Datadog.AutoInstrumentation.Util` must be
placed in a subdirectory with the same name.

## More info

[More about other shared assets](../README.md#shared-assets-overview).

### Copyright

Copyright (c) 2020 Datadog

[https://www.datadoghq.com](https://www.datadoghq.com/)

### License

See [license information](../../../LICENSE).
