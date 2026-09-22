This directory is vendored from [`dotnet/runtime`](https://github.com/dotnet/runtime) via the
`UpdateVendoredCode` Nuke target (`tracer/build/_build/UpdateVendors/VendoredDependency.cs`), the
same machinery used for every other third-party dependency under `shared/src/native-lib/`. Run
`tracer\build.cmd UpdateVendoredCode` to refresh it. **`VendoredDependency.cs` is the source of
truth** for exactly which files are kept and which local patches are applied — this file is a
summary, not the definition.

## Current pin

- Tag: `v11.0.0-rc.1.26425.128`
- Commit: `ab19415702aa8139d5369e47c73edb47343c34ad`

There is no further pinning beyond the tag URL (the same is true of every other vendored
dependency) — if the tag is ever force-moved upstream, the recorded commit above is the only way
to detect it.

## What's vendored, and from where

Two independent upstream directories, registered as two separate `VendoredDependency` entries so
each gets its own download/patch/replace cycle:

- **`coreclr/`** ← `src/coreclr/{inc,pal/inc,pal/prebuilt}` — the CoreCLR Platform Adaptation
  Layer and public headers (`cor.h`, `corhdr.h`, `corhlpr.h`, `corprof.h`, ...). Restricted via
  `onlyIncludePaths` to `inc/`, `pal/inc/`, `pal/prebuilt/`, excluding `inc/CrstTypeTool/` and
  `inc/genheaders/` (tooling we don't need). `inc/clrversion.h` includes a generated
  `runtime_version.h` that we do not vendor and never will — it's dead weight we don't compile,
  not a bug.
- **`minipal/`** ← `src/native/minipal` — a sibling of `coreclr/`, not nested inside it, because
  it comes from a different upstream root (`src/native/`, not `src/coreclr/`) and because
  `UpdateVendors` wipes each `libraryName` directory wholesale before replacing it; nesting it
  under `coreclr/` would make the result depend on registration order. Needed because
  `pal/inc/pal.h` includes `<minipal/utils.h>` and `pal/inc/pal_mstypes.h` includes
  `<minipal/guid.h>`, both unconditionally.

Both are vendored to `shared/src/native-lib/dotnet-runtime` (this directory), so `minipal/utils.h`
resolves via an include root that is the *parent* of `minipal/` — see the extra include directory
in `build/cmake/FindCoreclr.cmake` and the Windows vcxprojs' `LIB_INCLUDES`/`AdditionalIncludeDirectories`/`IncludePath`.

## Local patches

Applied by `PatchCoreClrFile` (coreclr) / `PatchMinipalFile` (minipal) in `VendoredDependency.cs`
on every refresh, each guarded by `ReplaceOrThrow` so a patch whose anchor text has moved upstream
fails the vendoring run loudly instead of silently no-op'ing:

- **`minipal/guid.h`** — adds `#define GUID_DEFINED` after the non-Windows `GUID` typedef.
  Upstream never sets this macro there (unlike Windows' own `guiddef.h`, and unlike the v7 PAL's
  `pal_mstypes.h`, which defined `GUID` itself and set it). Our
  `shared/src/native-src/dd_guid.h` guards its own `struct GUID` definition on
  `#if !defined(GUID_DEFINED)`; without this patch that guard never trips and collides with this
  typedef (`error: definition of type 'GUID' conflicts with typedef of the same name`).
- **`inc/corhlpr.cpp`** — restores `#ifdef _BLD_CLR` around `#include "utilcode.h"`. Upstream
  dropped this guard; without it, `utilcode.h` transitively pulls in ~15 headers we don't vendor
  (`dn_xxhash.h`, `cdacdata.h`, `crsttypes_generated.h`, `<minipal/*>` beyond what we vendor,
  `clr_std/*`, ...). We compile this file directly (`tracer/src/Datadog.Tracer.Native/il_rewriter.cpp`
  does `#include <corhlpr.cpp>`) and we never define `_BLD_CLR` (we're not building the real CLR),
  so restoring the guard is equivalent to deleting the include for our build.
- **`pal/prebuilt/idl/corprof_i.cpp`** — adds `bool g_arm64_atomics_present = false;` before the
  `#include <rpc.h>` block. This MIDL-generated file is the one file in the vendored tree we
  actually compile, and it's the only place we can give this ARM64-atomics-dispatch symbol
  (referenced by `pal.h`) a definition, since we don't compile the file that would normally
  provide one.
- **`pal/inc/rt/sal.h`** — comments out the bare `#define __valid` / `#define __pre` macros
  (distinct from the `_Valid_impl_`/`_Pre_impl_` SAL2 forms nearby, which are left alone), because
  they conflict with stdlibc++ 8 (C++17) on Linux. We don't use SAL2 `__valid`/`__pre` annotations.
- **`pal/inc/rt/specstrings.h`** — comments out `#define __bound` for the same stdlibc++ 8
  conflict, applied to `__bound`.
- **`pal/inc/rt/palrt.h`** — restores the `PROCESSOR_ARCHITECTURE_*` defines ("copied from
  winnt.h", never Windows-gated even in v7). Upstream dropped the whole block; our own
  `profiler/src/ProfilerEngine/Datadog.Profiler.Native/CorProfilerCallback.cpp`
  (`SysInfoProcessorArchitectureToStr`) switches on `PROCESSOR_ARCHITECTURE_AMD64/ARM/ARM64/
  IA64/INTEL`, which only ever came from this file on non-Windows (`pal/inc/rt` isn't on the
  include path for any Windows project - they see the real SDK `winnt.h` via `<windows.h>`).

Two patches that were needed at v7.0.0 are **no longer applied** as of the v11 RC1 resync, because
upstream now does the equivalent itself: the `#ifdef _DEBUG` guard around the `origBuff`/`outBuff`
assert in `corhlpr.cpp`, and the relocation of the `extern bool g_arm64_atomics_present;`
declaration in `pal.h`.

## Known gaps in the build's own definitions

Since v11, upstream `pal.h` no longer derives `HOST_AMD64`/`HOST_ARM64`/`HOST_X86` from compiler
built-in macros — our build defines them itself via
`shared/src/native-lib/dotnet-runtime/host_arch.h`, forced-included on every native project that
uses these headers (see `build/cmake/FindCoreclr.cmake` and the `HOST_ARCH_HEADER`/
`ForcedIncludeFiles` properties in the Windows vcxprojs).

We also don't compile `minipal/guid.c` itself, even though we vendor it. On non-Windows, GUID
equality (`operator==`/`!=`, used by every `REFIID` comparison such as `QueryInterface`) is
implemented in `minipal/guid.h` via `minipal_guid_equals`, which needs a definition from
somewhere. `guid.c` provides one, but in the same translation unit it also defines
`minipal_guid_v4_create`, which calls into `minipal/random.c` for RNG — and `random.c` needs
upstream's generated `minipalconfig.h` (`HAVE_GETRANDOM`/`HAVE_ARC4RANDOM_BUF`/`HAVE_BCRYPT_H`/...
capability probing via CMake `configure_file`) that our simplified single-`CMakeLists.txt` build
doesn't produce. We never generate GUIDs, only compare ones the CLR profiling APIs give us, so
`shared/src/native-src/minipal_guid.cpp` provides just `minipal_guid_equals` ourselves (identical
to `guid.c`'s own one-line `memcmp` implementation) and is compiled as part of the `coreclr`
CMake target (`build/cmake/FindCoreclr.cmake`) instead.

Since v11, upstream `pal.h` also no longer `#undef`s/redefines `NULL` to the integer literal `0`
in C++ mode. Our toolchain's own `NULL` can expand to `nullptr`, and comparing an integral CLR
handle type (`ModuleID`, `mdMethodDef`, `mdToken`, ...) against `nullptr` doesn't compile
(`invalid operands to binary expression`). This isn't patched in the vendored tree — it's fixed at
the two call sites that did this (`tracer/src/Datadog.Tracer.Native/rejit_handler.cpp`,
`shared/src/native-src/loader.cpp`) by comparing against `0` instead, which is what these
comparisons always meant.
