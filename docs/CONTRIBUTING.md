# Contributing to `dd-trace-dotnet`

We welcome contributions of many forms to our open source project.
However, please be aware of some of the policies below, and we *strongly* recommend reaching out before starting *any* code changes.

## External Pull Request Policies

Because of security policies in place, external pull requests have the following policies:

- **Fork Required**: You **must** create pull requests from a fork of the repository. Only approved Datadog engineers have push access.
- **Limited Testing Access**: External pull requests **cannot** run our full automated test suite.
- **Merge Process**: Pull requests from forks **cannot be merged directly**. A `dd-trace-dotnet` contributor must first create a branch from your fork and run the CI suite against it. This ensures the build and test results apply to your commit. If the CI suite passes, the contributor will then merge your PR.

## Requesting Support for New Libraries

If a library is not yet supported by automatic instrumentation, please submit a feature request through our [support team][1].

If a new version of an already instrumented library is unsupported by the .NET Tracer, please submit a feature request through our [support team][1].


[1]: https://docs.datadoghq.com/help