# GitLab CI migration plan and findings

Last updated: 2026-08-11

## Purpose

Migrate `dd-trace-dotnet` CI from Azure DevOps to GitLab without rewriting the NUKE build system. The migration is additive: Azure DevOps remains unchanged and continues to run during a long parallel-validation period. Duplicated work is acceptable until GitLab demonstrates equivalent builds, tests, artifacts, and reliability.

This document combines:

- Guidance received from the CI Infrastructure team.
- The proposed phased migration plan.
- Completed Windows GitLab performance and log-quality work.
- Findings from the Windows runner-image and vcpkg investigations.
- The remaining implementation and validation work.

## Help and documentation

### Internal documentation

- [Windows Runners V2](https://datadoghq.atlassian.net/wiki/spaces/DEVX/pages/5091983382/Windows+Runners+V2)
- [Create custom macOS Runner](https://datadoghq.atlassian.net/wiki/spaces/DEVX/pages/3457515540/Create+custom+macOS+Runner)
- [Getting Started with macOS Runners](https://datadoghq.atlassian.net/wiki/spaces/DEVX/pages/3557065191/Getting+Started+with+macOS+Runners)
- [Onboarding To GitLab CI](https://datadoghq.atlassian.net/wiki/spaces/DEVX/pages/2411528655/Onboarding+To+Gitlab+CI)
- [GitLab User Documentation](https://datadoghq.atlassian.net/wiki/spaces/DEVX/pages/2411561449/Gitlab+User+Documentation)
- [Using CI Identities](https://datadoghq.atlassian.net/wiki/spaces/SECENG/pages/5324145720/Using+CI+Identities)
- [GitLab CI/CD YAML reference](https://docs.gitlab.com/ci/yaml/)
- [GitLab CI/CD variables](https://docs.gitlab.com/ci/variables/)
- [CI job-sizing dashboard](https://app.datadoghq.com/dashboard/vev-54z-7wr/?fromUser=false&refresh_mode=sliding&from_ts=1776091263874&to_ts=1777300863874&live=true)

## Current state

CI is split across Azure DevOps and GitLab.

- `.azure-pipelines/ultimate-pipeline.yml` contains the full build and test matrix. As of this update, it is 5,693 lines with 80 top-level stages.
- Azure DevOps covers Windows, Linux x64 and ARM64, glibc and musl, macOS, unit tests, integration tests, smoke tests, profiler tests, packaging, and publishing.
- GitLab currently runs the Windows build and native unit tests in one producer job, followed by a NUKE-generated Windows managed-unit-test child pipeline, packaging/publishing work, and benchmarks.
- Existing GitLab jobs import Azure artifacts through `.gitlab/download-single-step-artifacts.sh` and `.gitlab/download-serverless-artifacts.sh`.
- The proposed `.gitlab/ci/` Phase 1 files do not exist yet.

The long-term goal is to host the entire build and test pipeline in GitLab, remove the cross-CI artifact handoff, and retire Azure DevOps only after an extended period of demonstrated parity.

## Runner guidance

| Workload | Runner tags | Characteristics and intended use |
| --- | --- | --- |
| Linux without a Docker daemon | `arch:amd64`, `arch:arm64` | Kubernetes pod runners. A job may specify its image, sidecars, CPU, and memory. Docker images can be built through the remote BuildKit executor. |
| Linux requiring Docker | `docker-in-docker:amd64`, `docker-in-docker:arm64` | Kubernetes microVM runners with a Docker daemon. Use for Testcontainers, Docker Compose, `kind`, and similar workloads. |
| Windows | `windows-v2:2019`, `windows-v2:2022`, `windows-v2:2025` | Shared, persistent runners. Jobs should use Docker images to isolate dependencies and state. |
| Shared macOS | `macos:sonoma-arm64`, `macos:sonoma-amd64` | Shared native macOS runners available across repositories. |
| Dedicated macOS | Team-specific | Available when a specialized configuration is required; follow the custom-runner documentation. |

### CI Infrastructure recommendations

- Avoid full clones, especially in large repositories.
- Keep the number of jobs within reason because concurrent capacity is shared across repositories.
- Public repositories must not depend on private repositories.
- Use the job-sizing dashboard to select appropriate CPU and memory for Kubernetes jobs.
- On Windows, pre-pull frequently used images into the runner AMI.
- Structure Dockerfiles so frequently changing layers appear late and do not invalidate stable, expensive layers.

## Migration principles

- Keep Azure DevOps running during the migration.
- Add GitLab jobs without changing existing Azure behavior.
- Reuse NUKE targets as the unit of work.
- Do not rewrite build logic in YAML.
- Keep platform-specific setup in shared templates.
- Use shallow fetches where possible.
- Build and test platforms in parallel.
- Publish artifacts from each platform build and consume them through explicit `needs`.
- Gate PoC jobs with a reversible variable.
- Measure correctness, duration, queueing, artifact size, and runner utilization before expanding the matrix.

## Completed foundation work

### Windows compiler parallelism and memory

[PR #8925](https://github.com/DataDog/dd-trace-dotnet/pull/8925) is merged.

It:

- Enables MSVC multiprocessor compilation.
- Caps `CL_MPCount` at the runner's 16 logical processors.
- Raises the Windows build-container limit from 10 GB to 20 GB.
- Logs runner RAM and logical processors.
- Measures peak container memory during the build.

The runner reported 31.2 GB total RAM and 26.6 GB free before the build. Peak usage with `/MP16` was approximately 9.1 GiB, leaving reasonable headroom within the 20 GB limit.

Measured results:

| Configuration | NUKE | Native compilation | End-to-end | Peak container memory |
| --- | ---: | ---: | ---: | ---: |
| Baseline, MP disabled / 10 GB | 31:45 | 24:52 | ~40 min | Not measured |
| MP2 / 10 GB | 22:27 | 14:40 | ~29–31 min | Not measured |
| MP3 / 10 GB | Not measured | Not measured | ~24–28 min | Not measured |
| MP4 / 16 GB | 15:00–15:54 | Not measured | ~17–23 min | Not measured |
| MP8 / 16 GB | 13:50 | 6:01 | 23:27 | Not measured |
| MP8 / 20 GB | 13:11 | 5:44 | 21:31 | 8.686 GiB |
| MP16 / 20 GB | 11:37 | 4:37 | 19:05–19:54 | 9.105 GiB |

Native compilation detail:

| Configuration | Tracer | Loader | Profiler |
| --- | ---: | ---: | ---: |
| Baseline | 8:38 | 2:46 | 13:28 |
| MP2 / 10 GB | 5:15 | 1:40 | 7:45 |
| MP8 / 16 GB | 2:21 | 0:42 | 2:58 |
| MP8 / 20 GB | 2:13 | 0:41 | 2:50 |
| MP16 / 20 GB | 1:50 | 0:33 | 2:14 |

The optimized job is approximately 50% faster end to end than the original baseline. Run-to-run variance remains significant and was dominated by Docker image pulling.

### CI log cleanup

[PR #8948](https://github.com/DataDog/dd-trace-dotnet/pull/8948) is merged.

It:

- Treats vendored spdlog/fmt headers as MSVC external headers.
- Uses `ExternalWarningLevel=TurnOffAllWarnings`, which generates `/external:W0`.
- Preserves warnings from Datadog and PPDB sources.
- Keeps external-header settings in the relevant `.vcxproj` files.
- Filters routine NuGet restore progress in CI while preserving warnings and errors.
- Keeps verbose NuGet output locally.
- Enables process output before attaching the NUKE custom logger.
- Applies the filter for `IsServerBuild || IsGitlab`, because the Windows Docker invocation does not expose all variables NUKE normally uses to detect GitLab.

Measured log reduction:

| Metric | Original | Intermediate | Latest |
| --- | ---: | ---: | ---: |
| Lines | 17,699 | 7,894 | 3,585 |
| Size | 2.55 MiB | 1.21 MiB | 0.46 MiB |
| Targeted NuGet messages | 5,475 | 4,308 | 0 |
| Compiler warnings | 1,036 | 116 | 116 |
| Errors | 0 | 0 | 0 |

The final log is approximately 80% shorter and is easier to inspect when later migration work fails.

### Content-addressed Windows build image

The Windows image is stored at:

```text
registry.ddbuild.io/ci/dd-trace-dotnet/dd-trace-dotnet-docker-build
```

`tracer/build/_build/docker/gitlab/compute-image-hash.ps1`:

1. Lists each file directly under `tracer/build/_build/docker/gitlab`, sorted by name.
2. Computes the SHA-256 of each file.
3. Concatenates those hashes.
4. Hashes the concatenated value.
5. Uses the first 12 lowercase characters as the image tag.

The current `master` checkout produces `814a0509e85a`.

The normal GitLab build verifies that the exact hash-tagged image exists. If it does not, the job fails with instructions to run the manual `build-windows-ci-image` job. That job builds and pushes both the content-addressed tag and `:latest`. Consumers always use the content-addressed tag.

This deliberately prevents a Dockerfile change from silently running against an old image.

### Windows native unit-test migration

The Windows native unit tests have now been added to GitLab and have completed successfully. They run in the existing `build` job immediately after the production build, reusing the same checkout and native compilation state. This matches Azure's build-then-test pattern without transferring intermediate objects between runners.

The build job:

- Uses `windows-v2:2022` and the content-addressed Windows build image.
- Passes `--NugetPackageDirectory c:\mnt\packages`. Without this argument, restoring an individual profiler test `.vcxproj` fails because NuGet cannot infer a packages or solution directory.
- Passes the same repository-mounted package directory to the build, test, and packaging invocations. Each invocation uses a short-lived Docker container, so packages restored to a container's default user profile would otherwise disappear before the next phase.
- Retains the 20 GB container limit and `/MP16` settings.
- Compiles and runs the tracer, native loader, and profiler native test suites.
- Runs packaging and signing targets only after the native tests pass.
- Publishes tracer/loader results from `artifacts/build_data/tests` and profiler results from `profiler/build_data/tests` through GitLab's JUnit report support, including when the job fails.
- Gates the later `publish` stage as part of the build job itself.

The first separated run took approximately 12 minutes. Its original packaging-artifact download took only about 3.5 seconds and did not contain reusable native compilation state. NUKE spent 1:10 restoring and 9:31 compiling/running the native suites; 8:38 of that was compilation. The log confirmed that the profiler, loader, and tracer production projects were rebuilt through project references. This motivated transferring the selective native build-state artifact described above.

A first attempt used a commit-scoped GitLab cache. Creating it added approximately 2:03 to the build, and the runner reported that no shared-cache URL was configured, so the cache remained local to one EC2 runner. It was removed because a downstream test job is not guaranteed to use the same runner. Regular GitLab artifacts provide the required cross-runner guarantee.

The first run with the selective native build-state artifact transferred 631 files with an uncompressed size of 4.68 GB. Downloading and extracting it took approximately 65 seconds. Reusing the production outputs reduced the native-test NUKE invocation from 9:31 to 7:53 and the complete job from approximately 11:59 to 11:13. The largest improvement was in the profiler test compilation, which fell from 5:29 to 3:59. The net improvement for this single consumer was therefore only about 46 seconds, but the same build state can later be reused by Windows unit, integration, and packaging jobs.

Azure provides the closest equivalent through `build-windows-working-directory`. The Windows tracer build publishes the entire working directory, and numerous downstream jobs restore it into `$(System.DefaultWorkingDirectory)`. A measured Azure artifact contained 27,222 files and was shown as 4,483 MB in the artifact UI; a downstream download reported 4,724.1 MB of total content. Azure transferred 2,548.5 MB physically, saved 2,119.1 MB through compression, and reused 56.5 MB from the runner's local cache.

Azure's upload uses chunk-level deduplication. For the measured run, it processed 3,893,529,926 source bytes and reported 7,781.7 MB of deduplicated content, but uploaded only 0.5 MB physically because nearly all chunks already existed in Azure's artifact store. This makes repeated Azure uploads exceptionally cheap, but does not eliminate the multi-gigabyte download on a downstream runner. GitLab's regular job artifact provides reliable cross-runner handoff but does not provide equivalent deduplicated-upload behavior.

The large artifact is CI build state, not a release deliverable. Azure separately publishes the curated `windows-tracer-home` (98 MB), `windows-profiler-home` (15 MB), profiler symbols, and later ZIP, MSI, and NuGet artifacts. GitLab should preserve the same distinction: retain the native bin/object state only as short-lived input for downstream CI jobs, while keeping final monitoring-home, symbols, installer, and package outputs as the artifacts used by release workflows.

Based on these measurements, the separate `test-native-windows` job and its selective 4.68 GB build-state artifact were removed. A single native-test consumer gained only about 46 seconds after paying the transfer cost, while Azure already avoids this handoff by running native tests in its producing build jobs. GitLab now does the same. The measurements remain useful when deciding what build state future managed, integration, or packaging jobs actually require, but native bin/object trees are no longer published solely for native tests.

The first complete run of the final build → test → package topology succeeded. Directing all three short-lived containers to `c:\mnt\packages` fixed the prior `NETSDK1064` failure: the test compilation resolved `System.Collections` 4.3.0 and the other managed dependencies from the shared workspace. The timing breakdown was:

| Phase | NUKE duration |
| --- | ---: |
| Production build | 10:34 |
| Native tests | 6:26 |
| Packaging and signing | 2:15 |
| Total NUKE work | 19:15 |

The complete GitLab job took approximately 35:32. Pulling the hash-tagged Windows image took about 13:49, from the start of the automatic pull until Docker reported the newer image downloaded. This confirms that image availability, rather than build/test/package execution, remains the dominant avoidable cost. The build container peaked at 8.628 GiB, 43.1% of its 20 GB limit. Artifact publication remained small and fast: GitLab found 135 release artifact entries, four tracer/loader XML reports, and two profiler XML reports; both archive and JUnit uploads succeeded.

The first Windows managed-test PoC ran `net8.0` and `net48` sequentially in the existing build job. Both frameworks completed successfully: `net8.0` reported 20,429 passed and 57 skipped tests, while `net48` reported 19,222 passed and 90 skipped tests. The complete job took approximately 59 minutes; the two managed invocations contributed 15:51 and 15:33 respectively. The resulting 2.28 MB log remained below GitLab's 4 MiB limit.

The PoC exposed a Windows-container difference for .NET Framework. Coverage-backfill tests produced temporary paths longer than `MAX_PATH`; `net8.0` passed, while `net48` initially failed with `DirectoryNotFoundException`. Enabling `LongPathsEnabled` inside the managed-test container fixed all 52 failures. The setting is currently applied before the existing Docker entrypoint runs and should later be baked into the Windows build image.

The validated two-framework loop has now been replaced by the Azure-shaped architecture: the `build` job remains the producer and runs native tests inline, while a parallel `generate-unit-tests-pipelines` job invokes NUKE's focused `GenerateGitlabWindowsUnitTestsPipeline` target and publishes only the generated child configuration. A separate artifact is required because GitLab limits a dynamic child-pipeline artifact archive to 5 MiB, while the build artifact is approximately 316 MiB. Both Azure and GitLab therefore use the framework definitions in `GetTestingFrameworks(PlatformFamily.Windows)` as the source of truth. The focused target accepts the parent's thorough-testing decision and does not require Git inside the Windows build image. Normal merge requests generate jobs for `net48`, `netcoreapp3.1`, `net9.0`, and `net10.0`; mainline runs, explicitly forced runs, integration changes, and large snapshot changes generate all nine frameworks from `net48` through `net10.0`. The parent `unit-tests-windows` trigger waits for both producer jobs and uses `strategy: depend`; generated child jobs retrieve the successful parent `build` artifact with `needs:pipeline:job` and the quoted parent pipeline ID.

The first end-to-end run of the generated child pipeline completed successfully. The normal merge-request matrix created four parallel jobs—`net48`, `netcoreapp3.1`, `net9.0`, and `net10.0`—and all four passed. This validates the small configuration-artifact handoff, the parent-to-child build-artifact download, and the reduced Windows framework selection. A later pipeline with `run_all_test_frameworks=true` also completed successfully, validating all nine Windows framework jobs and the expanded Linux matrices.

The next migration slice added a `build-linux-tracer-x64` producer on the `docker-in-docker:amd64` runner. The name follows Azure's `build_linux_tracer` stage and its `x64` matrix cell; x64 implies glibc here, while musl carries an explicit suffix. It mirrors Azure's three-part build: `Clean CompileManagedLoader` in the Debian .NET 10 builder, `BuildNativeTracerHome CompileTracerNativeTests RunTracerNativeTests` in the CentOS 7 .NET 7 builder, and `BuildManagedTracerHome ExtractDebugInfoLinux ValidateNativeTracerGlibcCompatibility` back in Debian. The job publishes a selective artifact for managed-unit-test consumers instead of Azure's entire working directory: monitoring home, managed binaries and reference assemblies, native symbols, and test diagnostics.

The same build template now creates `build-linux-tracer-x64-musl`, matching Azure's Alpine matrix cell. Because both its managed and native phases use the Alpine .NET 10 builder, the job builds that image once and reuses it for all three sequential NUKE invocations. It produces an independent musl monitoring home, managed build state, native symbols, and tracer native-test results. Profiler, native-loader/native-wrapper tests, and packaging remain outside this tracer producer.

The first attempt used `docker.io/library/docker:27-cli` as the job image and was rejected while Kubernetes prepared the pod: the `third-party-registry` admission policy allows only approved registries. The Java tracer provides the applicable precedent: Docker-requiring jobs use `docker-in-docker:amd64`, and its image-building job uses the approved `486234852809.dkr.ecr.us-east-1.amazonaws.com/docker:27.3.1` image. The Linux producer now uses the same pinned ECR image. Java otherwise runs builds and tests directly in prebuilt GHCR builder images, while libdatadog uses approved ECR/`registry.ddbuild.io` images or delegates its internal build; this reinforces the planned follow-up to publish content-addressed .NET Linux builder images instead of rebuilding Debian and CentOS on every pipeline.

The first complete Linux producer run was green in approximately 16:56. Repository and runner preparation took about 0:58, the Debian and CentOS builder images took about 2:25 and 4:31 respectively, the three NUKE phases took 0:11, 3:59, and 4:26, and artifact publication took about 0:19. All 76 tracer native tests passed, glibc compatibility validation and debug extraction succeeded, and both the selective archive and JUnit report uploaded successfully. As a short-term optimization, build the independent Debian and CentOS Docker images concurrently while keeping all NUKE invocations sequential because they share the mounted workspace and artifact directories. This could save up to roughly 2:25 on a cold run, subject to CPU, network, and Docker-daemon contention. Prebuilt content-addressed builder images remain the preferred long-term solution.

The Linux integration-test prerequisites are now implemented as independent GitLab build producers. `build-linux-profiler-x64` mirrors Azure's CentOS 7 native build plus Debian compatibility-validation phases, while `build-linux-profiler-x64-musl` builds and validates in Alpine. Both run the profiler, native-wrapper, and native-loader native suites and retain the profiler monitoring home, native symbols, XML test results, logs, and dumps. `build-linux-universal-x64` uses the existing universal builder to produce and validate the native loader/wrapper outputs consumed by both x64 libc variants.

Sample artifacts also mirror Azure's producer boundary. `build-samples-standalone` publishes `artifacts/bin`, while the ten-cell `build-samples-multi-version` matrix publishes `artifacts/publish` independently for `net48`, `netcoreapp2.1`, `netcoreapp3.0`, `netcoreapp3.1`, and .NET 5 through .NET 10. These jobs use the content-addressed Windows build image because Azure also builds the cross-platform sample artifacts on Windows. The prefilled `perform_comprehensive_testing` variable controls whether minor dependency-package versions are included. The first standalone run exposed an ambient `Platform=x64` inherited from the Visual Studio environment, while the generated samples solution only defines `Any CPU`; `CompileSamples` now explicitly selects the solution's exact `Any CPU` platform, while individual project builds continue to use `AnyCPU`. The two profiler cells, universal producer, standalone samples, and ten multi-version sample cells still require a complete green GitLab validation run.

The first integration-test consumer is intentionally one static PoC cell: `integration-tests-linux-x64-net10`. It downloads the glibc tracer and profiler outputs, copies the universal loader/wrapper into the `linux-x64` monitoring home, and consumes the standalone and `net10.0` multi-version sample artifacts. It then mirrors Azure's non-Docker `Test` job by building `BuildIntegrationTests` and `CompileTrimmingSamples` with `IncludeTestsRequiringDocker=false`, and running the `IntegrationTests` Compose service with `--no-deps` and `Area=Tracer`. The job still uses the Docker-in-Docker runner because the test process executes inside the standard tester container, but it does not start database, broker, or other dependency containers. The build container receives `CI_JOB_ID`, which is NUKE's GitLab marker and enables the on-demand restores required when build and test artifacts originate in separate jobs. This includes restoring the instrumentation samples built as trimming prerequisites; Azure retains `--no-restore` because those projects were restored earlier in its shared workspace.

The PoC retains test results, tracer and CI Visibility logs, dumps, and snapshot output; obtains a short-lived API key through dd-sts; and runs `CheckBuildLogsForErrors` after the tests. It uses `needs:parallel:matrix` to download only the `net10.0` multi-version sample artifact. After this cell and all prerequisite producers are green, replace the static cell with a NUKE-generated Linux non-Docker integration matrix, then add Alpine/musl and ASM before introducing the Docker dependency groups.

Linux managed unit tests use the same generated child-pipeline architecture as Windows. `GenerateGitlabLinuxUnitTestsPipeline` reads `GetTestingFrameworks(PlatformFamily.Linux)` and emits paired x64 glibc and musl jobs for every selected TFM: six jobs (`netcoreapp3.1`, `net9.0`, and `net10.0` on both libc variants) for a normal merge request and sixteen jobs for a thorough run. Existing `unit-tests-linux-x64:*` names remain the Debian/glibc cells; `unit-tests-linux-musl-x64:*` cells use Alpine.

The `unit-tests-linux-x64` bridge waits for both x64 Linux producers. Each glibc child downloads only `build-linux-tracer-x64`, while each musl child downloads only `build-linux-tracer-x64-musl`, then builds the matching Debian or Alpine `tester` image and runs `BuildManagedUnitTests RunManagedUnitTests` for one framework. Child jobs set `IncludeAllTestFrameworks=true` because the generated framework is already authoritative; without it, a thorough-only framework such as `net6.0` would be filtered out again inside NUKE and could produce a false-green job with no tests. The same guard is applied to Windows child jobs. Building the tester image in every consumer is intentionally temporary; a content-addressed prebuilt test image should replace it after the job behavior is validated. The Alpine producer, default generated musl matrix, and thorough sixteen-job x64 Linux matrix are green.

Linux ARM64 now has two equivalent producers on `docker-in-docker:arm64`: `build-linux-tracer-arm64` uses Debian/glibc and `build-linux-tracer-arm64-musl` uses Alpine/musl. They reuse the existing Azure-tested Dockerfiles and run `Clean CompileManagedLoader BuildNativeTracerHome BuildManagedTracerHome ExtractDebugInfoLinux ValidateNativeTracerGlibcCompatibility`. They deliberately omit tracer native tests because Azure's ARM64 build does not invoke those targets. `GenerateGitlabLinuxArm64UnitTestsPipeline` uses `GetTestingFrameworks(PlatformFamily.Linux, isArm64: true)` and emits eight jobs normally (`net5.0`, `net6.0`, `net9.0`, and `net10.0` on both libc variants) or twelve for thorough runs (`net5.0` through `net10.0` on both variants). Both the default and thorough ARM64 build and managed-unit-test matrices are green, validating the private ECR Docker CLI mirror and the Debian, Alpine, and Datadog CA-certificate images on the ARM64 runner.

Inspection of the first downloaded ARM64 musl artifacts confirmed the expected output. The producer finished in 9:47 and uploaded a 164.9 MB archive containing 909 entries (520.4 MB uncompressed). Its `linux-musl-arm64` monitoring home contains `Datadog.Tracer.Native.so`, `libdatadog_profiling.so`, and `libddwaf.so`; all three report ELF machine 183 (AArch64). The compatibility target found no glibc dependency, all undefined symbols were available in the supported Alpine baseline, and the symbol snapshot was unchanged. The sampled `net10.0` child finished in 17:49, including a 14:35 NUKE build-and-test invocation, and its nine TRX files reported 20,399 passed, 80 skipped, and zero failed tests. The 7.1 MB compressed test artifact retained the TRX files, Datadog test-logger results, and tracer logs. The dd-sts API key exchange succeeded, but the `infra_logs` directory was empty; confirm ARM64 test events in the CI Visibility backend instead of relying on that diagnostic file.

The first generated Linux unit-test pipeline was green, and its framework artifacts contained the expected ordered TRX results and tracer logs. Glibc managed tests execute in the Debian `tester` container; the glibc producer's downloaded artifact also contains native outputs previously built and tested on CentOS 7, but CentOS is not a second managed-test environment. Musl managed tests execute separately in Alpine using the Alpine producer artifact. Both Windows and Linux managed-test containers explicitly enable the Datadog test logger and receive the GitLab pipeline, job, commit, runner, and merge-request variables consumed by `GitlabEnvironmentValues`. `CI_PROJECT_DIR` is translated to the mounted in-container checkout path. Every test assembly produces a `Datadog_TestResult_*.txt` file, and Linux test events have been confirmed in CI Visibility under `test.service:dd-trace-dotnet`. The jobs configure a dedicated diagnostic log path, but the sampled ARM64 artifact still contained an empty `infra_logs` directory, so backend events and the Datadog test-result files are currently the reliable telemetry evidence.

The first macOS PoC follows Azure's `build_macos` → `unit_tests_macos` boundary on the shared `macos:sonoma-amd64` runner. Azure builds the universal native tracer and the managed tracer/native loader in two parallel jobs, then merges their monitoring-home artifacts. GitLab initially runs the same target groups sequentially in one `build-macos-tracer-amd64` workspace, avoiding the intermediate merge while still publishing a combined producer artifact. The native targets continue to build universal `x86_64` and `arm64` dylibs.

The first consumer, `unit-tests-macos-amd64:net10.0`, was green. It validated access to the shared amd64 runner, job-local SDK installation, universal native compilation, the selective build-artifact handoff, and net10 managed-test execution. The static consumer has now been replaced by the same generated child-pipeline architecture used for Windows and Linux. `GenerateGitlabMacosUnitTestsPipeline` reads `GetTestingFrameworks(PlatformFamily.OSX)` and emits three jobs (`netcoreapp3.1`, `net9.0`, and `net10.0`) normally or all eight macOS-compatible frameworks for a thorough run. The parent `unit-tests-macos-amd64` bridge waits for the macOS producer and generator, and each child downloads the producer artifact with `needs:pipeline:job`.

Each macOS child installs the pinned 10.0.100 SDK under the checkout's `.dotnet` directory and, for older TFMs, installs only the requested x64 runtime into that same directory. Checkout-local CLI home and NuGet package directories avoid modifying the persistent shared runner globally. Every child retains TRX results, tracer/CI Visibility logs, and dumps, and requests the same short-lived dd-sts API key used by the Windows and Linux unit jobs. Both the generated default matrix (`netcoreapp3.1`, `net9.0`, and `net10.0`) and the thorough eight-framework macOS matrix are green.

### Unit-test parity with Azure

The GitLab managed-unit-test architecture now covers every platform, architecture, libc variant, and framework selected by Azure's unit-test matrices. Both systems derive their framework lists from `GetTestingFrameworks()`, and GitLab runs the same `BuildManagedUnitTests` and `RunManagedUnitTests` targets for each generated cell. Azure's macOS managed tests run on amd64 only; producing a universal native macOS artifact does not add a separate ARM64 unit-test matrix.

| Managed matrix | Normal cells | Thorough cells | GitLab validation |
| --- | ---: | ---: | --- |
| Windows x64 | 4 | 9 | Normal and thorough green |
| Linux x64, glibc and musl | 6 | 16 | Normal and thorough green |
| Linux ARM64, glibc and musl | 8 | 12 | Normal and thorough green |
| macOS amd64 | 3 | 8 | Normal and thorough green |

This is managed-unit-test matrix parity. Windows already matches Azure by running the tracer, native-loader, and profiler native tests. Linux x64 runs the tracer native tests in both glibc and musl producers, and the newly added profiler producers run Azure's profiler, native-wrapper, and native-loader suites on both libc variants. Those new jobs still require CI validation. Azure and GitLab both omit explicit native-test targets from the Linux ARM64 and macOS build paths.

Code-coverage parity is also pending. Azure passes `CodeCoverageEnabled` into managed unit tests when `run_code_coverage=true`; GitLab does not yet translate that variable or aggregate coverage artifacts. Consequently, the current milestone establishes ordinary managed-test coverage parity, while validation of the new Linux native producers and coverage-mode execution remain follow-up work.

### Azure queue-time variable compatibility

Azure exposes several queue-time variables used to expand coverage, force conditional stages, or diagnose failures. Do not add a variable to GitLab's prefilled **New pipeline** form until its complete behavior is wired and tested. A visible but unused control would imply coverage that the pipeline does not provide. Preserve the Azure name where practical during parallel validation, even when GitLab must translate it to a differently cased environment variable inside a container.

| Azure variable | Azure behavior and default | Current GitLab state | Migration decision |
| --- | --- | --- | --- |
| `enable_crash_dumps` | NUKE enables CLR minidumps when `true`; Azure explicitly enables it for Windows tracer integration tests and selected tool artifact tests. The NUKE default is `false`, while several Docker Compose services default it to `true`. | Not forwarded into the current unit-test containers. | Add as a `false`/`true` prefilled variable when integration tests migrate, forward it explicitly, and retain dumps as failure artifacts. It may also be useful for unit-test-flake investigations once validated. |
| `force_run_tests_with_isAsmChanged` | Forces `isAsmChanged=true` during `GenerateVariables`, adding ASM cells to applicable Windows/Linux integration matrices. Default `false`. | The focused GitLab unit-test generator does not calculate area-change variables. | Add when ASM integration matrices migrate. |
| `force_run_tests_with_isDebuggerChanged` | Forces debugger change detection, enabling debugger integration stages and debugger exploration coverage. Default `false`. | Not used. | Add with the debugger integration/exploration phase. |
| `force_run_tests_with_isProfilerChanged` | Forces profiler change detection, enabling profiler integration, sanitizer, and exploration coverage. Default `false`. | Not used. | Add with the profiler integration/exploration phase. |
| `force_run_tests_with_isTracerChanged` | Forces tracer change detection, principally enabling tracer exploration coverage. Default `false`. | Not used. | Add with exploration tests. |
| `force_ssi_run` | No consumer exists in the current repository YAML, scripts, or NUKE code. | No effect. | Do not migrate or prefill. Confirm whether an external/retired pipeline once consumed it, then remove it from Azure if obsolete. |
| `ForceDebugRun` | Sets `DD_TRACE_DEBUG=true` globally and makes NUKE enable debug tracing for test processes. NUKE also enables this for the Azure schedule named `Daily Debug Run`. Default `false`. | Not forwarded into current test containers. | Add as a diagnostic `false`/`true` option after log-volume behavior is validated in GitLab. Scheduled behavior must use `CI_PIPELINE_SOURCE` or an explicit schedule input. |
| `GIT_PROFILER_REF` | No consumer exists in the current repository YAML, scripts, or NUKE code. | No effect. | Do not migrate or prefill. Confirm whether it is a remnant of an older profiler checkout workflow. |
| `perform_comprehensive_testing` | Includes minor package versions in sample, integration, debugger, and profiler test builds. Default `false`. | Implemented as a prefilled `false`/`true` variable and forwarded to the multi-version sample producer. Integration, debugger, and profiler consumers have not migrated. | Validate the expanded sample artifacts, then forward the same value when those test families migrate. |
| `push_artifacts_to_azure_storage` | Tri-state release control: `true` uploads from any eligible run, `false` disables upload, and unset uploads only from main/release branches. Requires Azure storage credentials. | Not applicable to the current GitLab PoC. | Keep Azure-only during parallel validation. Replace it with an explicit GitLab release/publication policy rather than exposing an Azure-specific control prematurely. |
| `random_seed` | Azure exposes it to scripts as `RANDOM_SEED`; the custom xUnit framework uses an integer seed to reproduce randomized collection and test ordering. Unset chooses a new random seed and logs it. | Not forwarded into current test containers. | High-value diagnostic candidate. Add a prefilled free-text value only after mapping it explicitly to `RANDOM_SEED` in Windows and Linux containers. |
| `run_all_installer_tests` | Forces installer-related smoke stages that otherwise run on main/release or the automated Docker-image-bump PR. Default `false`. | Installer smoke stages have not migrated. | Add with installer smoke tests. |
| `run_all_test_frameworks` | Expands NUKE's reduced TFM set to every supported framework. Main/release/hotfix non-scheduled Azure runs also expand automatically. Default `false` for ordinary PRs. | Implemented. The parent generators accept the Azure-compatible lowercase name (and uppercase alias), and expand the Windows, Linux x64, Linux ARM64, and macOS child pipelines. | Already exposed as a prefilled `false`/`true` dropdown. Thorough matrices are green on all four platform groups. |
| `run_code_coverage` | Sets Azure's `CodeCoverageEnabled`, which NUKE passes to managed, integration, debugger, and profiler test targets. Default `false`; coverage can break tests that inspect IL. | Not translated to NUKE's `CodeCoverageEnabled`, and no merged coverage artifact/report flow exists. | Migrate as a complete coverage feature—not just a visible boolean—after defining artifact aggregation, reporting, duration, and exclusions. |

Suggested exposure order after `run_all_test_frameworks`: `random_seed`, `enable_crash_dumps`, and `ForceDebugRun` are useful diagnostics; `run_code_coverage` requires a dedicated coverage slice; area-force, comprehensive-package, and installer controls should land with the stages they affect. `force_ssi_run`, `GIT_PROFILER_REF`, and `push_artifacts_to_azure_storage` should not be added to the GitLab form under the current design.

### CI Visibility API key and secret management

GitLab pipeline visibility is already available without a repository-managed API key. On 2026-08-07, CI Visibility showed `DataDog/apm-reliability/dd-trace-dotnet` pipeline and `build` stage executions for non-default branches, including failures that occurred after only three seconds. This data is produced by Datadog's server-side GitLab integration: GitLab emits authenticated pipeline and job webhook events, and a background service reports them to CI Visibility. It therefore works even when a job fails before repository scripts run or before a container could retrieve a secret.

This must be distinguished from test visibility:

| Telemetry | Producer | Repository API key required? | Current state |
| --- | --- | --- | --- |
| Pipeline, stage/job status, duration, branch, and links | Server-side GitLab integration/webhooks | No | Already visible for default and non-default branches |
| DDCI orchestration around a GitLab pipeline | Server-side DDCI integration | No | Available for pipelines triggered through DDCI; may appear alongside the GitLab record |
| Managed test sessions, modules, suites, individual tests, and test traces | In-process `DatadogTestLogger` | Yes, unless a reachable Datadog Agent provides intake | Linux ingestion is confirmed; dd-sts supplies a temporary key to both Linux and Windows jobs |
| `dd_trace_dotnet.ci.tests.retries` and related build-side metrics | Test/build process | Yes | dd-sts supplies the temporary key; validate retry metrics in the next run |
| GitLab test-result artifacts | GitLab artifact/report upload | No Datadog key | TRX files are retained as ordinary artifacts; conversion to GitLab JUnit reports remains pending |

The existing Pipeline Executions data therefore does not prove that test events are being submitted. Validate test ingestion separately in the CI Visibility test views by filtering on a known GitLab pipeline or job ID and looking for test sessions and individual tests. The pipeline provider should be `GitLab`; DDCI-triggered work can additionally have a `DDCI` provider record.

Test-level GitLab ingestion is proven for both the existing microbenchmark pipeline and the migrated Linux unit-test jobs. The unit-test events were initially missed because the UI was filtered on the wrong service; they are present under `@test.service:dd-trace-dotnet` for the feature branch and corresponding child jobs.

The initial SSM experiment exposed an important platform difference. Linux could read `ci.dd-trace-dotnet.dd_api_key-prod` through broad ambient runner credentials, while both the Windows runner role and the repository's untrusted CI Identity received `AccessDeniedException`. Extending the inherited CI Identity policy would expose the reusable production key to branch-controlled untrusted jobs, so the proposed `cloud-inventory` permission is not the correct solution. The benchmark pipeline's ambient SSM access is legacy behavior to migrate later, not a model to expand.

The shared APM SDK GitLab pipeline already provides the appropriate mechanism. Its `apm-sdks-api-key` dd-sts policy accepts GitLab OIDC subjects matching `DataDog/apm-reliability/.*` branches and tags, which includes this repository. Jobs request `DD_STS_OIDC_TOKEN` with audience `rapid-seceng-sit` and exchange it at `https://dd-sts.us1.ddbuild.io/sts/datadog/exchange?policy=apm-sdks-api-key`. The response contains a temporary Datadog API key, so no long-lived project variable, SSM grant, new Vault path, or new policy is required.

The Windows child jobs perform the exchange on the runner and pass only `DD_LOGGER_DD_API_KEY` into the test container. Linux forwards the OIDC token into its short-lived tester container, whose Datadog CA bundle allows NUKE to perform the same exchange immediately before launching tests. Neither the OIDC token nor the returned key is logged. The old CI Identities client and SSM fallback have been removed from these paths.

Validate the next run by checking for `CI Visibility API key configured using dd-sts` and then confirming individual tests in CI Visibility with `@test.service:dd-trace-dotnet`, the feature branch, and the relevant GitLab child-job name. The existing `cloud-inventory` draft that adds SSM access should be closed or revised because it is no longer needed for unit-test telemetry.

The producer publishes `artifacts/bin`, `artifacts/monitoring-home`, and only the managed intermediate `ref`/`refint` assemblies needed by `dotnet build --no-dependencies`; broad `obj` trees are not transferred. The `dd_dotnet` apphost is also included because its net7 test project copies that intermediate executable. Each consumer restores its own package and MSBuild metadata, builds only projects that declare its requested test TFM, and runs that framework. This filtering is required because some unit-test projects intentionally target only one runtime, such as `Datadog.Trace.Tools.dd_dotnet.Tests` on `net7.0`. During matrix validation, jobs for .NET 3.0, 3.1, 5.0, 6.0, and 7.0 install only their requested runtime inside the container, allowing the existing image to remain usable. Once the runtime set is verified, these runtimes should be baked into and published as a new Windows image.

The first successful matrix consumer was `net7.0`. It downloaded and extracted the producer artifact in approximately 10 seconds, pulled the two missing image layers in approximately 3 seconds, and installed the 7.0.20 x64 runtime in approximately 4 seconds. NUKE completed in 15:03, including 0:48 to compile the selected test projects and 12:52 to run them; the complete GitLab job took approximately 15:56. All 10 applicable test assemblies produced TRX files: 20,463 tests passed, 57 were skipped, and none failed. The downloaded result artifact contains 10 TRX files and 11 tracer logs, totalling 53.4 MB uncompressed and 6.4 MB as the downloaded ZIP. The artifact upload succeeded. The only pipeline warnings were the expected `NU1503` messages for native projects included in the generated solution and the absent dumps directory on a successful run. The stored size of the producer handoff should still be measured from GitLab's build artifact UI.

Managed test results use TRX rather than JUnit XML. Each framework job retains its own `artifacts/build_data/results`, logs, and dumps as ordinary artifacts. Converting TRX to JUnit, or otherwise surfacing the managed results directly in GitLab's test-report UI, remains pending.

The first managed-test execution exceeded GitLab's 4 MiB job-log limit before the actual failure was printed. By the cutoff, the Datadog xUnit logger had emitted 7,230 per-test `STARTED` or `SUCCESS` lines. GitLab temporarily overrides NUKE's static `DotNetTasks.DotNetLogger` while managed tests run and filters only those two successful-event forms. This avoids NUKE 6.3's inability to clone settings containing delegates. Failures, skips, diagnostics, summaries, Azure output, and local output remain unchanged. The framework-specific TRX artifacts remain the source of truth if a failure occurs after any future log truncation.

The first managed-test attempts exposed a broader restore boundary. The solution-level Windows `NuGet.exe restore` did not populate every SDK-style `PackageReference` dependency needed by the managed test graph in the shared directory. This first appeared as a missing `StyleCop.Analyzers.Unstable` package in `BuildRunnerTool`; after restoring that project directly, compilation reached another missing package, `Microsoft.NET.ILLink.Analyzers`, through `Datadog.Trace.Tools.dd_dotnet`. GitLab now runs one `RestoreManagedUnitTestPackages` prerequisite against the solution using `Release|Any CPU`; otherwise NUKE's Windows `x64` target platform produces an invalid generated-solution configuration. Projects outside or incompletely represented by that solution restore—the instrumentation-verification generator, test helpers, and unit-test projects—restore explicitly for the selected GitLab TFM before building. The reduced producer artifact includes managed `ref`/`refint` assemblies so existing `--no-dependencies` builds can resolve project references without rebuilding the full production graph. It also includes the small `Datadog.Trace.Tools.dd_dotnet` apphost set required by its net7 test project. All of this behavior is gated by `IsGitlab`; Azure and local builds retain their original restored-workspace and all-TFM behavior.

The first run also revealed that the configured JUnit paths pointed at `build-out`, while NUKE writes results to `artifacts/build_data/tests` and `profiler/build_data/tests`. The paths are corrected. Native-loader x64 and x86 previously wrote the same filename; the filename now uses the loop architecture so both reports are retained.

The Windows commands are:

```text
CompileTracerNativeTests
RunTracerNativeTests
CompileNativeLoaderNativeTests
RunNativeLoaderNativeTests
CompileProfilerNativeTests
RunProfilerNativeTests
```

Windows coverage compared with Azure:

| Coverage | GitLab | Azure DevOps |
| --- | --- | --- |
| Runner definition | `windows-v2:2022` | `azure-managed-windows-x64-2` |
| Tracer native tests | x64 and x86 | x64 and x86 |
| Native loader tests | x64 and x86 | x64 and x86 |
| Profiler native tests | x64 and x86 | x64 and x86 |
| Windows ARM64/ARM64EC | Not tested | Not tested |
| Multiple Windows versions | No | No |

NUKE defaults to an x64 target on these runners, but the Windows native-test targets explicitly compile and execute both x64 and x86. GitLab therefore has parity with Azure for these Windows native suites.

Structural differences remain:

- Azure runs tracer/loader tests in the Windows tracer build job and profiler tests in a separate Windows profiler build job. GitLab runs all three suites after its unified Windows build in the same job.
- Azure retries each native-test command once. GitLab now forwards `DD_LOGGER_DD_API_KEY` to the test container but does not yet provide Azure's task-level retry.
- GitLab runs tracer native tests inline in both Linux x64 producers and now has separate glibc and musl profiler producers for the profiler, native-loader, and native-wrapper suites. The profiler producers await their first CI validation.
- Azure's Linux ARM64 and macOS build stages do not currently invoke these native unit-test targets.

## Active Windows image work and findings

### Pre-pull the image into the Windows runner AMI

Repeated logs show that pulling the dd-trace Windows image takes approximately 7–9 minutes. The four base Windows layers are already present, but the remaining dd-trace layers are downloaded for each fresh runner.

The CI Infrastructure team confirmed that the intended optimization is:

```text
ci-platform-machine-images/packer/windows/scripts/base/pre-pull-docker-images.ps1
```

Findings:

- The script runs while the Windows AMI is baked.
- The Windows root volume defaults to 300 GB, so the additional cached image should not create a material storage problem.
- Pre-pull the exact content tag consumed by CI, not only `:latest`.
- The exact tag must be refreshed whenever a file under `tracer/build/_build/docker/gitlab` changes.
- A modified copy currently exists at `citemp/packer/windows/scripts/base/pre-pull-docker-images.ps1`.
- That copy currently references `814a0509e85a`.
- Do not submit that tag unchanged after PR #8962 or another image change merges. Recompute the hash, build and publish the new image, and then update the AMI PR.

Expected benefit: remove approximately 7–9 minutes from a fresh Windows job. Based on measured runs, that could reduce a roughly 19–24 minute job to approximately 12–17 minutes, subject to runner and build variance.

### Pre-install vcpkg helper tools

[PR #8962](https://github.com/DataDog/dd-trace-dotnet/pull/8962) is open.

The PR:

- Installs and bootstraps vcpkg in the GitLab Windows image.
- Pre-fetches helper tools such as Git, CMake, 7-Zip, PowerShell Core, and Ninja.
- Uses vcpkg's default downloads root so the pre-fetched helper tools are reused.
- Avoids downloading the helper toolchain on every GitLab build.

The latest GitLab build, Azure Windows tracer build, and Azure Windows profiler build are green.

Review findings:

- The vcpkg version remains duplicated between `gitlab.windows.dockerfile` and `Build.Steps.cs`.
- `GetVcpkg()` prefers any `vcpkg.exe` on `PATH` and does not validate its version. A future one-sided version bump could therefore make GitLab and fallback builds use different versions.
- Prefer a single checked-in version source, for example `vcpkg-version.txt`, read by both NUKE and the Docker installation.
- `install_vcpkg.ps1` downloads a GitHub tag archive without verifying a checksum. Pinning and verifying SHA-256 would align it with the other image installers.
- A checksum calculated from the same first download pins the observed bytes but is not independent publisher verification.
- GitHub-generated source archives may eventually change their compressed byte layout. For stronger reproducibility, pin the tag's commit SHA as well; a release asset with a publisher-provided digest would be preferable if one exists.
- Removing the explicit downloads root changes all Windows builds, not only GitLab. The successful Azure Windows jobs reduce the immediate compatibility risk, but the behavior should remain documented.
- Pre-downloading libdatadog itself remains a possible later optimization and is intentionally out of scope for PR #8962.

## Proposed GitLab file layout

Use one file per pipeline phase, with all platforms represented as separate jobs or matrices inside that file:

```text
.gitlab-ci.yml
.gitlab/
  ci/
    templates.yml
    images.yml
    build.yml
    test-unit.yml
    test-integration.yml
    package.yml
    test-smoke.yml
  benchmarks/
```

Responsibilities:

- `templates.yml`: common defaults, rules, runner tags, and per-platform NUKE skeletons.
- `images.yml`: image hash calculation and image-build jobs.
- `build.yml`: platform builds and their artifacts.
- `test-unit.yml`: native and managed unit-test jobs that consume platform build artifacts.
- `test-integration.yml`: integration and profiler-integration tests.
- `package.yml`: packaging and signing.
- `test-smoke.yml`: artifact smoke tests.

Naming convention:

```text
<phase>:<platform>[:<arch>][:<libc>][:<framework>]
```

Examples:

```text
gl-build:windows:x64
gl-build:linux:arm64:musl
gl-test-unit:linux:x64:glibc:net8.0
```

During parallel validation, use `gl-` stage and job prefixes so the new jobs cannot collide with existing jobs. Gate the PoC with:

```yaml
GITLAB_POC: "true"
```

The variable should default to enabled during validation but allow the PoC jobs to be disabled without removing YAML.

## Phase 1: builds and unit tests

### Scope

- Build Windows x64.
- Build Linux x64 on glibc and musl.
- Build Linux ARM64 on glibc and musl.
- Build macOS ARM64.
- Produce platform monitoring-home artifacts without signing or packaging.
- Run native unit tests explicitly in downstream jobs when build artifacts can be reused efficiently.
- Run managed unit tests in downstream jobs by target framework.

Out of scope:

- Signing and MSI/deb/rpm packaging.
- S3 and OCI publishing.
- R2R variants.
- Sample and debug builds.
- IIS and Azure Functions tests.
- Integration and smoke tests.
- macOS AMD64.

### Top-level changes

Modify `.gitlab-ci.yml` additively:

- Include `.gitlab/ci/templates.yml`.
- Include `.gitlab/ci/images.yml`.
- Include `.gitlab/ci/build.yml`.
- Include `.gitlab/ci/test-unit.yml`.
- Add `gl-images`, `gl-build`, and `gl-test-unit` stages.
- Add the `GITLAB_POC` variable.
- Preserve all existing jobs and stages.

### Shared templates

Proposed defaults:

```yaml
.dd-default:
  interruptible: true
  variables:
    GIT_DEPTH: 20
    GIT_STRATEGY: fetch
    GIT_SUBMODULE_STRATEGY: recursive

.dd-rules-poc:
  rules:
    - if: '$GITLAB_POC == "true"'
      when: on_success
    - when: never
```

Before adopting recursive submodules, verify that the repository requires them; avoid unnecessary clone work.

Add `.dd-nuke-windows`, `.dd-nuke-linux`, and `.dd-nuke-macos` templates with their images, runner tags, setup, and common artifact handling.

### Image jobs

Windows:

- Reuse `tracer/build/_build/docker/gitlab/compute-image-hash.ps1`.
- Keep or refactor the existing manual `build-windows-ci-image` job.
- Continue failing consumers when their expected content-addressed image is absent.

Linux:

- Add `tracer/build/_build/docker/compute-linux-image-hash.sh`.
- Build Debian, Alpine, CentOS 7, universal, and Alpine ARM64 variants as required.
- Use `arch:amd64` or `arch:arm64` runners with remote BuildKit.
- Publish content-addressed images under a repository such as:

  ```text
  registry.ddbuild.io/ci/dd-trace-dotnet/dd-trace-dotnet-linux-build-<variant>:<hash>
  ```

The exact Linux image set should be derived from Phase 1 jobs first; avoid building unused variants.

### Build jobs

| Job | Runner | Image | Principal NUKE targets |
| --- | --- | --- | --- |
| `gl-build:windows:x64` | `windows-v2:2022` | Windows content-hash image | `BuildTracerHome BuildProfilerHome BuildNativeLoader BuildDdDotnet` |
| `gl-build:linux:x64:glibc` | `arch:amd64` | Debian build image | `Clean CompileManagedLoader BuildNativeTracerHome BuildManagedTracerHome BuildNativeLoader BuildNativeWrapper BuildDdDotnet ExtractDebugInfoLinux BuildProfilerHome` |
| `gl-build:linux:x64:musl` | `arch:amd64` | Alpine build image | Same platform-appropriate build targets |
| `gl-build:linux:arm64:glibc` | `arch:arm64` | ARM64 Debian build image | Same platform-appropriate build targets |
| `gl-build:linux:arm64:musl` | `arch:arm64` | ARM64 Alpine build image | Same platform-appropriate build targets |
| `gl-build:macos:arm64` | `macos:sonoma-arm64` | Native runner | `CreateRequiredDirectories CompileManagedLoader BuildNativeTracerHome BuildManagedTracerHome BuildNativeLoader` |

Linux jobs may use a matrix over architecture and libc when the image, runner tag, artifact name, and `needs` mapping remain understandable.

#### Native-test requirement

Do not assume that `BuildTracerHome` or `BuildProfilerHome` automatically runs the native unit tests. Azure invokes them explicitly.

Windows parity requires at least:

```text
CompileTracerNativeTests
RunTracerNativeTests
CompileNativeLoaderNativeTests
RunNativeLoaderNativeTests
CompileProfilerNativeTests
RunProfilerNativeTests
```

The current `build` job provides this Windows parity inline, matching Azure's build-then-native-test topology. It should be moved into the proposed Phase 1 file layout rather than reimplemented.

Additional native-test jobs required for current Azure parity:

| Job | Coverage | Principal NUKE targets |
| --- | --- | --- |
| `gl-build:windows:x64` | Windows x64 and x86, inline after the build | The six tracer, loader, and profiler targets listed above |
| `gl-test-native:linux:x64:glibc` | Linux x64 on glibc | Tracer, profiler, native loader, and native wrapper compile/run targets |
| `gl-test-native:linux:x64:musl` | Linux x64 on musl | The same Linux native targets in Alpine |

Azure does not currently invoke these native unit-test targets in its Linux ARM64 or macOS build stages, so those platforms are not required for native-test parity in Phase 1.

### Build artifacts

Publish the minimum artifacts required by downstream tests:

- `monitoringHome/`
- Required managed build outputs under `tracer/bin/`
- Required profiler outputs
- Required shared/native outputs
- Native test reports from the producing Windows build job and downstream Linux native-test jobs

Do not publish broad working directories by default. Measure artifact size and transfer time, then add only missing paths required by downstream jobs. Native tests currently run in the build job and therefore do not require native bin/object artifacts.

Current Windows measurements:

| Measurement | GitLab selective build state | Azure `build-windows-working-directory` |
| --- | ---: | ---: |
| Files | 631 | 27,222 |
| Reported/uncompressed size | 4.68 GB | 4,483 MB in the artifact UI |
| Downstream total content | 4.68 GB uncompressed | 4,724.1 MB |
| Physical downstream download | Not reported separately | 2,548.5 MB |
| Download and extraction | Approximately 65 seconds | Duration not available in the captured excerpt |
| Current native-test benefit | Approximately 46 seconds end-to-end | Tests run in the producing build jobs |

Azure's artifact is broader, while the tested GitLab artifact selected native tracer, loader, and profiler state and excluded vcpkg. It was removed from the current pipeline because the transfer had limited value for one native-test consumer. Reconsider a minimal build-state artifact only when additional downstream Windows jobs demonstrate that its shared benefit exceeds its upload, storage, and download cost.

### Managed unit-test jobs

| Job pattern | Matrix | Runner | Dependency | NUKE target |
| --- | --- | --- | --- | --- |
| `unit-tests-windows:*` | NUKE-selected Windows TFMs: 4 normally, 9 for thorough runs | `windows-v2:2022` | parent `build` via `needs:pipeline:job` | `BuildManagedUnitTests RunManagedUnitTests --framework $FRAMEWORK` |
| `unit-tests-linux-x64:*` | NUKE-selected Linux x64 TFMs: 3 normally, 8 for thorough runs; Debian/glibc | `docker-in-docker:amd64` | parent `build-linux-tracer-x64` via `needs:pipeline:job` | `BuildManagedUnitTests RunManagedUnitTests --framework $FRAMEWORK` |
| `unit-tests-linux-musl-x64:*` | Same NUKE-selected TFMs; Alpine/musl | `docker-in-docker:amd64` | parent `build-linux-tracer-x64-musl` via `needs:pipeline:job` | `BuildManagedUnitTests RunManagedUnitTests --framework $FRAMEWORK` |
| `unit-tests-linux-arm64:*` | NUKE-selected Linux ARM64 TFMs: 4 normally, 6 for thorough runs; Debian/glibc | `docker-in-docker:arm64` | parent `build-linux-tracer-arm64` via `needs:pipeline:job` | `BuildManagedUnitTests RunManagedUnitTests --framework $FRAMEWORK` |
| `unit-tests-linux-musl-arm64:*` | Same ARM64-selected TFMs; Alpine/musl | `docker-in-docker:arm64` | parent `build-linux-tracer-arm64-musl` via `needs:pipeline:job` | `BuildManagedUnitTests RunManagedUnitTests --framework $FRAMEWORK` |
| `unit-tests-macos-amd64:*` | NUKE-selected macOS TFMs: 3 normally, 8 for thorough runs | `macos:sonoma-amd64` | parent `build-macos-tracer-amd64` via `needs:pipeline:job` | `BuildManagedUnitTests RunManagedUnitTests --framework $FRAMEWORK` |

Publish JUnit-compatible XML with `artifacts:reports:junit` so failures appear directly on the merge request.

Current implemented build-and-unit-test slice:

- 6 producer jobs: Windows, Linux x64 glibc and musl, Linux ARM64 glibc and musl, and macOS amd64. Native tests run inside the relevant x64 producers.
- 1 matrix-generator job and 4 child-pipeline bridge jobs.
- 4 Windows managed-unit-test jobs normally; 9 for thorough runs.
- 6 Linux x64 managed-unit-test jobs normally; 16 for thorough runs.
- 8 Linux ARM64 managed-unit-test jobs normally; 12 for thorough runs.
- 3 macOS managed-unit-test jobs normally; 8 for thorough runs.
- 32 jobs normally; 56 for thorough runs.

Validate the budget with the CI Infrastructure team because concurrency is shared and job count alone does not capture queueing or CPU usage.

### Phase 1 success criteria

- All 28 jobs pass on representative merge requests.
- Existing Azure and GitLab jobs remain unchanged and green.
- Each build publishes the expected platform artifacts.
- Each managed test job publishes a visible JUnit report.
- GitLab and Azure monitoring-home outputs are equivalent for the same commit, allowing timestamps and explicitly documented signing differences.
- Compare artifacts manually for approximately the first ten successful PoC runs.
- No unexplained binary or file-set differences.
- Windows build remains below 25 minutes before AMI pre-pull and targets approximately 12–17 minutes after pre-pull.
- Each Linux build cell targets 25 minutes or less.
- macOS build targets 30 minutes or less.
- Total build and unit-test wall clock targets 75 minutes or less, including queueing.
- The PoC can be disabled through `GITLAB_POC` without reverting code.

## Phase 2

No separate Phase 2 is currently required. Managed and native unit tests are part of Phase 1.

## Phase 3: integration tests

Add `.gitlab/ci/test-integration.yml`.

Initial direction:

- Linux integration jobs use `docker-in-docker:amd64` and `docker-in-docker:arm64`.
- Reuse `docker-compose.yml`, Testcontainers, and existing NUKE targets.
- Start with matrices over architecture, libc, and framework.
- Keep Windows IIS tests separate because they mutate machine state.
- Keep Windows Azure Functions tests separate.
- Add macOS integration tests per supported framework.
- Mirror profiler integration tests on Windows and Linux.

The proposed coarse matrix favors fewer, longer jobs to reduce shared-runner contention. Measure real durations before committing to it. Split by area only where a job approaches its timeout or becomes too costly to retry.

## Phase 3.5: packaging

Add `.gitlab/ci/package.yml`.

Windows:

- Consume `gl-build:windows:x64`.
- Run packaging and signing targets such as `PackageTracerHome`, `PublishFleetInstaller`, `SignDlls`, and `SignMsi`.
- Produce MSI, symbols, tracer home, Fleet Installer, and related artifacts with Azure-equivalent naming and retention.

Linux:

- Consume the matching architecture/libc build.
- Produce tar.gz, deb, and rpm artifacts as applicable.
- Verify whether current Azure packages are GPG-signed and reproduce that behavior.

Packaging must be complete before smoke tests that consume installed artifacts.

## Phase 4: smoke tests

Add `.gitlab/ci/test-smoke.yml`.

Candidate categories:

- Installer
- NuGet
- Tool
- Trimming
- Self-instrumentation
- Fleet Installer
- `dd-dotnet`

Use matrices over architecture, libc, and .NET version where appropriate.

- Linux smoke tests use Docker-in-Docker microVMs.
- Windows smoke tests use `windows-v2:2022`.
- macOS initially covers applicable tool tests on `macos:sonoma-arm64`.

Start with approximately 10–12 job definitions and measure the expanded matrix before enabling all cells by default.

## Phase 5: validation and switchover

Run Azure and GitLab in parallel for at least four weeks.

Proposed confidence requirement:

- At least 95% of merge requests in the window have Azure and GitLab in agreement: both green or both red for equivalent work.
- Classify disagreements as infrastructure, flaky test, configuration difference, artifact difference, or genuine product regression.
- A comparison job or scheduled process should publish agreement and duration metrics to Datadog.

Suggested removal order:

1. Stop Azure packaging stages after artifact equivalence is established.
2. Stop Azure build stages after GitLab artifacts are proven consumable.
3. Stop Azure test stages incrementally: unit, integration, then smoke.
4. Re-point OCI, serverless, and publishing jobs to GitLab artifacts.
5. Remove `.gitlab/download-single-step-artifacts.sh` and `.gitlab/download-serverless-artifacts.sh`.
6. Remove Azure status-reporting hooks.
7. Delete `.azure-pipelines/`.
8. Drop the temporary `gl-` prefixes after the new pipeline becomes authoritative.

Do not create a test-stage outage between stopping Azure builds and migrating tests. Either keep Azure builds until their dependent tests move, or explicitly validate cross-CI artifact consumption first.

## Verification procedure

### Local command sanity

Windows:

```powershell
docker run --rm `
  -v "${PWD}:C:\src" `
  $WINDOWS_BUILD_IMAGE `
  BuildTracerHome BuildProfilerHome BuildNativeLoader BuildDdDotnet
```

Run the six explicit Windows native-test targets immediately after the production build in the same job when validating Phase 1 parity.

Linux:

```bash
docker run --rm -v "$PWD:/src" <image> \
  ./tracer/build.sh <platform build targets>
```

Run the x64 glibc and musl native-test targets separately to match Azure. ARM64 native unit tests are not part of current Azure parity.

macOS:

```bash
./tracer/build.sh \
  CreateRequiredDirectories \
  CompileManagedLoader \
  BuildNativeTracerHome \
  BuildManagedTracerHome \
  BuildNativeLoader
```

No macOS native unit-test targets are required for current Azure parity.

### Pipeline dry run

- Push a branch with `GITLAB_POC=true`.
- Confirm all 33 Phase 1 jobs are created.
- Confirm each job selects the intended runner and image.
- Confirm build artifacts are available through `needs`.
- Confirm test reports appear on the merge request.
- Record execution and queue duration separately.

### Artifact comparison

For the same commit:

1. Download GitLab and Azure artifacts.
2. Normalize timestamps and known signing differences.
3. Compare file lists, binary contents, symbols, permissions, and expected executable formats.
4. Treat every other difference as a migration defect until explained.

### AMI pre-pull validation

Before updating the runner AMI:

- Record the exact requested image tag.
- Record the `Unable to find image ... locally` timestamp.
- Record the final `Downloaded newer image` timestamp.

After deployment:

- Confirm the exact hash-tagged image exists locally.
- Confirm no layers are downloaded before the build.
- Compare fresh-runner duration with the prior 7–9 minute pull.

### Azure unchanged

- Confirm `.azure-pipelines/ultimate-pipeline.yml` remains functionally unchanged during Phase 1.
- Confirm existing GitLab build, publish, benchmark, OCI, and serverless jobs remain functional.
- Run a clean `master` build after foundational image changes.

## Open decisions and risks

### Immediate

- Merge or otherwise resolve PR #8962 before finalizing the AMI pre-pull tag.
- Decide whether to centralize the vcpkg version before merging PR #8962.
- Decide whether to pin and verify the vcpkg archive checksum.
- Recompute and publish the final Windows image hash.
- Update the `ci-platform-machine-images` PR to pre-pull that exact tag.
- Make `docker pull` failures explicit in the pre-pull script:

  ```powershell
  docker pull $image
  if ($LASTEXITCODE -ne 0) {
      throw "Failed to pull Docker image '$image' (exit code $LASTEXITCODE)."
  }
  ```

- Run the Windows 2022 Packer build manually on the AMI PR.
- Deploy the AMI and validate the measured pull-time reduction.
- Decide whether the Windows Packer job must become a required PR check. It is manual on PR branches and automatic on `main`.

### Phase 1 design

- Rename the existing Windows `build` job to `build-windows-tracer-x64` after updating and validating every artifact, packaging, publishing, and child-pipeline consumer.
- Confirm the concurrent-job and runner-capacity budget with CI Infrastructure.
- Confirm whether recursive submodules are required.
- Validate the new Linux x64 profiler/native-loader/native-wrapper producers and their artifacts; the Windows mapping is complete and green.
- Define the minimum artifact set for each downstream test job.
- Confirm GitLab `needs` behavior and artifact naming for parallel matrices.
- Decide whether Linux needs every proposed image variant in Phase 1.
- Define Linux image ownership, rebuild procedure, hash inputs, and publication permissions.
- Decide how image-building jobs authenticate without creating private-repository dependencies that violate public-repository guidance.

### Later phases

- Decide when to add the Linux x64 R2R variant.
- Decide whether samples are built once or inside integration jobs.
- Extend the generated-child-pipeline approach beyond Windows unit tests as each later matrix migrates.
- Confirm Linux package-signing parity.
- Re-evaluate Dockerfile layer splitting if AMI pre-pull and vcpkg initialization do not meet the Windows target.
- Consider pre-caching libdatadog after measuring its remaining contribution.
- Define status-reporting and Datadog metrics for Azure/GitLab agreement.

## Critical files

### Proposed new files

- `.gitlab/ci/templates.yml`
- `.gitlab/ci/images.yml`
- `.gitlab/ci/build.yml`
- `.gitlab/ci/test-unit.yml`
- `.gitlab/ci/test-integration.yml`
- `.gitlab/ci/package.yml`
- `.gitlab/ci/test-smoke.yml`
- `tracer/build/_build/docker/compute-linux-image-hash.sh`

### Files modified during the migration

- `.gitlab-ci.yml`
- `.gitlab/linux-unit-tests-child.yml`
- `tracer/build/_build/Build.VariableGenerations.cs`
- `tracer/build/_build/docker/gitlab/UPDATING_IMAGE.md`
- `tracer/build/_build/docker/gitlab/gitlab.windows.dockerfile`
- `tracer/build/_build/docker/gitlab/compute-image-hash.ps1`
- `tracer/build/_build/docker/gitlab/entrypoint.bat`
- `ci-platform-machine-images/packer/windows/scripts/base/pre-pull-docker-images.ps1` in the separate runner-image repository

### Existing sources of truth

- `.azure-pipelines/ultimate-pipeline.yml`: current parity reference until Phase 5.
- `tracer/build/_build/Build.cs` and related partial files: NUKE orchestration and target source of truth.
- `tracer/build.sh` and `tracer/build.cmd`: platform launchers.
- Existing Dockerfiles and Docker Compose files: test and packaging environment definitions.
