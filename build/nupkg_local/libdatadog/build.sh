#!/bin/bash

# Get the latest release version from GitHub
version=$(curl -s https://api.github.com/repos/DataDog/libdatadog/releases/latest | grep -o '"tag_name": "*\([^"]*\)"' | cut -d '"' -f 4)

# Define the URLs
urls=(
    "https://github.com/DataDog/libdatadog/releases/download/$version/libdatadog-aarch64-alpine-linux-musl.tar.gz"
    "https://github.com/DataDog/libdatadog/releases/download/$version/libdatadog-aarch64-apple-darwin.tar.gz"
    "https://github.com/DataDog/libdatadog/releases/download/$version/libdatadog-aarch64-unknown-linux-gnu.tar.gz"
    "https://github.com/DataDog/libdatadog/releases/download/$version/libdatadog-x64-windows.zip"
    "https://github.com/DataDog/libdatadog/releases/download/$version/libdatadog-x86-windows.zip"
    "https://github.com/DataDog/libdatadog/releases/download/$version/libdatadog-x86_64-alpine-linux-musl.tar.gz"
    # "https://github.com/DataDog/libdatadog/releases/download/$version/libdatadog-x86_64-apple-darwin.tar.gz"
    "https://github.com/DataDog/libdatadog/releases/download/$version/libdatadog-x86_64-unknown-linux-gnu.tar.gz"
)

# Create the sources/runtime directory if it doesn't exist
mkdir -p sources/runtimes
Download release files
for url in "${urls[@]}"; do
    filename=$(basename "$url")

    echo "Downloading $url... at release/$filename"
    curl -L -o "release/$filename" "$url"
done

# extract the files
mkdir -p release/unzipped
for file in release/*; do
    if [[ $file == *.tar.gz ]]; then
        echo "Extracting $file..."
        tar -xzf "$file" -C release/unzipped
    elif [[ $file == *.zip ]]; then
        echo "Extracting $file..."
        # unzip with overwrite
        unzip -q "$file" -d release/unzipped -o
    fi
done

# copy required binaries to their rid format inside sources/runtimes
# {os]-{distro}-{arch}
# os: linux, osx, win
for dir in release/unzipped/*; do
    if [[ -d $dir ]]; then
        # libdatadog-aarch64-alpine-linux-musl
        # libdatadog-{arch}-{os+distro+libc}

        # get the arch
        arch=$(basename "$dir" | cut -d'-' -f2)
        os=$(basename "$dir" | cut -d'-' -f3-)

        # create rids
        if [[ $os == "alpine-linux-musl" ]]; then
            os="linux-musl"
        elif [[ $os == "apple-darwin" ]]; then
            os="osx"
        elif [[ $os == "unknown-linux-gnu" ]]; then
            os="linux"
        elif [[ $os == "windows" ]]; then
            os="win"
        fi

        if [[ $arch == "x86_64" ]]; then
            arch="x64"
        elif [[ $arch == "aarch64" ]]; then
            arch="arm64"
        elif [[ $arch == "x86" ]]; then
            arch="x86"
        elif [[ $arch == "x64" ]]; then
            arch="x64"
        fi

        rid="$os-$arch"

        echo "Copying $dir to sources/runtimes/$rid"
        mkdir -p "sources/runtimes/$rid/native"
        if [[ $os == "win" ]]; then
            cp -r "$dir/release/dynamic/datadog_profiling_ffi.dll" "sources/runtimes/$rid/native/datadog_profiling_ffi.dll"
        elif [[ $os == "osx" ]]; then
            cp -r "$dir/lib/libdatadog_profiling.dylib" "sources/runtimes/$rid/native/datadog_profiling_ffi.dylib"
        else
            cp -r "$dir/lib/libdatadog_profiling.so" "sources/runtimes/$rid/native/datadog_profiling_ffi.so"
        fi
    fi
done

# trim any aplha characters from the version
version=$(echo $version | tr -d 'a-zA-Z')
nuget pack sources/libdatadog.nuspec -OutputDirectory ../../../packages -Version $version