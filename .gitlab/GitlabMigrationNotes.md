# GitLab migration notes

These notes describe the build and unit-test shadow implementation, not the broader experimental integration-test branch or the full migration roadmap.

## Scope and merge authority

- Windows x64 runners build the tracer, loader, Continuous Profiler, and tools, run the existing x64/x86 native-test targets, and run managed unit tests.
- Linux x64/ARM64 producers cover glibc/musl, including universal loader/wrapper builds. Managed unit tests run on both architectures; native suites run on x64.
- Integration tests, macOS jobs, coverage aggregation, and migration of signing/publication are outside this shadow's scope. Existing publication jobs do not consume shadow artifacts.

`shadow-build-unit-tests` is the only optional boundary: failures inside its child pipelines propagate to that trigger through `strategy: depend`. Individual shadow jobs are not marked `allow_failure`. Azure remains authoritative.

Windows image preparation is a shared prerequisite for existing build/signing jobs as well as the shadow, so it remains blocking when needed. It is skipped when all consumers are excluded. The shadow itself is excluded for tags, release-environment deployments, and package-version runs; see the rules in [the root pipeline](../.gitlab-ci.yml).

## Why generate child-pipeline YAML?

Both providers use NUKE to choose frameworks at runtime; they consume that selection differently:

| Provider | How the selected matrix becomes jobs |
| --- | --- |
| Azure | `GenerateVariables` publishes JSON output variables consumed by `strategy.matrix` in the static pipeline. |
| GitLab | `GenerateGitlabUnitTestsPipelines` writes YAML artifacts that trigger child pipelines. Generated jobs extend committed, static templates. |

GitLab's matrix configuration is resolved when a pipeline is created, rather than from the output of a preceding job in that pipeline. [Dynamic child pipelines](https://docs.gitlab.com/ci/pipelines/downstream_pipelines/#dynamic-child-pipelines) let NUKE select the jobs without duplicating its selection rules in GitLab YAML.

Ordinary changes use reduced framework lists. Mainline, relevant broad-impact changes, or `run_all_test_frameworks=true` select the full framework lists. That flag expands frameworks only; it does not enable integration tests or other test families. Keep framework and thorough-testing selection in NUKE, shared with Azure.

The generated files are **CI artifacts, not checked-in files**:

- `.gitlab/generated/windows-unit-tests.yml`
- `.gitlab/generated/linux-unit-tests.yml`
- `.gitlab/generated/linux-arm64-unit-tests.yml`

## Execution and file responsibilities

1. [build-unit-tests.yml](build-unit-tests.yml) defines producers, the matrix-generation job, and the managed-test child triggers.
2. [generate-unit-tests-pipelines.ps1](generate-unit-tests-pipelines.ps1) asks NUKE for the comparison branch, captures changed paths on the runner, then invokes [the NUKE generator](../tracer/build/_build/Build.Gitlab.UnitTests.cs) inside the Windows image. Both steps use the merge-request target when supplied, otherwise Azure's shared version-to-release-branch mapping. Git runs on the host because runner-specific Git alternates may be inaccessible inside the container. The script does not select frameworks itself.
3. Generated jobs extend [windows-unit-tests-child.yml](windows-unit-tests-child.yml) or [linux-unit-tests-child.yml](linux-unit-tests-child.yml). These templates own execution steps, runner selection, and artifact dependencies.
4. Each managed-test job downloads artifacts from its corresponding producer in the same shadow pipeline, using `PARENT_PIPELINE_ID`, then restores packages and builds tests with `--UnitTestFrameworkOnly true --framework <TFM>`. This mode is opt-in on every provider and can also be used locally with producer artifacts.

Forward required variables explicitly across nested triggers: variables inherited from the root are not automatically forwarded again. In particular, the Windows test trigger forwards `WINDOWS_BUILD_IMAGE_BASE`.

## Images, SDKs, and shared setup

- Linux managed builds, validation images, and unit-test images read the SDK from `global.json` through [get-dotnet-sdk-version.sh](get-dotnet-sdk-version.sh). CentOS and universal builds intentionally retain their older native-toolchain SDK pin; do not replace it with the managed SDK automatically.
- The Windows SDK remains pinned in its image definition and must be kept compatible with `global.json`. [install-windows-test-runtime.ps1](install-windows-test-runtime.ps1) supplies older runtimes needed by individual test jobs.
- Windows images use content-addressed tags, resolved and validated by [get-windows-build-image.ps1](get-windows-build-image.ps1). An automatic availability check starts immediately in the `build` stage; image consumers depend on it explicitly. Missing images require review and manual `build-windows-ci-image` execution, followed by retrying the check and affected consumers. The manual step provides a review checkpoint before branch-controlled image builds write shared registry tags; existing images need no approval. Publication is serialized across pipelines; manual execution is not a registry permission boundary. Follow [UPDATING_IMAGE.md](../tracer/build/_build/docker/gitlab/UPDATING_IMAGE.md) for updates and retries.
- Linux builds and unit tests share the Docker CLI image and BuildKit setting in [linux-build-common.yml](linux-build-common.yml), and build SDK images through [build-linux-image.sh](build-linux-image.sh). The legacy native SDK pin is defined once in `build-unit-tests.yml`, separately from the managed SDK in `global.json`.
- Linux tracer, profiler, and universal producers share [run-nuke-linux.sh](run-nuke-linux.sh). Universal jobs share a YAML template and do not forward CI Visibility credentials.
- [linux-unit-tests.dockerfile](linux-unit-tests.dockerfile) adds Datadog CA certificates using a pinned multi-architecture image digest. Preserve both x64 and ARM64 support when updating it.

## Results and validation

Managed-test jobs opt into `--RequireGitlabCiVisibility true`, independently of framework selection. Windows and Linux forward the OIDC token into the test container; `ConfigureGitlabUnitTestCredentials` obtains a temporary Datadog API key through `dd-sts` when no key is already configured. It clears `DD_STS_OIDC_TOKEN` before test processes start, including when a key is preconfigured or the exchange fails. This prevents subsequent processes from inheriting the token, but is not a guarantee that memory or dumps contain no credentials. Credential-exchange failures fail the job; never log or persist the returned credentials.

Windows shadow producers and matrix generation retain CI Identity tokens on the runner for [registry authentication](https://datadoghq.atlassian.net/wiki/spaces/SECENG/pages/4956889069), but do not forward them into their containers, which do not need an AWS role.

Results, logs, and dumps are retained on failure. Native XML is published as GitLab JUnit reports. Linux profiler producers must collect both `profiler/build_data/tests` and `artifacts/build_data/tests`: native-loader results are written to the latter. Managed TRX files remain downloadable artifacts, not native GitLab test reports; managed test telemetry is sent to CI Visibility.

A green ordinary run validates only its selected matrix. Before treating coverage as equivalent to Azure, check generated job lists, full-framework executions, actual test counts, retained results, and CI Visibility attribution. Local syntax checks do not validate runner access, image publication, nested artifact downloads, or credential exchange.
