---
name: bump-libdatadog
description: >-
  Update/bump the libdatadog native library version in dd-trace-dotnet. Use when the user asks to
  bump, update, or upgrade libdatadog, or mentions a new libdatadog release version.
---

# Bump libdatadog Version

## Overview

The native library is consumed as prebuilt binaries from GitHub releases of
[**`DataDog/libdatadog-dotnet`**](https://github.com/DataDog/libdatadog-dotnet) — the .NET-specific
distribution of libdatadog (a minimal feature preset: `profiling`, `crashtracker`, `symbolizer`,
`library-config`). This is **not** upstream `DataDog/libdatadog`.

> **Version scheme:** the pinned version is libdatadog-dotnet's **own** release version (e.g. `v1.3.5`),
> which is distinct from the upstream libdatadog version it is built from (tracked by `LIBDATADOG_VERSION`
> in that repo — e.g. libdatadog-dotnet `v1.3.5` is built from upstream libdatadog `v32.0.0`). Pin the
> **libdatadog-dotnet** version here, not the upstream one.

There is a **single version pin** to update — everything in the build flows from one file, so a bump is
a one-file change:

| Platform | File | Hash type | Version format |
|----------|------|-----------|----------------|
| All platforms | `build/cmake/FindLibdatadog.cmake` | SHA-512 | `<MAJOR>.<MINOR>.<PATCH>` (no `v` prefix) |

The Windows vcpkg port has no version pin of its own — it inherits the version (and SHA-256 hashes) from
the root `vcpkg.json`, so there is nothing to edit under `build/vcpkg_local_ports/`.

## Files to Modify

### The only file you need to edit — `build/cmake/FindLibdatadog.cmake`

Update:
- `LIBDATADOG_VERSION` — the version, with **no `v` prefix** (e.g. `32.0.0`).
- the `SHA512_LIBDATADOG*` values — this file uses **SHA-512** hashes (copy them from the release page).

Then propagate the same version so the rest of the build stays in sync:
- **`.gitlab/one-pipeline.locked.yml`** — bump the `libdatadog` version field here too so CI uses the new binary.
- **`vcpkg.json`** (repo root) — set the `libdatadog` version pin here.

The Windows vcpkg port under `build/vcpkg_local_ports/libdatadog/` reads its version from the root
`vcpkg.json`, so you don't edit it directly. (It uses **SHA-256** hashes, which vcpkg resolves for you.)

## Step-by-Step Procedure

### Step 1: Determine target version

Ask the user for the target version, or check the latest release:
```
https://github.com/DataDog/libdatadog-dotnet/releases
```

### Step 2: Get hashes from the release page

The release notes publish checksums. You only need the **SHA512 checksums** section → these are the
hashes for `FindLibdatadog.cmake` (the single file you pin). Either:

1. Visit `https://github.com/DataDog/libdatadog-dotnet/releases/tag/v<VERSION>` and copy from the SHA512 section, or
2. Run the helper script which fetches them via the GitHub API:

```bash
bash .claude/skills/bump-libdatadog/scripts/fetch-release-hashes.sh <VERSION>
```

Where `<VERSION>` is without any `v` prefix (e.g. `1.3.5`).

### Step 3: Update `build/cmake/FindLibdatadog.cmake`

1. Update `LIBDATADOG_VERSION` to `"<VERSION>"` (no `v` prefix)
2. Replace each `SHA512_LIBDATADOG*` value with the new SHA-512 hash from the script output

### Step 4: Update `.gitlab/one-pipeline.locked.yml`

Bump the `libdatadog` version field in this file by hand so CI uses the new binary.

### Step 5: Update `vcpkg.json` (repo root)

Set the `libdatadog` version pin in the root `vcpkg.json`. The Windows vcpkg port reads its version and
its SHA-256 hashes from here, so you don't touch the port files under `build/vcpkg_local_ports/`.

### Step 6: Verify

- Confirm the single version pin and its SHA-512 hashes were updated.
- Build locally; CI will validate the hash on all platforms. Hash mismatches cause clear build failures.

## Files That May Need Regeneration

These don't contain version strings but may need refreshing after a bump if the exported symbol set or glibc requirements change:

- `tracer/build/_build/NativeValidation/native-libdatadog-symbols-alpine-x64.verified.txt`
- `tracer/build/_build/NativeValidation/native-libdatadog-symbols-alpine-arm64.verified.txt`

CI will fail if the symbol list changes — re-run the Alpine symbol validation step and accept the new snapshot.

## Optional Updates (doc-comment SHAs)

Several files under `tracer/src/Datadog.Trace/LibDatadog/` have XML doc comments linking to a specific upstream libdatadog git SHA (e.g. `60583218a8de6768f67d04fcd5bc6443f67f516b`). These are informational only and can be updated for traceability (use the upstream SHA the libdatadog-dotnet release was built from):
- `VecU8.cs`, `CharSlice.cs`, `Error.cs`
- `ServiceDiscovery/ResultTag.cs`, `ServiceDiscovery/TracerMemfdHandleResult.cs`

## Related Files

- `profiler/Directory.Build.targets` — Windows link dependencies for libdatadog
- `tracer/build/_build/Build.Steps.cs` — copies libdatadog binary to monitoring home
- `tracer/build/_build/Build.Profiler.Steps.cs` — profiler build steps; glibc version expectations for Alpine
- `.gitlab/one-pipeline.locked.yml` — CI pipeline; bump the `libdatadog` version field here when you bump
- `vcpkg.json` (root) — declares the libdatadog dependency and holds its version pin
- `vcpkg-configuration.json` — overlay ports config

## Verify

- `FindLibdatadog.cmake` uses **SHA-512** hashes — there are no SHA-256 hashes to manage.
- The version string has **no `v` prefix** anywhere (`1.3.5`).
- There is a single version pin, so there is nothing to keep in lockstep across platforms.
- After bumping, CI validates the hash on all platforms. Hash mismatches cause clear build failures.
