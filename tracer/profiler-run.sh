#!/usr/bin/env bash
#
# Run a Samples.Computer01 scenario with the freshly-built profiler attached.
#
# Prerequisites — build artifacts must already exist. From the repo root:
#   ./tracer/build_in_docker.sh BuildTracerHome BuildNativeLoader BuildNativeWrapper BuildProfilerHome BuildProfilerSamples
#
# Usage:
#   ./tracer/profiler-run.sh                       # default scenario: PiComputation (4)
#   ./tracer/profiler-run.sh 5                     # scenario 5 = FibonacciComputation
#   ./tracer/profiler-run.sh 5 --timeout 30        # extra args forwarded to the sample
#   DD_PROFILING_CPU_ENABLED=1 ./tracer/profiler-run.sh 5
#
# Scenario IDs are the values of the `Scenario` enum in
# profiler/src/Demos/Samples.Computer01/Program.cs.
#
# Output (gitignored under .profiler-out/):
#   .profiler-out/logs/    — DD-DotNet-Profiler-Native-*.log etc.
#   .profiler-out/pprof/   — .pprof captures (set via DD_INTERNAL_PROFILING_OUTPUT_DIR)
#
set -euo pipefail

SCRIPT_DIR=$( cd -- "$( dirname -- "${BASH_SOURCE[0]}" )" &> /dev/null && pwd )
ROOT_DIR="$(dirname -- "$SCRIPT_DIR")"
IMAGE_NAME="dd-trace-dotnet/alpine-base"

# --- arg parsing --------------------------------------------------------------
# First positional arg is the scenario number; anything starting with '-' is
# treated as a sample-level flag and we fall back to the default scenario.
SCENARIO=4   # 4 = PiComputation
if [[ $# -gt 0 && ! "$1" =~ ^- ]]; then
    SCENARIO="$1"
    shift
fi
EXTRA_ARGS=("$@")

# --- arch detection -----------------------------------------------------------
ARCH_ENV=()
case "$(uname -m)" in
    arm64|aarch64) RID="linux-musl-arm64"; BUILD_ARCH="ARM64"; ARCH_ENV=(--env DD_INTERNAL_PROFILING_ENABLED_ARM64=1) ;;
    x86_64|amd64)  RID="linux-musl-x64";   BUILD_ARCH="x64"   ;;
    *) echo "Unsupported arch: $(uname -m)" >&2; exit 1 ;;
esac

# --- locate sample dll --------------------------------------------------------
SAMPLE_REL=""
for CFG in Release Debug; do
    candidate="profiler/_build/bin/${CFG}-${BUILD_ARCH}/profiler/src/Demos/Samples.Computer01/net10.0/Samples.Computer01.dll"
    if [[ -f "${ROOT_DIR}/${candidate}" ]]; then
        SAMPLE_REL="$candidate"
        break
    fi
done
if [[ -z "$SAMPLE_REL" ]]; then
    echo "ERROR: Samples.Computer01.dll not found under profiler/_build/bin/{Release,Debug}-${BUILD_ARCH}/." >&2
    echo "Build first: ./tracer/build_in_docker.sh BuildTracerHome BuildNativeLoader BuildNativeWrapper BuildProfilerHome BuildProfilerSamples" >&2
    exit 1
fi

# --- sanity-check the monitoring-home layer ----------------------------------
MH_REL="shared/bin/monitoring-home/${RID}"
required=(Datadog.Trace.ClrProfiler.Native.so Datadog.Profiler.Native.so Datadog.Tracer.Native.so Datadog.Linux.ApiWrapper.x64.so loader.conf)
for f in "${required[@]}"; do
    [[ -f "${ROOT_DIR}/${MH_REL}/${f}" ]] || { echo "ERROR: missing ${MH_REL}/${f} — rebuild the missing target." >&2; exit 1; }
done

# --- output directories (gitignored) -----------------------------------------
OUT_REL=".profiler-out"
mkdir -p "${ROOT_DIR}/${OUT_REL}/logs" "${ROOT_DIR}/${OUT_REL}/pprof"

# --- image -------------------------------------------------------------------
if ! docker image inspect "$IMAGE_NAME" >/dev/null 2>&1; then
    echo "Building $IMAGE_NAME (one-off)..."
    docker build \
        --build-arg DOTNETSDK_VERSION=10.0.100 \
        --tag "$IMAGE_NAME" \
        --file "${ROOT_DIR}/tracer/build/_build/docker/alpine.dockerfile" \
        "${ROOT_DIR}/tracer/build/_build" >&2
fi

# --- TTY handling ------------------------------------------------------------
TTY_FLAGS=(-i)
[[ -t 0 ]] && TTY_FLAGS+=(-t)

echo "→ scenario=${SCENARIO}  rid=${RID}  sample=${SAMPLE_REL}"
echo "→ logs:    ${OUT_REL}/logs/"
echo "→ pprofs:  ${OUT_REL}/pprof/"
echo

docker run --rm "${TTY_FLAGS[@]}" \
    --mount type=bind,source="${ROOT_DIR}",target=/project \
    --workdir /project \
    --env CORECLR_ENABLE_PROFILING=1 \
    --env "CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}" \
    --env "CORECLR_PROFILER_PATH=/project/${MH_REL}/Datadog.Trace.ClrProfiler.Native.so" \
    --env "DD_DOTNET_TRACER_HOME=/project/shared/bin/monitoring-home" \
    --env "DD_NATIVELOADER_CONFIGFILE=/project/${MH_REL}/loader.conf" \
    --env "LD_PRELOAD=/project/${MH_REL}/Datadog.Linux.ApiWrapper.x64.so" \
    --env DD_PROFILING_ENABLED=1 \
    "${ARCH_ENV[@]}" \
    --env DD_PROFILING_MANAGED_ACTIVATION_ENABLED=0 \
    --env DD_TRACE_ENABLED=0 \
    --env DD_TRACE_DEBUG="${DD_TRACE_DEBUG:-0}" \
    --env "DD_TRACE_LOG_DIRECTORY=/project/${OUT_REL}/logs" \
    --env "DD_INTERNAL_PROFILING_OUTPUT_DIR=/project/${OUT_REL}/pprof" \
    ${DD_PROFILING_CPU_ENABLED:+--env DD_PROFILING_CPU_ENABLED="${DD_PROFILING_CPU_ENABLED}"} \
    ${DD_PROFILING_WALLTIME_ENABLED:+--env DD_PROFILING_WALLTIME_ENABLED="${DD_PROFILING_WALLTIME_ENABLED}"} \
    ${DD_PROFILING_UPLOAD_PERIOD:+--env DD_PROFILING_UPLOAD_PERIOD="${DD_PROFILING_UPLOAD_PERIOD}"} \
    "$IMAGE_NAME" \
    dotnet "/project/${SAMPLE_REL}" --scenario "$SCENARIO" "${EXTRA_ARGS[@]}"
rc=$?

# --- post-run sanity check --------------------------------------------------
loader_log="${ROOT_DIR}/${OUT_REL}/logs/dotnet-native-loader-dotnet-1.log"
if [[ -f "$loader_log" ]] && grep -q "Error loading dynamic library.*Datadog.Profiler.Native.so" "$loader_log"; then
    echo >&2
    echo "WARNING: the native profiler library failed to load — no .pprof will be produced." >&2
    grep "Error loading dynamic library.*Datadog.Profiler.Native.so" "$loader_log" | tail -1 >&2
    echo "Hint: rebuild the profiler home (./tracer/build_in_docker.sh BuildProfilerHome)." >&2
fi
exit $rc
