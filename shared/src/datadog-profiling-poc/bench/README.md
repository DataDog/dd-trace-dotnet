# PoC vs libdatadog benchmark kit

Compares the PoC C library (this checkout) with a libdatadog release by calling
each library's C API directly with the same synthetic inputs. It measures CPU
and memory for adding samples and serializing profiles, plus the exporter:

| Phase | Measured |
|---|---|
| `add` | time per sample, heap held after the adds |
| `serialize` | time, peak extra RSS (Linux), heap while the encoded profile is alive, encoded size |
| endpoints | `set_endpoint` + `add_endpoint_count` for 1000 spans, plus the endpoint labels at serialize |
| upscaling | 32 proportional rules applied at serialize |
| exporter | creation time and RSS, `send` time to a local HTTP sink (profile + 16 KB attachment), thread count |

A cycle is "add N samples, then serialize", repeated on the same profile as a
profiler does. The report shows medians of the steady-state cycles (the first
one is excluded: it also pays for growing the tables).

## Linux

Requires `cmake`, a C compiler (`cc`, or set `CC`), `curl`, `tar`, `python3`, `git`.

```
./run-benchmarks.sh -v v44.0.0              # full run, a few minutes
./run-benchmarks.sh -v v38.0.0 --quick      # smaller scenarios, 1 repeat
./run-benchmarks.sh -v v38.0.0 -s scale_10k,export -c 3 -r 5 -o /tmp/bench
```

The script downloads the release artifact for this machine (x86_64/aarch64,
glibc or musl), builds the PoC with its standalone CMake build, builds
`bench.c` against both libraries and writes `out/report.md` and
`out/results.json`.

## Windows (no CMake)

Requires Visual Studio 2019+ or the Build Tools with the C++ workload, and
Python 3 (`python` or `py` on `PATH`). From PowerShell:

```
.\run-benchmarks.ps1 -LibdatadogVersion v44.0.0
.\run-benchmarks.ps1 -LibdatadogVersion v38.0.0 -Arch x86 -Quick
.\run-benchmarks.ps1 -LibdatadogVersion v38.0.0 -Scenarios scale_10k,export -Cpu 3 -Repeat 5
```

The script finds MSVC with `vswhere`, downloads the libdatadog Windows package
(about 250 MB, only the headers and the release DLL are kept), compiles the
PoC sources and the vendored zstd (`vendor/zstd/zstd.c`) directly with `cl.exe`
- the same files as the profiler's `.vcxproj`, WinHTTP exporter included - and
runs the same scenarios.

## Options

| Linux | Windows | Default | Meaning |
|---|---|---|---|
| `-v` | `-LibdatadogVersion` | `v38.0.0` | libdatadog release tag |
| | `-Arch` | `x64` | `x64` or `x86` |
| `-o` | `-Out` | `bench/out` | output directory (downloads are cached in `<out>/cache`) |
| `-s` | `-Scenarios` | `all` | comma-separated scenario names, `export` for the exporter |
| `-c` | `-Cpu` | `2` | core the benchmark is pinned to (`-1`: no pinning) |
| `-r` | `-Repeat` | `3` | processes per scenario and library |
| `--quick` | `-Quick` | | 1 repeat, 2 cycles, 10x fewer samples |

Scenarios (see `run.py`): `scale_1k`, `scale_10k`, `no_ts_10k`,
`aggregation_heavy_20k`, `shallow_100k`, `endpoints_upscale_5k`, `export`.
For other shapes, run the binaries directly: `bench_poc --samples N --stacks S
--depth D --labels L --values V --ts 0|1 --cycles C [--endpoints E] [--upscale 1]`.

## Notes

- **libdatadog versions**: `bench.c` targets the v38-style FFI (verified with
  v38.0.0 and v44.0.0). Patch releases such as v44.0.2 are published without
  binaries; use the `.0` release. If an API changed, adapt the `BACKEND_LDD`
  section of `bench.c`.
- **Heap**: both libraries allocate through the C heap, so it is measured the
  same way for both: `mallinfo2()` on Linux, a walk of all the process heaps on
  Windows (UCRT and Rust's allocator both use the process heap).
- **Windows timing**: the process CPU clock (`GetProcessTimes`) has a ~15.6 ms
  granularity, so the Windows report uses wall time measured on the pinned
  core. The serialize peak-RSS metric is Linux-only.
- For stable numbers: idle machine, pinned core, and `-r 5` or more.
- Validation of the Windows path so far: the code was cross-compiled with
  MinGW-w64 and the PoC benchmark run under Wine; the MSVC command lines in
  `run-benchmarks.ps1` and the libdatadog run on Windows have not been run on
  a real Windows machine yet.
