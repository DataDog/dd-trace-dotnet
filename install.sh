#!/usr/bin/env bash

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default values
INSTALL_DIR="/opt/datadog"
GITHUB_REPO="DataDog/dd-trace-dotnet"
VERSION=""
UNINSTALL=false

# Print functions
print_info() {
    echo -e "${GREEN}[INFO]${NC} $1" >&2
}

print_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1" >&2
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1" >&2
}

# Usage information
usage() {
    cat << EOF
Usage: $0 [OPTIONS]

Install or uninstall the Datadog .NET APM Tracer.

OPTIONS:
    -v, --version VERSION     Specify version to install (e.g., v3.36.0 or 3.36.0)
                             Default: latest release
    -p, --path DIRECTORY      Installation directory
                             Default: /opt/datadog
    -u, --uninstall          Uninstall the tracer from the specified path
    -h, --help               Show this help message

EXAMPLES:
    # Install latest version to default location
    sudo $0

    # Install specific version (with or without 'v' prefix)
    sudo $0 --version v3.36.0
    sudo $0 --version 3.36.0

    # Install to custom directory
    sudo $0 --path /usr/local/datadog

    # Uninstall from default location
    sudo $0 --uninstall

    # Uninstall from custom location
    sudo $0 --uninstall --path /usr/local/datadog
EOF
    exit 0
}

# Parse command line arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            -v|--version)
                VERSION="$2"
                # Ensure version has 'v' prefix for GitHub API/download URLs
                if [[ ! "$VERSION" =~ ^v ]]; then
                    VERSION="v${VERSION}"
                fi
                shift 2
                ;;
            -p|--path)
                INSTALL_DIR="$2"
                shift 2
                ;;
            -u|--uninstall)
                UNINSTALL=true
                shift
                ;;
            -h|--help)
                usage
                ;;
            *)
                print_error "Unknown option: $1"
                usage
                ;;
        esac
    done
}

# Detect architecture
detect_architecture() {
    local arch
    arch=$(uname -m)

    case $arch in
        x86_64)
            echo "x64"
            ;;
        aarch64|arm64)
            echo "arm64"
            ;;
        *)
            print_error "Unsupported architecture: $arch"
            print_error "Supported architectures: x86_64 (x64), aarch64/arm64"
            exit 1
            ;;
    esac
}

# Check if running with sudo/root
check_permissions() {
    if [[ $EUID -ne 0 ]] && [[ "$INSTALL_DIR" == /opt/* || "$INSTALL_DIR" == /usr/* ]]; then
        print_error "This script must be run with sudo when installing to system directories"
        print_error "Please run: sudo $0 $*"
        exit 1
    fi
}

# Check required commands
check_requirements() {
    local missing_commands=()

    for cmd in curl tar; do
        if ! command -v "$cmd" &> /dev/null; then
            missing_commands+=("$cmd")
        fi
    done

    if [[ ${#missing_commands[@]} -gt 0 ]]; then
        print_error "Missing required commands: ${missing_commands[*]}"
        print_error "Please install them and try again"
        exit 1
    fi
}

# Get latest release version from GitHub
get_latest_version() {
    print_info "Fetching latest release version..."

    local latest_url="https://api.github.com/repos/$GITHUB_REPO/releases/latest"
    local version

    version=$(curl -sL "$latest_url" | grep '"tag_name":' | sed -E 's/.*"([^"]+)".*/\1/')

    if [[ -z "$version" ]]; then
        print_error "Failed to fetch latest version from GitHub"
        exit 1
    fi

    echo "$version"
}

# Download artifact
download_artifact() {
    local version=$1
    local arch=$2

    # Strip 'v' prefix from version if present (e.g., v3.36.0 -> 3.36.0)
    local version_number="${version#v}"

    # Construct artifact name based on architecture
    local artifact_name
    if [[ "$arch" == "x64" ]]; then
        artifact_name="datadog-dotnet-apm-${version_number}.tar.gz"
    else
        artifact_name="datadog-dotnet-apm-${version_number}.${arch}.tar.gz"
    fi

    local download_url="https://github.com/$GITHUB_REPO/releases/download/${version}/${artifact_name}"
    local temp_file="/tmp/${artifact_name}"

    print_info "Downloading $artifact_name..."
    print_info "URL: $download_url"

    if ! curl -SL --fail --progress-bar -o "$temp_file" "$download_url"; then
        print_error "Failed to download artifact"
        print_error "URL: $download_url"
        exit 1
    fi

    echo "$temp_file"
}

# Install tracer
install_tracer() {
    local artifact_path=$1

    print_info "Installing to $INSTALL_DIR..."

    # Create installation directory if it doesn't exist
    mkdir -p "$INSTALL_DIR"

    # Extract tarball
    if ! tar -C "$INSTALL_DIR" -xzf "$artifact_path"; then
        print_error "Failed to extract artifact"
        exit 1
    fi

    # Run createLogPath.sh if it exists
    if [[ -f "$INSTALL_DIR/createLogPath.sh" ]]; then
        print_info "Creating log directory..."
        if ! "$INSTALL_DIR/createLogPath.sh"; then
            print_warn "Failed to run createLogPath.sh, you may need to create log directories manually"
        fi
    fi

    print_info "Installation complete!"
}

# Print environment variables to set
print_env_vars() {
    local arch=$1

    cat << EOF

${GREEN}Installation successful!${NC}

To enable automatic instrumentation, set the following environment variables:

    export CORECLR_ENABLE_PROFILING=1
    export CORECLR_PROFILER={846F5F1C-F9AE-4B07-969E-05C26BC060D8}
    export CORECLR_PROFILER_PATH=$INSTALL_DIR/linux-$arch/Datadog.Trace.ClrProfiler.Native.so
    export DD_DOTNET_TRACER_HOME=$INSTALL_DIR

For .NET Framework on Linux (Mono):

    export CORECLR_PROFILER_PATH=$INSTALL_DIR/linux-$arch/Datadog.Trace.ClrProfiler.Native.so

For more information, see:
    https://docs.datadoghq.com/tracing/trace_collection/dd_libraries/dotnet-core

EOF
}

# Cleanup
cleanup() {
    local temp_file=$1
    if [[ -f "$temp_file" ]]; then
        print_info "Cleaning up temporary files..."
        rm -f "$temp_file"
    fi
}

# Uninstall tracer
uninstall_tracer() {
    print_info "Uninstalling Datadog .NET APM Tracer from $INSTALL_DIR..."

    # Check if directory exists
    if [[ ! -d "$INSTALL_DIR" ]]; then
        print_error "Installation directory does not exist: $INSTALL_DIR"
        exit 1
    fi

    # Verify it looks like a Datadog installation
    if [[ ! -f "$INSTALL_DIR/version" ]] && [[ ! -d "$INSTALL_DIR/netcoreapp3.1" ]]; then
        print_error "Directory does not appear to be a Datadog tracer installation: $INSTALL_DIR"
        print_error "Refusing to delete for safety. Please verify the path and remove manually if needed."
        exit 1
    fi

    # Remove the installation directory
    print_info "Removing $INSTALL_DIR..."
    if ! rm -rf "$INSTALL_DIR"; then
        print_error "Failed to remove installation directory"
        exit 1
    fi

    # Remove log directory if it exists
    local log_dir="/var/log/datadog/dotnet"
    if [[ -d "$log_dir" ]]; then
        print_info "Removing log directory: $log_dir..."
        rm -rf "$log_dir" || print_warn "Failed to remove log directory: $log_dir"
    fi

    cat << EOF

${GREEN}Uninstallation complete!${NC}

The Datadog .NET APM Tracer has been removed from $INSTALL_DIR.

Remember to remove or unset the following environment variables:
    - CORECLR_ENABLE_PROFILING
    - CORECLR_PROFILER
    - CORECLR_PROFILER_PATH
    - DD_DOTNET_TRACER_HOME

EOF
}

# Main
main() {
    parse_args "$@"

    print_info "Datadog .NET APM Tracer Installer"
    print_info "=================================="

    check_permissions "$@"

    # Handle uninstall mode
    if [[ "$UNINSTALL" == true ]]; then
        uninstall_tracer
        exit 0
    fi

    # Continue with installation
    check_requirements

    # Detect architecture
    ARCH=$(detect_architecture)
    print_info "Detected architecture: $ARCH"

    # Get version
    if [[ -z "$VERSION" ]]; then
        VERSION=$(get_latest_version)
        print_info "Latest version: $VERSION"
    else
        print_info "Installing version: $VERSION"
    fi

    # Download artifact
    TEMP_FILE=$(download_artifact "$VERSION" "$ARCH")

    # Install
    install_tracer "$TEMP_FILE"

    # Cleanup
    cleanup "$TEMP_FILE"

    # Print environment variables
    print_env_vars "$ARCH"
}

# Run main function
main "$@"
