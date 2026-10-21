# Native Linux glibc compatibility builds

## Purpose

The non-musl native libraries are built on Ubuntu 22.04 with Clang 16, but
must still load on distributions whose system C library is glibc 2.17, such
as CentOS 7.

Building on a recent distribution gives the build access to maintained tools
and current .NET SDKs. Linking against a frozen glibc 2.17 sysroot prevents
the resulting libraries from accidentally requiring newer versioned glibc
symbols.

This document describes:

- which artifacts use this build path;
- how the sysroot is produced and consumed;
- what the automated validation proves;
- known gaps and how to investigate them;
- how to refresh and test the sysroot.

## Build modes

Linux artifacts do not all use the same libc build path.

- **Ubuntu with the glibc 2.17 sysroot** builds the non-musl
  `Datadog.Tracer.Native.so` and `Datadog.Profiler.Native.so` artifacts. Native
  unit tests in those jobs use the same CMake build directory and toolchain.
- **Alpine** builds the musl tracer and profiler artifacts.
- **Universal** builds the shipped native loader and Linux API wrapper as
  musl-based binaries. These artifacts are merged into the glibc package later
  in the pipeline; they do not use the glibc 2.17 sysroot.
- **Debian** is still used for `dd-dotnet`, tool-artifact tests, and several
  generated test matrices. Do not assume those artifacts use the sysroot.
- **CentOS 7** remains the source of the harvested sysroot and a runtime smoke
  test target. It is not the normal compiler image for the tracer or profiler.

When reviewing a compatibility result, first identify which of these paths
produced the artifact.

## Build flow

The glibc build follows this path:

1. [`glibc217-sysroot.harvest.dockerfile`](../../tracer/build/_build/docker/glibc217-sysroot.harvest.dockerfile)
   installs CentOS 7 glibc development files and a Developer Toolset compiler.
2. The harvest stage exports an architecture-specific directory. It is
   archived and uploaded to the `build-dependencies` storage container.
3. [`ubuntu.dockerfile`](../../tracer/build/_build/docker/ubuntu.dockerfile)
   downloads the archive, verifies its SHA-512 digest, and extracts it below
   `/sysroot`.
4. The Ubuntu image sets `UseGlibc217Sysroot=true`.
5. The Nuke build converts that setting into the appropriate
   `CMAKE_TOOLCHAIN_FILE` argument.
6. CMake configures the shared native build directory with
   [`Glibc217.cmake.x86_64`](../../build/cmake/Glibc217.cmake.x86_64) or
   [`Glibc217.cmake.aarch64`](../../build/cmake/Glibc217.cmake.aarch64).
7. The linker resolves glibc and C++ runtime inputs from the harvested files.
   The completed libraries are then checked for their maximum required
   `GLIBC_*` symbol version.

The architecture-specific settings are:

- x86_64: `/sysroot/x86_64-glibc217` and Developer Toolset 11;
- arm64: `/sysroot/aarch64-glibc217` and Developer Toolset 10, because
  Developer Toolset 11 was not released for CentOS 7 arm64.

Selecting the toolchain file is the CMake opt-in. The toolchain sets
`USE_GLIBC217_SYSROOT`; callers must not also enable the Universal/musl mode.

## What is in the sysroot

The harvest contains the link-time glibc inputs, including:

- the dynamic loader and glibc shared objects;
- glibc linker scripts and non-shared archives;
- startup objects such as `crt1.o`, `crti.o`, and `crtn.o`;
- Developer Toolset C++ headers;
- the selected Developer Toolset's `libstdc++` and `libgcc` inputs.

The toolchain uses `--sysroot`, `-B`, and `-L` for linker resolution and links
`libstdc++` and `libgcc` statically. It also supplies
`shared/src/glibc217-compat` for `__libc_single_threaded`, which is referenced
by newer C++ headers but is not provided by glibc 2.17.

The toolchain deliberately does **not** set `CMAKE_SYSROOT`. C headers therefore
come from Ubuntu, while C++ headers come from the harvested Developer Toolset.
The old glibc files constrain link-time symbol resolution, but this is not
equivalent to compiling against a complete glibc 2.17 SDK. The consequences
are covered under [Known risks and follow-ups](#known-risks-and-follow-ups).

The re2 and libunwind dependencies use CMake `ExternalProject` builds, which
do not inherit the parent toolchain automatically. When
`USE_GLIBC217_SYSROOT` is enabled, their build commands explicitly receive the
selected compiler target, C/C++ flags, and executable linker flags. This keeps
re2 compilation and libunwind configure probes on the harvested C++ headers
and glibc 2.17 link sysroot.

Developer Toolset 10 and 11 include C++17 filesystem support directly in
`libstdc++.a`. Sysroot builds therefore do not link the legacy
`libstdc++fs.a`, which is not harvested and could otherwise resolve from the
Ubuntu host. Non-sysroot builds retain that library for compatibility with
older local toolchains.

The `__libc_single_threaded` compatibility shim is compiled with hidden
visibility and its stub declaration marks references hidden as well. Native
artifact validation checks `nm -D --defined-only` and fails if this internal
symbol is ever exposed from a shipped library.

The profiler uses its install RPATH while it is built, restricted to
`$ORIGIN`, so CMake does not embed the temporary directory containing the
imported `libdatadog_profiling.so`.

## Refreshing the sysroot

Refresh both architectures together so their source and validation history
remain clear.

### 1. Produce the x86_64 harvest

From the repository root:

```bash
docker build \
  --platform linux/amd64 \
  --target harvest \
  --output type=local,dest=./glibc217-sysroot-out \
  --file tracer/build/_build/docker/glibc217-sysroot.harvest.dockerfile \
  tracer/build/_build/docker

tar -czf glibc217-sysroot-x86_64.tar.gz \
  -C ./glibc217-sysroot-out .
sha512sum glibc217-sysroot-x86_64.tar.gz
```

### 2. Produce the arm64 harvest

On an x86_64 host, install binfmt support first if it is not already present:

```bash
docker run --privileged --rm tonistiigi/binfmt --install arm64
```

Then build the arm64 harvest:

```bash
docker build \
  --platform linux/arm64 \
  --build-arg DEVTOOLSET_VERSION=10 \
  --target harvest \
  --output type=local,dest=./glibc217-sysroot-out-aarch64 \
  --file tracer/build/_build/docker/glibc217-sysroot.harvest.dockerfile \
  tracer/build/_build/docker

tar -czf glibc217-sysroot-aarch64.tar.gz \
  -C ./glibc217-sysroot-out-aarch64 .
sha512sum glibc217-sysroot-aarch64.tar.gz
```

### 3. Publish and pin the archives

Upload both archives to:

```text
https://apmdotnetbuildstorage.blob.core.windows.net/build-dependencies/
```

Keep the names expected by `ubuntu.dockerfile`:

- `glibc217-sysroot-x86_64.tar.gz`
- `glibc217-sysroot-aarch64.tar.gz`

Update the architecture-specific `SYSROOT_SHA512` values in
`ubuntu.dockerfile`. A sysroot change is incomplete until the uploaded
archives, hashes, Docker image build, native validation, and old-distribution
runtime smoke test all agree.

### 4. Iterate without uploading

For local work on the harvest itself, build the Ubuntu `base` stage with
`SkipGlibc217SysrootFetch=true`, then mount the local output at the expected
architecture path:

```bash
docker build \
  --platform linux/arm64 \
  --build-arg SkipGlibc217SysrootFetch=true \
  --target base \
  --tag ubuntu217-local \
  --file tracer/build/_build/docker/ubuntu.dockerfile \
  tracer/build/_build

docker run --rm -it \
  --volume "$(pwd)/glibc217-sysroot-out-aarch64:/sysroot/aarch64-glibc217" \
  --volume "$(pwd):/project" \
  --workdir /project \
  ubuntu217-local \
  bash
```

Use `/sysroot/x86_64-glibc217` for x86_64. The toolchain also accepts
`-DGLIBC217_SYSROOT=/path/to/sysroot` for direct CMake reproduction.

Always use a clean CMake build directory after adding, removing, or changing a
toolchain file. CMake reads a toolchain only during the first configuration;
reusing an existing cache can silently retain the host linker settings. The
root `CMakeLists.txt` contains a guard for the common stale-cache case.

## Validation

Compatibility is established in layers. No single check proves all of them.

### Native build and unit tests

The Ubuntu jobs compile the tracer and profiler native tests with the same
toolchain used by the corresponding library. These tests prove that the
libraries build and work on the Ubuntu build image.

They do not prove that the libraries load on glibc 2.17.

### glibc symbol-version check

`ValidateNativeTracerGlibcCompatibility` and
`ValidateNativeProfilerGlibcCompatibility` call `nm --with-symbol-versions -D`
through
[`NativeValidationHelper`](../../tracer/build/_build/NativeValidation/NativeValidationHelper.cs).
The current implementation rejects imported `GLIBC_*` versions above the
configured compatibility ceiling. The ceiling for the Datadog-built tracer and
profiler libraries is 2.17; requiring an older version is valid. It also rejects
`GLIBC_PRIVATE` requirements and reports the offending symbols.

This catches a common regression: compiling or linking code that introduces a
reference such as `function@GLIBC_2.18`.

The check does not currently:

- validate every `DT_NEEDED` library against a CentOS 7 installation;
- apply a version policy to `GLIBCXX_*`, `CXXABI_*`, or `GCC_*`;
- snapshot undefined symbols for the glibc artifacts;
- detect API or structure-layout assumptions introduced solely by newer C
  headers;
- execute the library with the glibc 2.17 runtime.

Undefined-symbol snapshots are currently applied on the Alpine path, not as
part of the glibc ceiling check.

### Profiler runtime search path

`ValidateNativeProfilerGlibcCompatibility` also inspects the profiler's dynamic
section with `readelf`. It requires exactly one RPATH/RUNPATH entry whose value
is `$ORIGIN`. This ensures `libdatadog_profiling.so` is loaded from beside the
profiler without preserving a temporary CMake build directory.

### Old-distribution runtime smoke tests

[`SmokeTestScenarios.cs`](../../tracer/build/_build/SmokeTests/SmokeTestScenarios.cs)
contains CentOS 7 installer scenarios. They install the packaged artifacts and
run applications inside CentOS 7 images. This is the strongest existing
end-to-end evidence that the combined package loads on the target runtime.

These scenarios are not a substitute for the ELF checks, and they may not run
on every pull request. A sysroot, toolchain, or native dependency change should
explicitly run at least one relevant CentOS 7 scenario. See
[Run smoke tests locally](CI/RunSmokeTestsLocally.md).

### Manual ELF inspection

When a compatibility issue is suspected, inspect the final packaged library,
not only an intermediate static archive:

```bash
nm --with-symbol-versions -D Datadog.Tracer.Native.so
readelf --version-info Datadog.Tracer.Native.so
readelf --dynamic Datadog.Tracer.Native.so
readelf --program-headers Datadog.Tracer.Native.so
```

Repeat for `Datadog.Profiler.Native.so`. Check for:

- a `GLIBC_*` requirement newer than 2.17;
- unexpected `GLIBC_PRIVATE`, `GLIBCXX_*`, `CXXABI_*`, or `GCC_*` needs;
- unexpected dynamic dependencies;
- RPATH or RUNPATH entries that refer to build-machine paths;
- executable-stack or other unexpected ELF program-header flags.

Use `-Wl,-verbose`, `-Wl,--trace`, or the equivalent lld options when the
problem is which archive or shared object the linker selected.

## Known risks and follow-ups

The items below should be reviewed before treating the sysroot as a hermetic
cross-toolchain.

### Ubuntu C headers

The build uses Ubuntu C headers with CentOS 7 link-time glibc files. Symbol
version validation catches newer versioned glibc imports, but it cannot catch
all differences in constants, inline implementations, structure layouts, or
kernel-facing behavior.

Ways to reduce the risk:

1. keep the glibc symbol check mandatory;
2. run a CentOS 7 package smoke test for native/toolchain changes;
3. add focused runtime tests for APIs affected by newer headers;
4. evaluate harvesting and using the matching CentOS 7 C headers if a header
   compatibility issue appears.

### Harvest reproducibility

The consumed archives are pinned by SHA-512, so CI detects an archive changing
under an existing hash. Recreating an archive is less hermetic: the Docker base
tag, CentOS vault metadata, and Developer Toolset packages are not recorded in
a manifest of exact source package versions.

For a refresh, record the base image digest and installed package versions
alongside the new archive hashes. Consider pinning those inputs if sysroot
refreshes become frequent.

### Mixed package provenance

The glibc package combines Ubuntu/sysroot-built tracer and profiler libraries
with Universal/musl loader and wrapper artifacts. Validate both build paths
when changing package assembly, CMake target selection, or native dependency
layout.

`dd-dotnet` is built separately on Debian and is not covered by the CMake
sysroot validation.

## Troubleshooting

### A library requires a newer `GLIBC_*` version

1. Run `nm --with-symbol-versions -D` on the final shared library.
2. Find the object or archive that introduced the symbol with linker tracing.
3. Confirm that the CMake configure command used the expected
   `Glibc217.cmake.*` file.
4. Delete the native CMake build directory and configure again.
5. Check external-project compile and link commands for host search paths.

Do not add a symbol to an allowlist merely because it is present on the Ubuntu
builder. The target runtime must provide it.

### CMake reports that the toolchain was not sourced

The native build directory was configured before the toolchain argument was
added. Delete the directory and rebuild. Changing `CMAKE_TOOLCHAIN_FILE` in an
existing cache is not supported.

### `__libc_single_threaded` is unresolved

Confirm that:

- the glibc 2.17 toolchain was selected;
- `FindGlibc217Compat.cmake` created `glibc217-compat`;
- the final shared library or test executable links that object library
  directly, rather than relying on an earlier static archive;
- the compatibility include directory shadows the host
  `sys/single_threaded.h`.

### The linker selects a host C++ archive

Use linker tracing and inspect `clang++ -print-search-dirs`. Verify that the
Developer Toolset `-B` and `-L` paths precede host GCC paths and that every
explicitly linked archive is present in the harvest.

### Native tests pass but CentOS 7 fails

Treat this as a missing validation layer, not evidence that the runtime is
wrong. Compare the final package's version needs and dynamic dependencies with
the CentOS 7 filesystem, then reduce the failure to a focused `dlopen` or API
test in the old-distribution image.

### Ubuntu-only LDAP or sanitizer failures

The OpenLDAP compatibility symlink and sanitizer runtime settings in
`ubuntu.dockerfile` support tests running on the build image. They are separate
from the glibc 2.17 contract. Diagnose them as build-image runtime issues unless
the final ELF inspection shows that they changed a shipped dependency.

## Review checklist

For changes to the Ubuntu image, sysroot, toolchains, native dependencies, or
Linux package assembly:

- [ ] Confirm which artifacts and architectures are affected.
- [ ] Build the Ubuntu image for x86_64 and arm64 as applicable.
- [ ] Start from a clean native CMake build directory.
- [ ] Run tracer/profiler native unit tests.
- [ ] Run `ValidateNativeTracerGlibcCompatibility`.
- [ ] Run `ValidateNativeProfilerGlibcCompatibility`.
- [ ] If native dependencies changed, inspect final `DT_NEEDED` and version
      requirements.
- [ ] Confirm C++ archives and external projects did not fall back to host
      headers or libraries.
- [ ] Run at least one CentOS 7 package smoke scenario.
- [ ] Re-run the Universal loader/wrapper validation if package composition or
      shared native build logic changed.
- [ ] Re-run Alpine validation if common native code or dependencies changed.
- [ ] Record new archive hashes and source package versions for a sysroot
      refresh.
