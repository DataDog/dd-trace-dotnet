# datadog-profiling-poc

**This is not production code.** It's a proof of concept studying whether
libdatadog's profiling-upload path (the only piece dd-trace-dotnet's native
profiler needs) can be replaced with a small, hand-written C library instead
of the real Rust one. Full design/rationale/decision log is in the plan doc
this was built from (`i-would-like-to-imperative-lemur.md`); this README just
tracks what actually landed vs. what's still simplified.

**This is wired into both real profiler builds, not just standalone.** The
wrapper files under `profiler/src/ProfilerEngine/Datadog.Profiler.Native/`
(`Profile.cpp`, `AgentProxy.hpp`, `ExporterBuilder.cpp`, `Exporter.cpp`,
`Tags.cpp`, `TagsImpl.hpp`, `FileSaver.hpp`, `EncodedProfile.hpp`,
`ProfileImpl.hpp`, `SuccessImpl.hpp`, `FfiHelper.h`/`.cpp`) call this
library's API instead of real libdatadog's, on every platform - see "Windows"
and the standalone build instructions below for how each platform actually
gets this library's code compiled and linked in.

## Building standalone

```
cmake -S . -B build
cmake --build build
```

No system packages needed - curl and zstd are vendored from source via CMake
`FetchContent` (see `CMakeLists.txt`), built HTTP-only/no-TLS and
static-linked in. This is the same mechanism this repo already uses to vendor
libunwind for the real native profiler, just applied to curl/zstd too. Only
real requirement: network access at first configure time to fetch the two
tarballs (cached under `build/_deps/` afterwards).

**This `CMakeLists.txt` itself is Linux/non-Windows only and always vendors
curl/zstd from source - it has no Windows branch and is never invoked on
Windows.** The real Windows profiler build is MSBuild/vcxproj-based, not
CMake, so there was never a CMake-on-Windows path to branch for. Windows gets
this library a completely different way:

- This library's `.c` files are compiled directly as additional `<ClCompile>`
  items inside `profiler/src/ProfilerEngine/Datadog.Profiler.Native/Datadog.Profiler.Native.vcxproj`
  (the same static-lib project that already compiles `Profile.cpp`,
  `FfiHelper.cpp`, `CrashReporting.cpp`, etc.), each forced to
  `CompileAs=CompileAsC`/`LanguageStandard_C=stdc11` since that project's
  C++ settings (`stdcpp20`, `ConformanceMode`) don't apply to C. This
  project's own `AdditionalIncludeDirectories` was extended with
  `shared\src\datadog-profiling-poc\include` so `FfiHelper.h`'s
  `#include "datadog_poc/common.h"` (and this library's own internal
  includes) resolve. Being a *static* lib, it doesn't need curl/zstd/its own
  symbols resolved at this stage - unresolved externals here are normal and
  get resolved later, at whichever project finally links this static lib in
  (`Datadog.Profiler.Native.Windows.vcxproj`'s DLL, or
  `Datadog.Profiler.Native.Tests.vcxproj`'s test exe - both already
  `ProjectReference` it).
- curl/zstd themselves are declared in the **root** `vcpkg.json` (alongside
  the existing `libdatadog` entry) - not a manifest local to this directory,
  since `VcpkgEnableManifest` (set repo-wide in `profiler/Directory.Build.props`)
  auto-discovers the nearest `vcpkg.json` by walking up from each project
  file, and the root one is the only one any actual `.vcxproj` will ever find.
  Pinned via `builtin-baseline` + `overrides` (both including an explicit
  `port-version` - vcpkg's version files can carry more than one port-version
  per version string, and omitting it silently defaults to 0, which isn't
  always the one that exists) to **the exact same curl 8.19.0 / zstd 1.5.7**
  vendored on the Linux/CMake side above - bump both together if you ever
  bump one. Note curl is deliberately *not* on the latest available version
  (8.22.0): curl's own `cmake_minimum_required` jumped from a `3.7...3.16`
  range to a flat `3.18` starting at 8.20.0, and this repo's CI pins CMake
  3.13.4 - 8.19.0 is the newest release still building under that. This is
  exactly the mechanism that already
  resolves real libdatadog's headers/lib with zero explicit
  `AdditionalIncludeDirectories`/`AdditionalDependencies` anywhere in this
  repo's `.vcxproj`/`.props`/`.targets` files (confirmed by their absence) -
  curl/zstd get the same automatic treatment once declared.

**Caveat, stated plainly: none of the Windows-side changes above have been
compile-tested.** This session has no Windows environment and no vcpkg
installed to verify against - the design is based on reading this repo's
actual `.vcxproj`/`Directory.Build.props`/`.targets` files closely and
matching their existing, working patterns (especially how real libdatadog
itself is wired with almost no explicit project configuration), not on a
green build. The first real signal will be the next Windows CI run.

`build/manual_smoke` is a standalone driver (mirrors libdatadog's own
`examples/ffi/profiles.c`) that creates a profile, adds a couple of synthetic
samples, serializes, and either dumps the result to a file or POSTs it to an
agent URL - see its `--help` / source for usage.

## Known simplifications (deliberate, not bugs)

- **No content-based deduplication** of Mapping/Function/Location - every
  occurrence gets a fresh pprof id. Valid pprof, just larger than necessary.
  Only string interning (mandatory) is actually deduplicated, via a linear
  scan (`pprof_encode.c`) - fine at the sample counts a PoC deals with, would
  want a real hash map before this sees serious traffic.
- **`ddog_prof_profile_set_endpoint`/`add_endpoint_count`** validate their
  arguments and store the data, but it is **not yet reflected in the encoded
  pprof output** - real libdatadog injects a derived label onto matching
  samples / reports counts alongside the upload. See `profile.h`.
- **`ddog_prof_profile_add_upscaling_rule_proportional`/`poisson`** validate
  their arguments and return success, but the rule is **not stored or
  applied anywhere** - sample values are never rescaled. See `profile.h`.
- **Additional files** (`ddog_prof_exporter_send_blocking`'s `files` param)
  are attached uncompressed; real libdatadog zstd-compresses each one
  individually. Untested either way - nothing in the verification path
  (M1-M4) sends a non-empty files list.
- **Agentless mode** (`ddog_prof_endpoint_agentless`) is implemented (URL
  construction + `dd-api-key` header) but never exercised by any milestone -
  only agent mode is verified end to end.
- `ddog_last_error_message()` and the multipart timestamp formatter
  (`format_rfc3339` in `exporter.c`) both use plain thread-local/non-reentrant
  state (`_Thread_local` buffer, `gmtime()`); fine as long as one exporter/one
  thread drives calls at a time, which matches how the wrapper this targets
  actually uses it.
- `curl_global_init()` is guarded by a plain `bool`, not a real once-guard -
  not safe if two threads call `ddog_prof_exporter_new` for the very first
  time concurrently.
