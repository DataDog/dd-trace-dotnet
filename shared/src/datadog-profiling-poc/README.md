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
  includes) resolve.
- **No vcpkg, no CMake, and no libcurl at all on Windows** - deliberately.
  This repo's Windows CI machines have vcpkg pre-installed at a fixed,
  frozen version outside this repo's control (not bootstrapped by the Nuke
  targets that download a fresh copy for other platforms/local dev) - its
  bundled registry snapshot caps out at ancient package versions (curl
  ~7.6x-8.11 depending on exactly which snapshot), with no way to get newer
  packages through it without touching CI machine provisioning, which is
  out of scope here. Rather than fight that, Windows sources its two
  dependencies differently from Linux entirely:
  - **zstd**: `vendor/zstd/{zstd.c,zstd.h,zstd_errors.h}` is zstd's own
    official single-file amalgamation (generated via zstd's
    `build/single_file_libs/create_single_file_library.sh` at the same
    1.5.7 pinned on the Linux/CMake side - regenerate from that script if
    ever bumping the version, and bump both platforms together). Verified
    correct via zstd's own `build_library_test.sh` roundtrip test, not just
    by generating it. `zstd.c` is compiled as one more `<ClCompile>` item
    right alongside this library's own files; `vendor\zstd` was added to
    `AdditionalIncludeDirectories` so `zstd_compress.c`'s `#include <zstd.h>`
    resolves. Linux is unaffected - it still vendors zstd via CMake
    `FetchContent` as before; this amalgamation exists only for Windows.
  - **HTTP transport**: `exporter_win.c` reimplements the same
    `ddog_prof_exporter_*` functions as `exporter.c` (the Linux/libcurl
    version) using WinHTTP instead - `winhttp.lib`/`winhttp.dll` ship with
    every Windows install since XP, and Microsoft documents WinHTTP (not
    WinINet) as the recommended choice for services/non-interactive
    processes, which matches how this library gets loaded into arbitrary
    customer processes. No libcurl anywhere on Windows as a result. Linked
    via `#pragma comment(lib, "winhttp.lib")` inside `exporter_win.c`
    itself, so no `AdditionalDependencies` edit was needed. The multipart
    body framing is hand-rolled to byte-for-byte match what `exporter.c`
    produces via libcurl's `curl_mime_*` (including falling back to
    `application/octet-stream` for the profile part, matching libcurl's own
    default when no type is set and the filename's extension isn't
    recognized), so the Agent/`MockDatadogAgent` see identical wire bytes
    regardless of platform. `exporter.c` and `exporter_win.c` intentionally
    duplicate a handful of portable helpers (event.json building, tag
    string building, RFC3339 formatting) rather than share them, to avoid
    touching the already-tested `exporter.c`.
  - The root `vcpkg.json` now declares only `libdatadog` (resolved via its
    overlay port, unaffected by any of the above) - the `curl`/`zstd`
    entries and their version `overrides` that an earlier iteration of this
    work added have been removed as unnecessary.

**Caveat, stated plainly:** this session has no Windows environment to
compile-test against, but the Windows-only pieces above (`exporter_win.c`,
the vendored `zstd.c`) were cross-compiled and linked into a real PE32+ DLL
using a MinGW-w64 toolchain and its bundled `winhttp.h`/`libwinhttp.a` against
every other `.c` file in this library, with zero missing symbols - stronger
signal than a docs-only review, though still not the real MSVC toolchain.
The WinHTTP API usage itself (whole-body-in-one-call via `WinHttpSendRequest`,
`WinHttpCrackUrl` for URL parsing, the services-safety recommendation) is
sourced from Microsoft's own current documentation, not from memory. The vcxproj
edits (`<ClCompile>`/`AdditionalIncludeDirectories` entries) are unverified
beyond visual inspection - the first real signal will be the next Windows CI
run.

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
