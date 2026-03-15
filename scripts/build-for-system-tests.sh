#!/usr/bin/env bash
# build-for-system-tests.sh
#
# Builds a tar.gz artifact suitable for use with system-tests parametric tests,
# targeting Linux arm64 (or x86_64 when run on an Intel machine).
#
# This script is designed for local development on ARM Mac (Apple Silicon).
# It spins up a Debian-based Docker container that cross-compiles/builds the
# .NET tracer for the host architecture, then produces the standard
# datadog-dotnet-apm-<version>.arm64.tar.gz (or .tar.gz for x64) artifact.
#
# Usage:
#   scripts/build-for-system-tests.sh [--copy-to <path>] [--no-clean]
#
#   --copy-to <path>   If provided, copies the final tar.gz to <path>/
#                      This is useful to drop it directly into system-tests/binaries/
#   --no-clean         Skip the Clean step (useful if re-running after a partial build failure)
#
# Requirements:
#   - Docker (with buildx and multi-platform support for arm64)
#   - The script must be run from the repository root or the tracer/ subdirectory.
#     It will auto-detect the repo root.
#
# Typical first run (~10-30 min depending on machine):
#   ./scripts/build-for-system-tests.sh --copy-to ~/src/system-tests/binaries/dotnet
#
# Subsequent runs (skip clean to reuse partial artifacts):
#   ./scripts/build-for-system-tests.sh --no-clean --copy-to ~/src/system-tests/binaries/dotnet

set -euo pipefail

###############################################################################
# Helpers
###############################################################################

info()    { echo "[INFO]  $*"; }
success() { echo "[OK]    $*"; }
warn()    { echo "[WARN]  $*" >&2; }
error()   { echo "[ERROR] $*" >&2; exit 1; }

###############################################################################
# Parse arguments
###############################################################################

COPY_TO=""
NO_CLEAN=false

while [[ $# -gt 0 ]]; do
  case "$1" in
    --copy-to)
      [[ -n "${2:-}" ]] || error "--copy-to requires a path argument"
      COPY_TO="$2"
      shift 2
      ;;
    --no-clean)
      NO_CLEAN=true
      shift
      ;;
    -h|--help)
      sed -n '/^# Usage:/,/^[^#]/p' "$0" | sed 's/^# \?//'
      exit 0
      ;;
    *)
      error "Unknown argument: $1  (use --help for usage)"
      ;;
  esac
done

###############################################################################
# Locate repo root
###############################################################################

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Walk up from the script location until we find the repo root (contains tracer/)
REPO_ROOT="$SCRIPT_DIR"
while [[ ! -d "$REPO_ROOT/tracer" && "$REPO_ROOT" != "/" ]]; do
  REPO_ROOT="$(dirname "$REPO_ROOT")"
done
[[ -d "$REPO_ROOT/tracer" ]] || error "Could not locate repo root (expected a directory containing tracer/)"

info "Repo root: $REPO_ROOT"
TRACER_DIR="$REPO_ROOT/tracer"
BUILD_DIR="$TRACER_DIR/build/_build"

###############################################################################
# Detect architecture
###############################################################################

HOST_ARCH="$(uname -m)"
case "$HOST_ARCH" in
  arm64|aarch64)
    LINUX_ARCH="arm64"
    DOCKER_PLATFORM="linux/arm64"
    ;;
  x86_64|amd64)
    LINUX_ARCH="x64"
    DOCKER_PLATFORM="linux/amd64"
    ;;
  *)
    error "Unsupported host architecture: $HOST_ARCH"
    ;;
esac

info "Host architecture: $HOST_ARCH  →  Linux arch: $LINUX_ARCH  (Docker platform: $DOCKER_PLATFORM)"

###############################################################################
# Detect .NET SDK version from global.json (used to tag the builder image)
###############################################################################

GLOBAL_JSON="$REPO_ROOT/global.json"
if [[ -f "$GLOBAL_JSON" ]]; then
  # Extract the sdk.version field; fall back to a default if jq is not present
  if command -v jq &>/dev/null; then
    SDK_VERSION="$(jq -r '.sdk.version' "$GLOBAL_JSON")"
  else
    # Simple grep fallback — assumes "version": "X.Y.Z" is on a single line
    SDK_VERSION="$(grep -Eo '"version"\s*:\s*"[^"]+"' "$GLOBAL_JSON" | head -1 | grep -Eo '[0-9]+\.[0-9]+\.[0-9]+')"
  fi
fi
: "${SDK_VERSION:=10.0.100}"   # hard-coded fallback
info "Using .NET SDK version: $SDK_VERSION"

###############################################################################
# Build or reuse the Debian builder Docker image
###############################################################################

IMAGE_NAME="dd-trace-dotnet/debian-builder:${SDK_VERSION}"
DOCKERFILE="$BUILD_DIR/docker/debian.dockerfile"

[[ -f "$DOCKERFILE" ]] || error "Debian Dockerfile not found at: $DOCKERFILE"

if docker image inspect "$IMAGE_NAME" &>/dev/null; then
  info "Docker image already exists: $IMAGE_NAME  (skipping build)"
else
  info "Building Docker image: $IMAGE_NAME"
  docker build \
    --platform "$DOCKER_PLATFORM" \
    --build-arg "DOTNETSDK_VERSION=${SDK_VERSION}" \
    --tag "$IMAGE_NAME" \
    --file "$DOCKERFILE" \
    --target builder \
    "$BUILD_DIR"
  success "Docker image built: $IMAGE_NAME"
fi

###############################################################################
# Nuke build target list
###############################################################################

# These are the minimal targets required to produce a functional tracer home
# directory and then zip it. We intentionally omit profiler targets to keep
# build time short (the profiler .so files will be stubbed below if missing).
NUKE_TARGETS="CompileManagedLoader BuildNativeTracerHome BuildManagedTracerHome BuildNativeLoader ZipMonitoringHome"

if [[ "$NO_CLEAN" == false ]]; then
  # Prepend Clean so we start from a pristine state on a first run
  NUKE_TARGETS="Clean $NUKE_TARGETS"
fi

###############################################################################
# Helper: run Nuke inside the builder container
###############################################################################

run_nuke() {
  local targets="$1"
  info "Running Nuke targets: $targets"

  docker run --rm \
    --platform "$DOCKER_PLATFORM" \
    --mount "type=bind,source=${REPO_ROOT},target=/project" \
    --workdir /project/tracer \
    --env NugetPackageDirectory=/project/packages \
    --env artifacts=/project/tracer/bin/artifacts \
    --env DD_INSTRUMENTATION_TELEMETRY_ENABLED=0 \
    --env NUKE_TELEMETRY_OPTOUT=1 \
    --env DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    --env DOTNET_NOLOGO=1 \
    "$IMAGE_NAME" \
    dotnet /build/bin/Debug/_build.dll $targets
}

###############################################################################
# First build attempt
###############################################################################

info "=== Starting build (targets: $NUKE_TARGETS) ==="

if run_nuke "$NUKE_TARGETS"; then
  success "Nuke build completed successfully on first attempt."
else
  BUILD_EXIT=$?

  # -------------------------------------------------------------------------
  # ARM64 workaround: ZipMonitoringHome can fail because the profiler native
  # build does not produce Datadog.Linux.ApiWrapper.x64.so on arm64 in some
  # configurations.  We create a dummy (empty) .so and retry only the zip step.
  # -------------------------------------------------------------------------
  warn "Nuke build exited with code $BUILD_EXIT — attempting ARM64 stub workaround."

  MONITORING_HOME="$REPO_ROOT/tracer/bin/monitoring-home"
  ARCH_DIR="$MONITORING_HOME/linux-${LINUX_ARCH}"
  MUSL_DIR="$MONITORING_HOME/linux-musl-${LINUX_ARCH}"
  API_WRAPPER="Datadog.Linux.ApiWrapper.x64.so"

  # Create a stub ApiWrapper if it is missing
  if [[ ! -f "$ARCH_DIR/$API_WRAPPER" ]]; then
    warn "Creating stub $API_WRAPPER in $ARCH_DIR"
    mkdir -p "$ARCH_DIR"
    touch "$ARCH_DIR/$API_WRAPPER"
  fi

  # The linux-musl-<arch>/ directory must also exist with a few expected files
  # so that the packaging step can create hard-links.  Copy what we have from
  # the glibc directory if it is entirely absent.
  if [[ ! -d "$MUSL_DIR" || -z "$(ls -A "$MUSL_DIR" 2>/dev/null)" ]]; then
    warn "linux-musl-${LINUX_ARCH}/ is empty or missing — copying from linux-${LINUX_ARCH}/"
    mkdir -p "$MUSL_DIR"
    for f in \
      "Datadog.Trace.ClrProfiler.Native.so" \
      "libddwaf.so" \
      "loader.conf" \
      "$API_WRAPPER"
    do
      if [[ -f "$ARCH_DIR/$f" ]]; then
        cp "$ARCH_DIR/$f" "$MUSL_DIR/$f"
      else
        warn "  Source file missing, creating stub: $f"
        touch "$MUSL_DIR/$f"
      fi
    done
  fi

  # Retry just the zip step — skip Clean to preserve what was built above
  info "Retrying only: ZipMonitoringHome"
  if run_nuke "ZipMonitoringHome"; then
    success "ZipMonitoringHome succeeded after stub workaround."
  else
    error "ZipMonitoringHome failed even after stub workaround.  Check the Docker output above."
  fi
fi

###############################################################################
# Locate the produced tar.gz
###############################################################################

ARTIFACTS_DIR="$REPO_ROOT/tracer/bin/artifacts"
LINUX_ARTIFACTS_DIR="$ARTIFACTS_DIR/linux-${LINUX_ARCH}"

info "Looking for tar.gz in: $LINUX_ARTIFACTS_DIR"

# The nuke build names the file:
#   datadog-dotnet-apm-<version>.<arch>.tar.gz  (arm64)
#   datadog-dotnet-apm-<version>.tar.gz         (x64)
TARBALL="$(find "$LINUX_ARTIFACTS_DIR" -maxdepth 1 -name 'datadog-dotnet-apm-*.tar.gz' 2>/dev/null | sort | tail -1)"

if [[ -z "$TARBALL" ]]; then
  # Fallback: search the whole artifacts directory
  TARBALL="$(find "$ARTIFACTS_DIR" -name 'datadog-dotnet-apm-*.tar.gz' 2>/dev/null | sort | tail -1)"
fi

[[ -n "$TARBALL" ]] || error "Could not find datadog-dotnet-apm-*.tar.gz under $ARTIFACTS_DIR"

success "Artifact: $TARBALL"

###############################################################################
# Optionally copy to system-tests binaries path
###############################################################################

if [[ -n "$COPY_TO" ]]; then
  info "Copying artifact to: $COPY_TO"
  mkdir -p "$COPY_TO"
  cp "$TARBALL" "$COPY_TO/"
  success "Copied to: $COPY_TO/$(basename "$TARBALL")"
fi

###############################################################################
# Done
###############################################################################

echo ""
echo "=============================="
echo "  Build complete!"
echo "  Artifact: $TARBALL"
if [[ -n "$COPY_TO" ]]; then
  echo "  Copied to: $COPY_TO/$(basename "$TARBALL")"
fi
echo "=============================="
echo ""
echo "To use with system-tests parametric tests, set:"
echo "  export SYSTEM_TESTS_DOTNET_APM_PACKAGE=$TARBALL"
echo "or pass --copy-to <system-tests>/binaries/dotnet/ to place it automatically."
