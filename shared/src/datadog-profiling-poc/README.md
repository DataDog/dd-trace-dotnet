# datadog-profiling-poc

**This is not production code.** It's a proof of concept studying whether
libdatadog's profiling-upload path (the only piece dd-trace-dotnet's native
profiler needs) can be replaced with a small, hand-written C library instead
of the real Rust one. Full design/rationale/decision log is in the plan doc
this was built from (`i-would-like-to-imperative-lemur.md`); this README just
tracks what actually landed vs. what's still simplified.

It is isolated from the production build on purpose: nothing under
`shared/CMakeLists.txt` references this directory, so it is built as its own
standalone CMake project and has zero effect on the real tracer/profiler build
until something is deliberately wired up to link against it (see "Wrapper
changes needed" in the plan doc for that step).

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

**This `CMakeLists.txt` is Linux/non-Windows only and always vendors from
source - it has no Windows branch and is never invoked on Windows.** The real
Windows profiler build is MSBuild/`Datadog.Profiler.Native.Windows.vcxproj`-based,
not CMake, unlike the Linux build this PoC's M3/M4 milestones actually wired
into (`Datadog.Profiler.Native.Linux/CMakeLists.txt`), so there was never a
CMake-on-Windows path to branch for in the first place.

`vcpkg.json` in this directory is a separate, forward-looking manifest for
*that* eventual Windows integration - declaring curl+zstd the same way the
real libdatadog Windows dependency is already declared
(`build/vcpkg_local_ports/libdatadog`), not something this `CMakeLists.txt`
reads or consumes. **It's pinned to the exact same curl 8.22.0 / zstd 1.5.7**
as the FetchContent versions above, via `builtin-baseline` + `overrides`
(vcpkg's current default baseline already happens to resolve to those same
versions - the overrides just guarantee that stays true if the pinned
baseline commit is ever bumped without checking). If you ever bump one
side's version, bump the other to match. Wiring it into the real vcxproj
build is separate, not-yet-done work - most likely referencing vcpkg's
output `.lib`/`.dll` via `AdditionalDependencies`/`AdditionalLibraryDirectories`,
the same way `profiler/Directory.Build.targets` already does for the real
libdatadog Windows dependency.

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
