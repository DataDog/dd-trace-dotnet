# System Tests

> Refer to system-tests README for complete documentation https://github.com/DataDog/system-tests/blob/main/README.md

[System tests](https://github.com/DataDog/system-tests) is a black-box testing workbench for Datadog tracer libraries. It runs the same tests against every tracer implementation -- Java, Node.js, Python, PHP, Ruby, C++, .NET, Go, and Rust -- so shared features stay consistent across languages.

## Building the tracer locally for system-tests

To run system-tests locally, you first need to build the tracer package (`datadog-dotnet-apm-*.tar.gz`).

### Linux

```bash
./tracer/build.sh BuildTracerHomeWithoutProfiler
```

The artifact will be at `tracer/bin/artifacts/linux-<arch>/datadog-dotnet-apm-<version>.tar.gz`.

### macOS (via Docker)

macOS requires Docker to cross-compile for Linux. See the [Building Linux packages from macOS](../../tracer/README.md#building-linux-packages-from-macos) section in the tracer README for Docker setup and build instructions.

### Using the artifact with system-tests

Copy the tar.gz into your system-tests binaries directory:

```bash
cp tracer/bin/artifacts/linux-*/datadog-dotnet-apm-*.tar.gz /path/to/system-tests/binaries/dotnet/
```

Then follow the [system-tests README](https://github.com/DataDog/system-tests/blob/main/README.md) to run the tests.

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
3. The PR's (dev) tests will run against the image created, whereas (prod) runs against master without your changes.

For an example refer to these two PRs:

- https://github.com/DataDog/dd-trace-dotnet/pull/7337
- https://github.com/DataDog/system-tests/pull/5024
