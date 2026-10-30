#!/usr/bin/env bash
# Benchmarks the PoC C library against a given libdatadog release (Linux).
#
#   ./run-benchmarks.sh [-v v38.0.0] [-o out-dir] [-s scenario,...] [-c cpu] [-r repeat] [--quick]
#
# Downloads the libdatadog release artifact for this machine, builds the PoC
# from this checkout (standalone CMake build), builds bench.c against both,
# runs the scenarios and writes <out-dir>/report.md and results.json.
# Requirements: cmake, a C compiler (cc/gcc/clang), curl, tar, python3, git.
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
POC_DIR="$(cd "$HERE/.." && pwd)"
VERSION="v38.0.0"
OUT="$HERE/out"
SCENARIOS="all"
CPU="2"
REPEAT="3"
QUICK=""
CC="${CC:-cc}"

usage() { sed -n '2,10p' "$0" | sed 's/^# \{0,1\}//'; exit 1; }
while [[ $# -gt 0 ]]; do
  case "$1" in
    -v|--libdatadog-version) VERSION="$2"; shift 2 ;;
    -o|--out) OUT="$2"; shift 2 ;;
    -s|--scenarios) SCENARIOS="$2"; shift 2 ;;
    -c|--cpu) CPU="$2"; shift 2 ;;
    -r|--repeat) REPEAT="$2"; shift 2 ;;
    --quick) QUICK="--quick"; shift ;;
    -h|--help) usage ;;
    *) echo "unknown argument: $1"; usage ;;
  esac
done
[[ "$VERSION" == v* ]] || VERSION="v$VERSION"

for tool in cmake curl tar python3 "$CC"; do
  command -v "$tool" >/dev/null || { echo "missing required tool: $tool"; exit 1; }
done

# ---- libdatadog release artifact for this machine ----
case "$(uname -m)" in
  x86_64|amd64) ARCH="x86_64" ;;
  aarch64|arm64) ARCH="aarch64" ;;
  *) echo "unsupported architecture: $(uname -m)"; exit 1 ;;
esac
if ldd --version 2>&1 | grep -qi musl; then
  ASSET="libdatadog-$ARCH-alpine-linux-musl.tar.gz"
else
  ASSET="libdatadog-$ARCH-unknown-linux-gnu.tar.gz"
fi
CACHE="$OUT/cache"
LDD_ROOT="$CACHE/libdatadog-$VERSION"
mkdir -p "$CACHE"
if [[ ! -d "$LDD_ROOT" ]]; then
  echo "==> downloading libdatadog $VERSION ($ASSET)"
  if ! curl -sSfL -o "$CACHE/$ASSET" "https://github.com/DataDog/libdatadog/releases/download/$VERSION/$ASSET"; then
    echo "error: no '$ASSET' in libdatadog release $VERSION."
    echo "Note: patch releases (e.g. v44.0.2) are published without binaries; use the .0 release (e.g. v44.0.0)."
    exit 1
  fi
  mkdir -p "$LDD_ROOT.tmp" && tar xzf "$CACHE/$ASSET" -C "$LDD_ROOT.tmp" && mv "$LDD_ROOT.tmp" "$LDD_ROOT"
fi
LDD_DIR="$(find "$LDD_ROOT" -mindepth 1 -maxdepth 1 -type d -name 'libdatadog-*' | head -1)"
[[ -f "$LDD_DIR/lib/libdatadog_profiling.so" ]] || { echo "libdatadog_profiling.so not found under $LDD_ROOT"; exit 1; }

# ---- PoC: standalone build of this checkout ----
# CMAKE_POSITION_INDEPENDENT_CODE: the vendored static curl/zstd end up in a
# shared library (the full repo build sets it globally, the standalone one doesn't)
POC_BUILD="$OUT/poc-build"
echo "==> building the PoC ($POC_DIR)"
cmake -S "$POC_DIR" -B "$POC_BUILD" -DCMAKE_BUILD_TYPE=Release -DCMAKE_POSITION_INDEPENDENT_CODE=ON > "$OUT/poc-build.log" 2>&1 \
  && cmake --build "$POC_BUILD" -j "$(nproc)" --target datadog_profiling_poc >> "$OUT/poc-build.log" 2>&1 \
  || { echo "PoC build failed, see $OUT/poc-build.log"; tail -20 "$OUT/poc-build.log"; exit 1; }
POC_LIB_DIR="$(dirname "$(find "$POC_BUILD" -name libdatadog_profiling_poc.so | head -1)")"
POC_VERSION="$(git -C "$POC_DIR" describe --always --dirty 2>/dev/null || echo unknown)"

# ---- benchmark binaries ----
echo "==> building the benchmarks"
"$CC" -O2 -std=gnu11 -Wall -DBACKEND_POC -I"$POC_DIR/include" "$HERE/bench.c" \
  -L"$POC_LIB_DIR" -ldatadog_profiling_poc -Wl,-rpath,"$POC_LIB_DIR" -o "$OUT/bench_poc"
if ! "$CC" -O2 -std=gnu11 -Wall -DBACKEND_LDD -I"$LDD_DIR/include" "$HERE/bench.c" \
  -L"$LDD_DIR/lib" -ldatadog_profiling -Wl,-rpath,"$LDD_DIR/lib" -o "$OUT/bench_ldd"; then
  echo "error: bench.c does not compile against libdatadog $VERSION's FFI."
  echo "It targets the v38-style API (verified with v38.0.0 and v44.0.0); adapt the BACKEND_LDD section of bench.c."
  exit 1
fi

# ---- run ----
echo "==> running (this takes a few minutes; progress below)"
python3 "$HERE/run.py" --poc "$OUT/bench_poc" --ldd "$OUT/bench_ldd" --out "$OUT" \
  --scenarios "$SCENARIOS" --repeat "$REPEAT" --cpu "$CPU" \
  --ldd-version "$VERSION" --poc-version "$POC_VERSION" $QUICK
echo "==> report: $OUT/report.md"
