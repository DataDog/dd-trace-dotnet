# Updating the Linux build/unit-test images

Linux shadow jobs pull prepared environments; they never build or publish them. The root pipeline exposes two optional, manual publication matrices:

- `build-linux-ci-images-x64`, on `docker-in-docker:amd64`: Debian, Alpine, CentOS 7 and universal.
- `build-linux-ci-images-arm64`, on `docker-in-docker:arm64`: Debian, Alpine and universal.

Each toolchain family runs in its own job, preserving builder/tester layer reuse within that family without accumulating all toolchains in one Docker daemon. For example, `build-linux-ci-images-x64: [centos7]` publishes only the x64 CentOS environment. Publication is serialized per architecture/family across pipelines. Jobs report Docker storage usage before publication and on failure; they never prune the daemon.

Publication runs candidate-controlled Dockerfiles and writes shared registry tags, so review the inputs before starting either job. This is a human checkpoint, not a registry permission boundary. Normal pipelines with existing images need no approval. No `latest` tag is published or consumed.

## Inventory and inputs

| Environment | Architectures | SDK |
| --- | --- | --- |
| Debian builder/tester | x64, ARM64 | `global.json` |
| Alpine builder/tester | x64, ARM64 | `global.json` |
| CentOS 7 native builder | x64 | Azure's `nativeBuildDotnetSdkVersion` |
| Universal native builder | x64, ARM64 | Azure's `nativeBuildDotnetSdkVersion` |

[`linux-ci-images.sh`](linux-ci-images.sh) owns the 11 image references and publication flow. Tags in `registry.ddbuild.io/ci/dd-trace-dotnet/dd-trace-dotnet-linux-ci` contain a recipe hash, environment, role and architecture. The hash includes the toolchain Dockerfiles, managed/native SDK versions, image-layer definitions and publication helpers. It excludes NUKE source and restore inputs, which are restored/built at job startup, and this guide. Existing tags are skipped rather than overwritten.

The shared Dockerfiles expose `build-environment` and, where applicable, `test-environment` stages without compiled NUKE code. Azure's existing `builder` and `tester` stages retain their behavior. Test environments include the existing runtime inventory and pinned Datadog CA layer. Runtime channels in the shared Dockerfiles are resolved during manual publication, not ordinary test jobs.

[`linux-ci-environment.dockerfile`](linux-ci-environment.dockerfile) warms NUKE's package cache without retaining compiled code or `obj` state. Each container compiles the candidate checkout through [`run-nuke-linux-container.sh`](run-nuke-linux-container.sh), excluding checkout `bin`/`obj`. Compilation runs outside the checkout with an explicit SDK selection so legacy toolchains are not forced onto the managed SDK. This bootstrap restores/builds NUKE only; managed-test consumers still execute producer test DLLs without restoring or compiling product/test projects.

## Update and validate

1. Edit the environment inputs or SDK pins. NUKE source/dependency-only changes do not require new images.
2. Push the branch. Image checks, producers and managed-test jobs resolve the same image references and SDK versions directly from their checkout; no metadata job or artifact is needed. `LINUX_CI_IMAGE_BASE` overrides are forwarded through the nested test triggers.
3. `check-linux-ci-images-x64` and `check-linux-ci-images-arm64` inspect every expected image manifest for their architecture before its producers start. They are read-only, use the same inventory as publication, and have no dependency on the other architecture's check. Windows jobs do not depend on them.
4. If a check reports missing images, review the changes and run the matching toolchain-family entries in the root pipeline. On the first publication or when shared inputs change, all seven entries may be needed. Publication requires CI Identity registry access; do not pass registry credentials into image layers.
5. Wait for publication to succeed, retry the failed architecture check, then retry any skipped consumers. Retrying a check never builds or publishes images. Authentication or connectivity errors may not require rebuilding. A partial publication can be retried: images already pushed are skipped.
6. Validate managed unit tests on both architectures/libc variants, native tracer/profiler suites on x64, and universal builds on both architectures. Inspect image pull time, NUKE bootstrap time, runtime coverage, diagnostics and CI Visibility. Publishing images alone does not establish test parity.

If publication runs out of Docker disk space, inspect the storage diagnostics. Retry only the failed toolchain entry; completed registry tags are reused. If a single toolchain still cannot fit in a fresh job, raise the job's storage requirement with CI Infrastructure rather than pruning a shared runner or changing the compiler baseline.
