#!/bin/sh

# Get the directory of the script
DIR=$(dirname "$0")


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

# Set the DD_DOTNET_PATH according to the distribution and architecture
if [ "$ID" = "alpine" ]; then
    if [ "$ARCH" = "x86_64" ]; then
        DD_DOTNET_PATH="$DIR/linux-musl-x64/dd-dotnet"
    elif [ "$ARCH" = "aarch64" ]; then
        echo "Alpine ARM64 is not supported."
        exit 1
    else
        echo "Unsupported architecture: $ARCH"
        exit 1
    fi
else
    if [ "$ARCH" = "x86_64" ]; then
        DD_DOTNET_PATH="$DIR/linux-x64/dd-dotnet"
    elif [ "$ARCH" = "aarch64" ]; then
        DD_DOTNET_PATH="$DIR/linux-arm64/dd-dotnet"
    else
        echo "Unsupported architecture: $ARCH"
        exit 1
    fi
fi

# Check if DD_DOTNET_PATH is set and the file exists
if [ -z "$DD_DOTNET_PATH" ]; then
    echo "Error determining dd-dotnet path."
    exit 1
elif [ ! -f "$DD_DOTNET_PATH" ]; then
    echo "Error: $DD_DOTNET_PATH does not exist."
    exit 1
elif [ ! -x "$DD_DOTNET_PATH" ]; then
    echo "Error: $DD_DOTNET_PATH is not executable."
    exit 1
fi

# If all checks passed, execute dd-dotnet with passed arguments
exec "$DD_DOTNET_PATH" "$@"