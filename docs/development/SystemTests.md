# System Tests

> Refer to system-tests README for complete documentation https://github.com/DataDog/system-tests/blob/main/README.md

[System tests](https://github.com/DataDog/system-tests) is a black-box testing workbench for Datadog tracer libraries. It runs the same tests against every tracer implementation -- Java, Node.js, Python, PHP, Ruby, C++, .NET, Go, and Rust -- so shared features stay consistent across languages.

> TODO document how to run system-tests locally on Windows for dd-trace-dotnet

## How to run against a specific system-tests branch

### Editing ultimate-pipeline.yaml

> TODO - we could / should improve this

There is no pipeline parameter to override which system-tests branch is cloned. To test against a specific system-tests branch from the dd-trace-dotnet CI pipeline, modify the two `git clone` commands in `.azure-pipelines/ultimate-pipeline.yml`:

```yaml
# original runs against default system-tests branch
- script: git $(GIT_RETRY_CONFIG) clone --depth 1 https://github.com/DataDog/system-tests.git

# To use a specific branch on system-tests
- script: git $(GIT_RETRY_CONFIG) clone --depth 1 -b <SYSTEM_TESTS_BRANCH_HERE> https://github.com/DataDog/system-tests.git
```

There are two clone steps to update recommended to search for `system-tests.git`. **Remember to revert this change before merging your PR.**


### Building a Docker image for a PR branch (label-based)

A system-tests Docker base image can be built from a PR on dd-trace-dotnet by adding the `docker_image_artifacts` label to your PR.
This lets the system-tests repository use your branch without needing to merge into `master` on dd-trace-dotnet.

The workflow here is:

1. Add `docker_image_artifacts` label to your dd-trace-dotnet PR, this builds and pushes the Docker image
2. In your system-tests PR *title*, include `[dotnet@your-branch-name]`, this makes system-tests CI to pull that Docker image

For an example refer to these two PRs:

- https://github.com/DataDog/dd-trace-dotnet/pull/7337
- https://github.com/DataDog/system-tests/pull/5024
