# GitLab migration notes

These notes describe the build and unit-test shadow implementation, not the broader experimental integration-test branch or the full migration roadmap.

## Scope and merge authority

- Windows x64 runners build the tracer, loader, Continuous Profiler, and tools, run the existing x64/x86 native-test targets, and run managed unit tests.
- Linux x64/ARM64 runners build the tracer, loader, Continuous Profiler, and tools for glibc/musl, including universal loader/wrapper builds. Managed unit tests run on both architectures; native test suites run on x64.
- Integration tests, macOS jobs, coverage aggregation, and migration of signing/publication are outside this shadow's scope. Existing publication jobs do not consume shadow artifacts.

`shadow-build-unit-tests` is the only optional boundary: failures inside its child pipelines propagate to that trigger through `strategy: depend`. Individual shadow jobs are not marked `allow_failure`. Azure remains authoritative.

Windows image preparation is a shared prerequisite for existing build/signing jobs as well as the shadow, so it remains blocking when needed. The image check currently gates the entire shadow, including Linux jobs; matrix generation also uses the Windows image. It is skipped when all consumers are excluded. The shadow itself is excluded for tags, release-environment deployments, and package-version runs; see the rules in [the root pipeline](../.gitlab-ci.yml).

## Why generate child-pipeline YAML?

We generate YAML to keep framework lists and selection rules in NUKE, shared with Azure. Maintaining static GitLab matrices would duplicate those lists and require separate selection rules or jobs that start only to skip themselves. That creates another place to update when supported frameworks or test-selection behavior changes.

NUKE determines the applicable matrix during a job, after the parent pipeline has been created. GitLab cannot use that job's output to expand a matrix in the same pipeline. [Dynamic child pipelines](https://docs.gitlab.com/ci/pipelines/downstream_pipelines/#dynamic-child-pipelines) let us create only the selected jobs afterward, while keeping their execution steps in static templates.

Both providers therefore consume the same NUKE selection through different mechanisms:

| Provider | How the selected matrix becomes jobs |
| --- | --- |
| Azure | `GenerateVariables` publishes JSON output variables consumed by `strategy.matrix` in the static pipeline. |
| GitLab | `GenerateGitlabUnitTestsPipelines` writes YAML artifacts that trigger child pipelines. Generated jobs extend committed, static templates. |

Ordinary changes use reduced framework lists. Mainline, relevant broad-impact changes, or `run_all_test_frameworks=true` select the full framework lists. That flag expands frameworks only; it does not enable integration tests or other test families. Keep framework and thorough-testing selection in NUKE, shared with Azure.

The generated files are **CI artifacts, not checked-in files**:

- `.gitlab/generated/windows-unit-tests.yml`
- `.gitlab/generated/linux-unit-tests.yml`
- `.gitlab/generated/linux-arm64-unit-tests.yml`

## Execution and file responsibilities

1. [build-and-unit-tests.yml](build-and-unit-tests.yml) defines producers, the matrix-generation job, and the managed-test child triggers.
2. [generate-unit-tests-pipelines.ps1](generate-unit-tests-pipelines.ps1) asks NUKE for the comparison branch, captures changed paths on the runner, then invokes [the NUKE generator](../tracer/build/_build/Build.Gitlab.UnitTests.cs) inside the Windows image. Both steps use the merge-request target when supplied, otherwise Azure's shared version-to-release-branch mapping. Git runs on the host because runner-specific Git alternates may be inaccessible inside the container. The script does not select frameworks itself.
3. Generated jobs extend [windows-unit-tests-child.yml](windows-unit-tests-child.yml) or [linux-unit-tests-child.yml](linux-unit-tests-child.yml). These templates own execution steps, runner selection, and artifact dependencies.
4. Windows and Linux tracer producers also call the existing `BuildManagedUnitTests` target before uploading `artifacts/bin` and the monitoring home. Each managed-test job downloads its corresponding producer's artifacts using `PARENT_PIPELINE_ID`, then calls only `RunManagedUnitTests --RunManagedUnitTestsFromArtifacts true --framework <TFM>`. This opt-in mode passes the prebuilt test DLL and its adapter directory directly to VSTest, bypassing MSBuild test invocation, package restore, and product/test compilation. NUKE still queries project metadata to select the expected test assemblies. A missing expected test DLL fails the job. Azure and ordinary local runs retain the existing project-based execution path.
5. Linux image checks, producers and managed-test jobs use [linux-ci-images.sh](linux-ci-images.sh) to resolve image references and export SDK versions directly from their same-commit checkout. There is no metadata job or environment-file artifact; `LINUX_CI_IMAGE_BASE` overrides are explicitly forwarded through the shadow and Linux test triggers.
6. `check-linux-ci-images-x64` and `check-linux-ci-images-arm64` check registry manifests using the publication inventory. Each gates only its architecture's producers (and consequently their test children), without gating Windows or the other architecture. These checks never build or push images; consumer pulls still fail visibly if availability changes afterward.

Producers build the test projects' declared frameworks once; the generated matrix still selects which frameworks execute. Test jobs consume the complete output folders, including runtime dependencies, adapters, and the Datadog logger/collector. They do not need producer `obj` trees or the NuGet package cache. The same-commit checkout is still required for test fixtures, snapshots, and the NUKE runner. Check producer duration and artifact size in CI: compiling tests has moved into producers, not disappeared.

Forward required variables explicitly across nested triggers: variables inherited from the root are not automatically forwarded again. In particular, the Windows test trigger forwards `WINDOWS_BUILD_IMAGE_BASE`.

Generator containers must also receive `GITLAB_CI` for NUKE to detect the GitLab host. `CI_JOB_ID` alone is insufficient: without host detection, NUKE treats execution as local and selects all frameworks.

## Images, SDKs, and shared setup

- Ubuntu and Alpine environments use the SDK from `global.json`; only universal environments use `nativeBuildDotnetSdkVersion` from [Azure's pipeline](../.azure-pipelines/ultimate-pipeline.yml), without a duplicate GitLab pin. Ubuntu builds use the shared glibc 2.17 sysroot for native compatibility. Each consumer resolves both SDK versions. NUKE source and restore inputs do not invalidate environment tags: startup restores/builds the candidate's current code, using the image's package cache only as an optimization.
- The Windows SDK remains pinned in its image definition and must be kept compatible with `global.json`. The image also enables `LongPathsEnabled` and uses a pinned Microsoft `dotnet-install.ps1` revision for the additional x64 runtime channels listed in [the Dockerfile](../tracer/build/_build/docker/gitlab/gitlab.windows.dockerfile). Patch versions resolve only during publication, as on Linux, and the resulting runtime inventory is checked and logged; there is no separate runtime URL/checksum manifest. Test jobs do not install runtimes or edit this registry setting. The runner's Git long-path checkout hook remains separate.
- Windows images use content-addressed tags with automatic availability checks and manual publication after review; see [UPDATING_IMAGE.md](../tracer/build/_build/docker/gitlab/UPDATING_IMAGE.md) for the workflow and recovery steps.
- Windows jobs share [run-windows-container.ps1](run-windows-container.ps1) for image lookup, the checkout mount, common environment and a default 20 GB limit. Signing identity and test credentials are explicit caller options. The existing signed build retains its container name/memory monitoring and artifact commands; NuGet signing retains its unspecified memory limit.
- Linux builds and unit tests share the Docker CLI image and BuildKit setting in [linux-build-common.yml](linux-build-common.yml). Environment publication uses manual architecture/toolchain-family matrix jobs so large Docker toolchains do not accumulate in one job; ordinary producers and tests only pull content-addressed image tags. See [UPDATING_LINUX_IMAGES.md](UPDATING_LINUX_IMAGES.md) for inputs, inventory and recovery steps.
- Linux producers and managed tests use [run-linux-container.sh](run-linux-container.sh) for the checkout mount and common environment. Producers delegate through [run-nuke-linux.sh](run-nuke-linux.sh); test jobs explicitly forward logger/STS variables, and both call sites explicitly request `SYS_PTRACE`. The launcher does not forward the full host environment or grant extra capabilities by default.
- Containers compile the current checkout's NUKE code through [run-nuke-linux-container.sh](run-nuke-linux-container.sh); the images cache its dependencies, not its compiled program. The shared Azure `builder`/`tester` targets remain unchanged. Within this shadow, only managed-test containers receive CI Visibility credentials.
- During manual publication, [linux-unit-tests.dockerfile](linux-unit-tests.dockerfile) adds Datadog CA certificates using a pinned multi-architecture digest. The trust-store layer and existing test runtimes are consumed as part of the prepared image, not rebuilt per framework. Preserve x64/ARM64 support when updating them.

## Results and validation

Managed-test jobs explicitly invoke `ConfigureGitlabCiVisibility` and `RunManagedUnitTests` in the same NUKE process. The shared setup target in [Build.Gitlab.UnitTests.cs](../tracer/build/_build/Build.Gitlab.UnitTests.cs) obtains a temporary API key through `dd-sts` when none is configured, with a 60-second HTTP timeout and fatal missing/invalid credentials. Normal Azure/local test targets do not depend on this setup. The OIDC token is available during NUKE bootstrap; setup removes `DD_STS_OIDC_TOKEN` from the environment before its request and again in `finally`, so test processes inherit only the API key. This does not erase credentials from process memory or dumps. Never log or persist the returned credentials.

Windows shadow producers and matrix generation retain CI Identity tokens on the runner for [registry authentication](https://datadoghq.atlassian.net/wiki/spaces/SECENG/pages/4956889069), but do not forward them into their containers, which do not need an AWS role.

Results, logs, and dumps are retained on failure. Native XML is published as GitLab JUnit reports. Linux profiler producers must collect both `profiler/build_data/tests` and `artifacts/build_data/tests`: native-loader results are written to the latter. Managed TRX files remain downloadable artifacts, not native GitLab test reports; managed test telemetry is sent to CI Visibility.

Managed-test templates set `DD_TEST_LOG_ROUTINE_EVENTS=false` so the custom test framework omits STARTED/SUCCESS diagnostics at their source. Failure, skip, retry and hang diagnostics remain enabled; no console-output filter is applied. Azure and local runs keep routine logging by default.

A green ordinary run validates only its selected matrix. Before treating coverage as equivalent to Azure, check generated job lists, full-framework executions, actual test counts, retained results, and CI Visibility attribution. Local syntax checks do not validate runner access, image publication, nested artifact downloads, or credential exchange.
