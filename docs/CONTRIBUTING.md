# Contributing to `dd-trace-dotnet`

We welcome contributions of many forms to our open source project.
However, please be aware of some of the policies below, and we *strongly* recommend reaching out before starting *any* code changes.

## External Pull Request Policies

Because of security policies in place, external pull requests have the following policies:

- **Fork Required**: You **must** create pull requests from a fork of the repository.
- **Limited Testing Access**: External pull requests (from forks) **cannot** run our CI.
- **Merge Process**: A Datadog engineer will review your contribution and request changes as necessary. After review, the engineer will mirror your commits into a Datadog-managed branch and create a separate **mirrored** pull request for the necessary CI checks and reviews.
    - Each commit is cherry-picked and signed individually, the original author and contents are preserved; however, the commit SHAs will differ.
    - After the mirrored pull request passes our CI and gets the necessary reviews, it will be merged and your original pull request will be closed.
- **Follow-up Changes**: Push any follow-up changes to your original branch. A Datadog engineer will review your updates and synchronize them with the mirrored pull request.

## Mirroring a Community Pull Request (Datadog Engineers)

After reviewing a contribution, run [the mirroring script](../tooling/mirror-community-pull-request.sh) from the repository root of a clean local checkout with the PR number.

## Requesting Support for New Libraries

If a library is not yet supported by automatic instrumentation, please submit a feature request through our [support team][1].

If a new version of an already instrumented library is unsupported by the .NET Tracer, please submit a feature request through our [support team][1].


[1]: https://docs.datadoghq.com/help
