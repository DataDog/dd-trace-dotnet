---
name: bump-libdatadog
description: >-
  Update/bump the libdatadog native library version in dd-trace-dotnet. Use when the user asks to
  bump, update, or upgrade libdatadog, or mentions a new libdatadog release version.
---

# Bump libdatadog Version

## Overview

libdatadog is consumed as a prebuilt native binary. There is a **single version pin** to update — set it in
one place and everything else picks it up automatically.

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

1. Get the target version (without a `v` prefix, e.g. `32.0.0`).
2. Edit `build/cmake/FindLibdatadog.cmake`: set `LIBDATADOG_VERSION` and replace the `SHA512_LIBDATADOG*` hashes.
3. Set the same version in `.gitlab/one-pipeline.locked.yml` and the root `vcpkg.json`.
4. Done — there are no other files to touch.

## Verify

Build the solution; if the version pin is correct the native libraries download and link cleanly. No special
hash-validation target is needed — a wrong hash just fails the normal build.
