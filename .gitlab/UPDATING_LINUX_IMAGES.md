# Updating the Linux build/unit-test images

Linux shadow jobs pull prepared environments; they never build or publish them. The root pipeline exposes two optional, manual publication jobs:

- `build-linux-ci-images-x64`, on `docker-in-docker:amd64`.
- `build-linux-ci-images-arm64`, on `docker-in-docker:arm64`.

Publication runs candidate-controlled Dockerfiles and writes shared registry tags, so review the inputs before starting either job. This is a human checkpoint, not a registry permission boundary. Normal pipelines with existing images need no approval. No `latest` tag is published or consumed.

## Inventory and inputs

| Environment | Architectures | SDK |
| --- | --- | --- |
| Debian builder/tester | x64, ARM64 | `global.json` |
| Alpine builder/tester | x64, ARM64 | `global.json` |
| CentOS 7 native builder | x64 | Azure's `nativeBuildDotnetSdkVersion` |
| Universal native builder | x64, ARM64 | Azure's `nativeBuildDotnetSdkVersion` |

[`linux-ci-images.sh`](linux-ci-images.sh) owns the 11 image references and publication flow. Tags in `registry.ddbuild.io/ci/dd-trace-dotnet/dd-trace-dotnet-linux-ci` contain a recipe hash, environment, role and architecture. The hash includes the toolchain Dockerfiles, managed/native SDK versions, NUKE restore inputs, image-layer definitions and publication helpers. It excludes NUKE C# source and this guide. Existing tags are skipped rather than overwritten.

The shared Dockerfiles expose `build-environment` and, where applicable, `test-environment` stages without compiled NUKE code. Azure's existing `builder` and `tester` stages retain their behavior. Test environments include the existing runtime inventory, pinned Datadog CA layer and `jq`. Runtime channels in the shared Dockerfiles are resolved during manual publication, not ordinary test jobs.

[`linux-ci-environment.dockerfile`](linux-ci-environment.dockerfile) warms NUKE's package cache without retaining compiled code or `obj` state. Each container compiles the candidate checkout through [`run-nuke-linux-container.sh`](run-nuke-linux-container.sh), excluding checkout `bin`/`obj`. Compilation runs outside the checkout with an explicit SDK selection so legacy toolchains are not forced onto the managed SDK. This bootstrap restores/builds NUKE only; managed-test consumers still execute producer test DLLs without restoring or compiling product/test projects.

## Update and validate

1. Edit the environment inputs or SDK pins. C#-only changes do not require new images.
2. Push the branch. `resolve-linux-ci-images` resolves SDK/image metadata once in the shadow pipeline and retains `.gitlab/generated/linux-ci-images.env`. Producers and managed-test children download that same-pipeline artifact explicitly.
3. `check-linux-ci-images-x64` and `check-linux-ci-images-arm64` inspect every expected image manifest for their architecture before its producers start. They are read-only, use the same inventory as publication, and have no dependency on the other architecture's check. Windows jobs do not depend on them.
4. If a check reports missing images, review the changes and run the matching manual publication job in the root pipeline. Run both publication jobs when shared inputs change and neither architecture's new images exist. Publication requires CI Identity registry access; do not pass registry credentials into image layers.
5. Wait for publication to succeed, retry the failed architecture check, then retry any skipped consumers. Retrying a check never builds or publishes images. Authentication or connectivity errors may not require rebuilding. A partial publication can be retried: images already pushed are skipped.
6. Validate managed unit tests on both architectures/libc variants, native tracer/profiler suites on x64, and universal builds on both architectures. Inspect image pull time, NUKE bootstrap time, runtime coverage, diagnostics and CI Visibility. Publishing images alone does not establish test parity.
