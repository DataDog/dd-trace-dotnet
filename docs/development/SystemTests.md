# System Tests

> Refer to system-tests README for complete documentation https://github.com/DataDog/system-tests/blob/main/README.md

[System tests](https://github.com/DataDog/system-tests) is a black-box testing workbench for Datadog tracer libraries. It runs the same tests against every tracer implementation -- Java, Node.js, Python, PHP, Ruby, C++, .NET, Go, and Rust -- so shared features stay consistent across languages.

> TODO document how to run system-tests locally on Windows for dd-trace-dotnet

## How to run against a specific system-tests branch

The `system_tests` stage clones `DataDog/system-tests` at the branch or tag name in the
`system_tests_branch` queue-time variable, falling back to `main` when it isn't set. To run
against a system-tests feature branch:

1. Navigate to the [consolidated-pipeline](https://dev.azure.com/datadoghq/dd-trace-dotnet/_build?definitionId=54)
2. Click `Run Pipeline`, and select your dd-trace-dotnet branch
3. Click `Variables`, and set `system_tests_branch` to the system-tests branch or tag name
4. Select `Stages To Run`, and select `build_linux`, `package_linux` and `system_tests` to avoid using excessive resources

`system_tests_branch` accepts a branch or tag name, not a commit SHA. No source edit is
needed, and there is nothing to revert before merging.


### Building a Docker image for a PR branch (label-based)

A system-tests Docker base image can be built from a PR on dd-trace-dotnet by adding the `docker_image_artifacts` label to your PR.
This lets the system-tests repository use your branch without needing to merge into `master` on dd-trace-dotnet.

The workflow here is:

1. Add `docker_image_artifacts` label to your dd-trace-dotnet PR, this builds and pushes the Docker image
2. In your system-tests PR *title*, include `[dotnet@your-branch-name]`, this makes system-tests CI to pull that Docker image
3. The PR's (dev) tests will run against the image created, whereas (prod) runs against master without your changes.

For an example refer to these two PRs:

- https://github.com/DataDog/dd-trace-dotnet/pull/7337
- https://github.com/DataDog/system-tests/pull/5024
