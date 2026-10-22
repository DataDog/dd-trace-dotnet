# Native Linux glibc compatibility builds

## Purpose

The non-musl native libraries are built on modern Ubuntu with Clang 16 (or newer), but
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
  The same sysroot is used to link the non-musl `dd-dotnet` NativeAOT
  executables (see [NativeAOT `dd-dotnet`](#nativeaot-dd-dotnet)).
  The non-musl `tool_artifacts_tests_linux` jobs also use this image. They
  execute the already-built artifacts and do not link them again.
- **Alpine** builds the musl tracer and profiler artifacts.
- **Universal** builds the shipped native loader and Linux API wrapper as
  musl-based binaries. These artifacts are merged into the glibc package later
  in the pipeline; they do not use the glibc 2.17 sysroot.
- **Alpine** also builds the musl `dd-dotnet` executables, without a sysroot.
- **Debian** is still used for several generated test matrices. Do not assume
  those runs use the sysroot.
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
7. Clang resolves C headers and glibc libraries through `CMAKE_SYSROOT`, while
   C++ headers and runtime inputs come from the harvested Developer Toolset.
8. The completed libraries are checked for their maximum required `GLIBC_*`
   symbol version.

The architecture-specific settings are:

- x86_64: `/sysroot/x86_64-glibc217` and Developer Toolset 11;
- arm64: `/sysroot/aarch64-glibc217` and Developer Toolset 10, because
  Developer Toolset 11 was not released for CentOS 7 arm64.

Selecting the toolchain file is the CMake opt-in. The toolchain sets
`USE_GLIBC217_SYSROOT`; callers must not also enable the Universal/musl mode.

## What is in the sysroot

The harvest contains the compile-time and link-time target SDK, including:

- CentOS 7 glibc and kernel C headers under `/usr/include`;
- the dynamic loader and glibc shared objects;
- glibc linker scripts and non-shared archives;
- startup objects such as `crt1.o`, `crti.o`, and `crtn.o`;
- Developer Toolset compiler support objects such as `crtbeginS.o` and
  `crtendS.o`;
- Developer Toolset C++ headers;
- the selected Developer Toolset's `libstdc++` and `libgcc` inputs;
- a manifest of installed source package versions.

The toolchain sets `CMAKE_SYSROOT`, so both C compilation and linking use the
CentOS 7 root. It uses `-nostdinc++` plus explicit Developer Toolset paths for
C++ headers, and `-B`/`-L` for the selected compiler support objects and static
`libstdc++`/`libgcc`. Host programs such as CMake, Clang, lld, Make, and
Autoconf still run from Ubuntu.

The root CMake configuration fails if `<sys/single_threaded.h>` is visible.
That header and `__libc_single_threaded` were introduced after glibc 2.17; a
reference indicates that host C headers leaked into the target build.

The re2 and libunwind dependencies use CMake `ExternalProject` builds, which
do not inherit the parent toolchain automatically. When
`USE_GLIBC217_SYSROOT` is enabled, their build commands explicitly receive the
selected compiler target, compiler-level sysroot, C/C++ flags, and executable
linker flags. This keeps re2 compilation and libunwind configure probes on the
harvested C and C++ headers as well as the glibc 2.17 libraries.

Libunwind links its shared libraries through libtool. Libtool does not forward
`-B` from `LDFLAGS`, so Clang looks up `crtbeginS.o` without the Developer
Toolset directory and passes the bare filename to lld. `CC` and `CXX` for that
build therefore include `-B` pointing at `GLIBC217_TOOLCHAIN_LIB`.

Developer Toolset 10 and 11 include C++17 filesystem support directly in
`libstdc++.a`. Sysroot builds therefore do not link the legacy
`libstdc++fs.a`, which is not harvested and could otherwise resolve from the
Ubuntu host. Non-sysroot builds retain that library for compatibility with
older local toolchains.

Native artifact validation rejects any dynamic import or export of
`__libc_single_threaded`.

The profiler uses its install RPATH while it is built, restricted to
`$ORIGIN`, so CMake does not embed the temporary directory containing the
imported `libdatadog_profiling.so`.

## NativeAOT `dd-dotnet`

`dotnet publish` does not read the CMake toolchain files. When
`UseGlibc217Sysroot` is set and the RID is `linux-x64` or `linux-arm64`,
`BuildDdDotnet` passes two MSBuild properties instead:

- `SysRoot`, which NativeAOT forwards to clang as `--sysroot`;
- `Glibc217ToolchainLibPath`, the Developer Toolset `lib` directory. The
  `dd-dotnet` project turns it into `-B`/`-L` linker arguments plus
  `-static-libgcc`. The sysroot has no GCC installation tree for clang to
  discover. `devtoolset*/lib/libgcc_s.so` is a linker script
  (`GROUP ( /lib64/libgcc_s.so.1 libgcc.a )`), and the harvest does not include
  `libgcc_s.so.1`, so `-lgcc_s` cannot be resolved from the sysroot.

The sysroot paths in `Build.cs` must match the defaults in
`Glibc217.cmake.*`. NativeAOT keeps its default linker and links its own
`libstdc++compat.a`, so the executable does not depend on `libstdc++`.

`PublishAotCompressed` compresses the published executable with UPX, which
removes its dynamic section. Running `nm` or `readelf` on the shipped file
proves nothing. `BuildDdDotnet` therefore writes a `upx -d` copy of the
published binary to `.nuke/temp/dd-dotnet-uncompressed/<rid>/dd-dotnet`.
`ValidateDdDotnetGlibcCompatibility` checks that copy.

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
DOTNETSDK_VERSION="$(python3 -c \
  'import json; print(json.load(open("global.json"))["sdk"]["version"])')"

docker build \
  --platform linux/arm64 \
  --build-arg DOTNETSDK_VERSION="${DOTNETSDK_VERSION}" \
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

Inside the container, first validate the archive and the standalone native
dependency builds:

```bash
SYSROOT=/sysroot/aarch64-glibc217
TOOLCHAIN=build/cmake/Glibc217.cmake.aarch64

test -f "${SYSROOT}/usr/include/features.h"
test ! -e "${SYSROOT}/usr/include/sys/single_threaded.h"

rm -rf artifacts/glibc217-validation
cmake \
  -S . \
  -B artifacts/glibc217-validation \
  -DCMAKE_C_COMPILER=clang \
  -DCMAKE_CXX_COMPILER=clang++ \
  -DCMAKE_BUILD_TYPE=Release \
  -DCMAKE_TOOLCHAIN_FILE="${TOOLCHAIN}" \
  -DGLIBC217_SYSROOT="${SYSROOT}"

cmake --build artifacts/glibc217-validation --parallel \
  --target re2 libunwind Datadog.Trace.ClrProfiler.Native

nm --with-symbol-versions -D \
  artifacts/native-bin/Datadog.Trace.ClrProfiler.Native/Datadog.Trace.ClrProfiler.Native.so
```

The CMake configure output must report that
`sys/single_threaded.h` was **not** found. Running the compiler with `-E -v`
must show the Clang resource directory, Developer Toolset C++ directories, and
`${SYSROOT}/usr/include`, but not Ubuntu `/usr/include`.

For the complete CI-equivalent path, build the managed loader and run the Nuke
native build, test, and validation targets in the same mounted container.

```bash
./tracer/build.sh \
  CompileManagedLoader \
  BuildNativeTracerHome \
  CompileTracerNativeTests \
  RunTracerNativeTests \
  BuildProfilerHome \
  CompileProfilerNativeTests \
  RunProfilerNativeTests \
  ValidateNativeTracerGlibcCompatibility \
  ValidateNativeProfilerGlibcCompatibility
```

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

### `dd-dotnet` executable

`ValidateDdDotnetGlibcCompatibility` runs after `BuildDdDotnet` in the
non-musl `build_dd_dotnet_linux*` jobs. On the uncompressed copy it:

- applies the same glibc 2.17 ceiling and `GLIBC_PRIVATE` and
  `__libc_single_threaded` checks used for the native libraries;
- allows only `libc.so.6`, `libdl.so.2`, `libm.so.6`, `libpthread.so.0`,
  `librt.so.1` and the architecture's dynamic loader as `DT_NEEDED` entries;
- requires the program interpreter `/lib64/ld-linux-x86-64.so.2` (x64) or
  `/lib/ld-linux-aarch64.so.1` (arm64).

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

### Target header age

The build uses CentOS 7 glibc and kernel headers. This removes host-header ABI
drift, but it also means APIs, constants, and kernel definitions added after
that baseline are intentionally unavailable at compile time.

When newer native code is introduced:

1. keep the glibc symbol check mandatory;
2. run a CentOS 7 package smoke test for native/toolchain changes;
3. add focused runtime tests for APIs affected by conditional header features;
4. use explicit syscall or compatibility implementations only when their
   runtime semantics on the supported kernel baseline are understood.

### Harvest reproducibility

The consumed archives are pinned by SHA-512, so CI detects an archive changing
under an existing hash. Each complete archive also records the installed RPM
versions in `sysroot-packages.txt`. Recreating it is still not fully hermetic
unless the Docker base image and CentOS vault inputs are pinned by digest.

For a refresh, record the base image digest and installed package versions
alongside the new archive hashes. Consider pinning those inputs if sysroot
refreshes become frequent.

### Mixed package provenance

The glibc package combines Ubuntu/sysroot-built tracer and profiler libraries
with Universal/musl loader and wrapper artifacts. Validate both build paths
when changing package assembly, CMake target selection, or native dependency
layout.

Non-musl `dd-dotnet` is built in its own Ubuntu job. It uses the same
sysroot but a separate NativeAOT link path, so changes to the CMake toolchain
files do not apply to it, and the reverse is also true.

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

### CMake reports that `sys/single_threaded.h` is visible

Confirm that:

- the glibc 2.17 toolchain was selected;
- the archive contains `${GLIBC217_SYSROOT}/usr/include/features.h`;
- `CMAKE_SYSROOT` is the harvested root;
- compiler `-E -v` output does not contain Ubuntu `/usr/include`;
- the native CMake build directory was recreated after the toolchain change.

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
- [ ] Confirm C and kernel headers resolve from the harvested sysroot, not the
      Ubuntu host.
- [ ] Confirm `<sys/single_threaded.h>` is not visible.
- [ ] Run tracer/profiler native unit tests.
- [ ] Run `ValidateNativeTracerGlibcCompatibility`.
- [ ] Run `ValidateNativeProfilerGlibcCompatibility`.
- [ ] Run `BuildDdDotnet ValidateDdDotnetGlibcCompatibility` for x64 and arm64.
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
