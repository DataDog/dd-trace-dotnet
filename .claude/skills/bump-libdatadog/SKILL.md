---
name: bump-libdatadog
description: >-
  Update/bump the libdatadog native library version in dd-trace-dotnet. Use when the user asks to
  bump, update, or upgrade libdatadog, or mentions a new libdatadog release version.
---

# Bump libdatadog Version

## Overview

libdatadog is consumed as prebuilt native binaries from GitHub releases. There are **two independent version pins** that may need updating:

| Platform | File | Hash type | Version format |
|----------|------|-----------|----------------|
| Linux/macOS | `build/cmake/FindLibdatadog.cmake` | SHA-256 | `v<MAJOR>.<MINOR>.<PATCH>` |
| Windows | `build/vcpkg_local_ports/libdatadog/vcpkg.json` + `portfile.cmake` | SHA-512 | `<MAJOR>.<MINOR>.<PATCH>` (no `v` prefix) |

These versions can differ (Windows may lag behind Linux/macOS). Confirm with the user which platforms to update.

## Files to Modify

### 1. Linux/macOS — `build/cmake/FindLibdatadog.cmake`

Update these values:
- `LIBDATADOG_VERSION` — the version tag (e.g. `"v32.0.0"`)
- `SHA256_LIBDATADOG_ARM64` — macOS arm64 hash
- `SHA256_LIBDATADOG_X86_64` — macOS x86_64 hash
- `SHA256_LIBDATADOG` (aarch64 gnu) — Linux aarch64 glibc hash
- `SHA256_LIBDATADOG` (aarch64 musl) — Linux aarch64 Alpine hash
- `SHA256_LIBDATADOG` (x86_64 musl) — Linux x86_64 Alpine hash
- `SHA256_LIBDATADOG` (x86_64 gnu) — Linux x86_64 glibc hash

Artifact filenames (from GitHub releases):
- `libdatadog-aarch64-apple-darwin.tar.gz`
- `libdatadog-x86_64-apple-darwin.tar.gz`
- `libdatadog-aarch64-unknown-linux-gnu.tar.gz`
- `libdatadog-aarch64-alpine-linux-musl.tar.gz`
- `libdatadog-x86_64-alpine-linux-musl.tar.gz` (note: uses `${CMAKE_SYSTEM_PROCESSOR}` in filename)
- `libdatadog-x86_64-unknown-linux-gnu.tar.gz` (note: uses `${CMAKE_SYSTEM_PROCESSOR}` in filename)

### 2. Windows — `build/vcpkg_local_ports/libdatadog/`

Two files:
- **`vcpkg.json`** — update `"version-string"` (no `v` prefix)
- **`portfile.cmake`** — update SHA-512 hashes for x64 and x86 Windows zips

Artifact filenames:
- `libdatadog-x64-windows.zip`
- `libdatadog-x86-windows.zip`

## Step-by-Step Procedure

### Step 1: Determine target version

Ask the user for the target version, or check the latest release:
```
https://github.com/DataDog/libdatadog/releases
```

### Step 2: Get hashes from the release page

SHA-256 and SHA-512 checksums are published directly in the GitHub release notes. Either:

1. Visit `https://github.com/DataDog/libdatadog/releases/tag/v<VERSION>` and copy from the checksums sections, or
2. Run the helper script which fetches them via the GitHub API:

```bash
bash .claude/skills/bump-libdatadog/scripts/fetch-release-hashes.sh <VERSION>
```

Where `<VERSION>` is without the `v` prefix (e.g. `33.0.0`).

The release notes contain:
- **SHA256 checksums** section → for `FindLibdatadog.cmake` (6 Linux/macOS artifacts)
- **SHA512 checksums** section → for `portfile.cmake` (2 Windows artifacts)

### Step 3: Update `build/cmake/FindLibdatadog.cmake`

1. Update `LIBDATADOG_VERSION` to `"v<VERSION>"`
2. Replace each `SHA256_LIBDATADOG*` value with the new SHA-256 hash from the script output

### Step 4: Update `build/vcpkg_local_ports/libdatadog/vcpkg.json`

Update `"version-string"` to the new version (no `v` prefix).

### Step 5: Update `build/vcpkg_local_ports/libdatadog/portfile.cmake`

Replace the SHA-512 hashes for x64 and x86 Windows builds.

### Step 6: Verify

- Confirm all 8 hashes were updated (6 for CMake SHA-256, 2 for vcpkg SHA-512)
- Confirm version strings match in both files
- Run the Nuke build targets to validate hashes:
  ```bash
  # Linux — validates CMake SHA-256 hashes during configure
  ./tracer/build.sh CompileProfilerNativeSrc

  # Windows — validates vcpkg SHA-512 hashes during install
  .\tracer\build.cmd CompileProfilerNativeSrc
  ```

## Files That May Need Regeneration

These don't contain version strings but may need refreshing after a bump if the exported symbol set or glibc requirements change:

- `tracer/build/_build/NativeValidation/native-libdatadog-symbols-alpine-x64.verified.txt`
- `tracer/build/_build/NativeValidation/native-libdatadog-symbols-alpine-arm64.verified.txt`

CI will fail if the symbol list changes — re-run the Alpine symbol validation step and accept the new snapshot.

## Optional Updates (doc-comment SHAs)

Several files under `tracer/src/Datadog.Trace/LibDatadog/` have XML doc comments linking to a specific libdatadog git SHA (e.g. `60583218a8de6768f67d04fcd5bc6443f67f516b`). These are informational only and can be updated for traceability:
- `VecU8.cs`, `ByteSlice.cs`, `CharSlice.cs`, `Error.cs`
- `ServiceDiscovery/ResultTag.cs`, `ServiceDiscovery/TracerMemfdHandleResult.cs`

## Related Files (no changes needed, but useful context)

- `profiler/Directory.Build.targets` — Windows link dependencies for libdatadog
- `tracer/build/_build/Build.Steps.cs` — copies libdatadog binary to monitoring home
- `tracer/build/_build/Build.Profiler.Steps.cs` — profiler build steps; glibc version expectations for Alpine
- `.gitlab/one-pipeline.locked.yml` — CI pipeline (auto-generated, don't edit manually)
- `vcpkg.json` (root) — declares dependency on libdatadog (no version pin here)
- `vcpkg-configuration.json` — overlay ports config

## Important Notes

- The CMake file uses **SHA-256** hashes; the vcpkg portfile uses **SHA-512** hashes. Both are published in the GitHub release notes.
- The CMake version has a `v` prefix (`v32.0.0`); vcpkg does not (`30.0.0`).
- If a release doesn't have all platform artifacts, the bump may need to wait or be partial.
- After bumping, CI will validate the hashes on all platforms. Hash mismatches cause clear build failures.
