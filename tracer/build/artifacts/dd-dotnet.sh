#!/bin/sh

TRACER_VERSION="3.4.1"

# Get the directory of the script
DIR=$(dirname "$(readlink -f "$0")")

# Check the OS
OS_NAME="$(uname -s)"
if [ "$OS_NAME" != "Linux" ]; then
    echo "This script is intended for Linux systems only. Current system is $OS_NAME."
    exit 1
fi

# Read the /etc/os-release to detect distribution
if [ -f /etc/os-release ]; then
    . /etc/os-release
else
    echo "Unable to find /etc/os-release. Cannot determine the distribution."
    exit 1
fi

# Check the machine architecture
ARCH="$(uname -m)"

DD_DOTNET_PATH=""
EXPECTED_PACKAGE=""

DISTRO_ID="$ID"
if [ ! -z "$ID_LIKE" ]; then
    DISTRO_ID="$DISTRO_ID $ID_LIKE"
fi

contains_word() {
  local string="$1"
  local word="$2"

  for token in $string; do
    if [ "$token" = "$word" ]; then
      return 0  # word found
    fi
  done

  return 1  # word not found
}

# Set the DD_DOTNET_PATH according to the distribution and architecture
if contains_word "$DISTRO_ID" "alpine"; then
    if [ "$ARCH" = "x86_64" ]; then
        DD_DOTNET_PATH="$DIR/linux-musl-x64/dd-dotnet"
        EXPECTED_PACKAGE="datadog-dotnet-apm-${TRACER_VERSION}-musl.tar.gz"
    elif [ "$ARCH" = "aarch64" ]; then
	      DD_DOTNET_PATH="$DIR/linux-musl-arm64/dd-dotnet"
        EXPECTED_PACKAGE="datadog-dotnet-apm-${TRACER_VERSION}.arm64.tar.gz"
    else
        echo "Unsupported architecture: $ARCH"
        exit 1
    fi
elif contains_word "$DISTRO_ID" "centos" || contains_word "$DISTRO_ID" "rhel" || contains_word "$DISTRO_ID" "fedora" || contains_word "$DISTRO_ID" "opensuse"; then
    if [ "$ARCH" = "x86_64" ]; then
        DD_DOTNET_PATH="$DIR/linux-x64/dd-dotnet"
        EXPECTED_PACKAGE="datadog-dotnet-apm-${TRACER_VERSION}-1.x86_64.rpm"
    elif [ "$ARCH" = "aarch64" ]; then
        DD_DOTNET_PATH="$DIR/linux-arm64/dd-dotnet"
        EXPECTED_PACKAGE="datadog-dotnet-apm-${TRACER_VERSION}-1.aarch64.rpm"
    else
        echo "Unsupported architecture: $ARCH"
        exit 1
    fi
elif contains_word "$DISTRO_ID" "debian" || contains_word "$DISTRO_ID" "ubuntu"; then
    if [ "$ARCH" = "x86_64" ]; then
        DD_DOTNET_PATH="$DIR/linux-x64/dd-dotnet"
        EXPECTED_PACKAGE="datadog-dotnet-apm_${TRACER_VERSION}_amd64.deb"
    elif [ "$ARCH" = "aarch64" ]; then
        DD_DOTNET_PATH="$DIR/linux-arm64/dd-dotnet"
        EXPECTED_PACKAGE="datadog-dotnet-apm_${TRACER_VERSION}_arm64.deb"
    else
        echo "Unsupported architecture: $ARCH"
        exit 1
    fi
else
    if [ "$ARCH" = "x86_64" ]; then
        DD_DOTNET_PATH="$DIR/linux-x64/dd-dotnet"
        EXPECTED_PACKAGE="datadog-dotnet-apm-${TRACER_VERSION}.tar.gz"
    elif [ "$ARCH" = "aarch64" ]; then
        DD_DOTNET_PATH="$DIR/linux-arm64/dd-dotnet"
        EXPECTED_PACKAGE="datadog-dotnet-apm-${TRACER_VERSION}.arm64.tar.gz"
    else
        echo "Unsupported architecture: $ARCH"
        exit 1
    fi
fi

# Check if DD_DOTNET_PATH is set and the file exists
if [ -z "$DD_DOTNET_PATH" ]; then
    # Should never happen
    echo "Error determining dd-dotnet path."
    exit 1
elif [ ! -f "$DD_DOTNET_PATH" ]; then
    echo "Error: $DD_DOTNET_PATH does not exist."
    echo "Ensure you have downloaded/installed a version of the .NET tracer compatible with your architecture and OS."
    echo "For example, for the $ARCH architecture on $ID, you should choose the $EXPECTED_PACKAGE package."
    exit 1
elif [ ! -x "$DD_DOTNET_PATH" ]; then
    echo "Error: $DD_DOTNET_PATH is not executable."
    exit 1
fi

# If all checks passed, execute dd-dotnet with passed arguments
exec "$DD_DOTNET_PATH" "$@"