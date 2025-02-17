#!/bin/bash

set -e

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
# mkdir -p sources/runtimes
# Download release files
# for url in "${urls[@]}"; do
#     filename=$(basename "$url")

#     echo "Downloading $url... at release/$filename"
#     curl -L -o "release/$filename" "$url"
# done

# # extract the files
# mkdir -p release/unzipped
# for file in release/*; do
#     if [[ $file == *.tar.gz ]]; then
#         echo "Extracting $file..."
#         tar -xzf "$file" -C release/unzipped
#     elif [[ $file == *.zip ]]; then
#         echo "Extracting $file..."
#         # unzip with overwrite
#         unzip -q "$file" -d release/unzipped -o
#     fi
# done

# artifacts.zip                                              libdatadog_55986604_24558711_i686-alpine-linux-musl.tar    libdatadog_55986604_24558711_x86_64-unknown-linux-gnu.tar
# libdatadog_55986604_24558711_aarch64-alpine-linux-musl.tar libdatadog_55986604_24558711_i686-unknown-linux-gnu.tar
# libdatadog_55986604_24558711_aarch64-unknown-linux-gnu.tar libdatadog_55986604_24558711_x86_64-alpine-linux-musl.tar

# extract pipeline artifacts
mkdir -p release/unzipped
# for file in release/*; do
#     if [[ $file == *.tar ]]; then
#         echo "Extracting $file..."
#         tar -xf "$file" -C release/unzipped
#     elif [[ $file == *.zip ]]; then
#         echo "Extracting $file..."
#         # unzip with overwrite
#         unzip -q "$file" -d release/unzipped
#         unzip -q release/unzipped/artifacts/libdatadog-x64-windows.zip -d release/unzipped
#         unzip -q release/unzipped/artifacts/libdatadog-x86-windows.zip -d release/unzipped
#         rm -rf release/unzipped/artifacts
#     fi
# done

# copy required binaries to their rid format inside sources/runtimes
# aarch64-alpine-linux-musl i686-alpine-linux-musl    libdatadog-x64-windows    version.txt               x86_64-unknown-linux-gnu
# aarch64-unknown-linux-gnu i686-unknown-linux-gnu    libdatadog-x86-windows    x86_64-alpine-linux-musl
for dir in release/unzipped/*; do
    # if dir is any linux distro
    # they follow same structure
    # check if the dir contains linux and then follow {arch}-{os+distro+libc}
    if [[ $dir == *-linux-* ]]; then
        # get the arch
        arch=$(basename "$dir" | cut -d'-' -f1)
        os=$(basename "$dir" | cut -d'-' -f2-)

        container_dir=$(basename "$dir")

        # create rids
        if [[ $container_dir == "aarch64-alpine-linux-musl" ]]; then
            rid="linux-musl-arm64"
        elif [[ $container_dir == "aarch64-unknown-linux-gnu" ]]; then
            rid="linux-arm64"
        elif [[ $container_dir == "x86_64-alpine-linux-musl" ]]; then
            rid="linux-musl-x64"
        elif [[ $container_dir == "x86_64-unknown-linux-gnu" ]]; then
            rid="linux-x64"
        else
            echo "Unknown linux distro $container_dir"
            continue
        fi

        echo "Copying $dir to sources/runtimes/$rid"
        mkdir -p "sources/runtimes/$rid/native"
        cp -r "$dir/lib/libdatadog_profiling.so" "sources/runtimes/$rid/native/datadog_profiling_ffi.so"
    fi

    # if dir is windows
    # they follow same structure libdatadog-{arch}-{os}
    # check if the dir contains windows and then follow libdatadog-{arch}-{os}
    if [[ $dir == *windows ]]; then
        # get the arch
        arch=$(basename "$dir" | cut -d'-' -f2)
        os=$(basename "$dir" | cut -d'-' -f3)

        # create rids
        if [[ $os == "windows" ]]; then
            os="win"
        fi

        if [[ $arch == "x86" ]]; then
            arch="x86"
        elif [[ $arch == "x64" ]]; then
            arch="x64"
        fi

        rid="$os-$arch"

        echo "Copying $dir to sources/runtimes/$rid"
        mkdir -p "sources/runtimes/$rid/native"
        cp -r "$dir/release/dynamic/datadog_profiling_ffi.dll" "sources/runtimes/$rid/native/datadog_profiling_ffi.dll"
    fi

    # if dir is apple
    # they follow same structure {arch}-{distro}
    # check if the dir contains apple and then follow {arch}-{distro}
    if [[ $dir == *apple* ]]; then
        container_dir=$(basename "$dir")

        # create rids
        if [[ $container_dir == "aarch64-apple-darwin" ]]; then
            rid="osx-arm64"
        elif [[ $container_dir == "x86_64-apple-darwin" ]]; then
            rid="osx-x64"
        else
            echo "Unknown apple distro $container_dir"
            continue
        fi

        echo "Copying $dir to sources/runtimes/$rid"
        mkdir -p "sources/runtimes/$rid/native"
        cp -r "$dir/lib/libdatadog_profiling.dylib" "sources/runtimes/$rid/native/datadog_profiling_ffi.dylib"
    fi
done

# copy required binaries to their rid format inside sources/runtimes
# {os]-{distro}-{arch}
# os: linux, osx, win
# for dir in release/unzipped/*; do
#     if [[ -d $dir ]]; then
#         # libdatadog-aarch64-alpine-linux-musl
#         # libdatadog-{arch}-{os+distro+libc}

#         # get the arch
#         arch=$(basename "$dir" | cut -d'-' -f2)
#         os=$(basename "$dir" | cut -d'-' -f3-)

#         # create rids
#         if [[ $os == "alpine-linux-musl" ]]; then
#             os="linux-musl"
#         elif [[ $os == "apple-darwin" ]]; then
#             os="osx"
#         elif [[ $os == "unknown-linux-gnu" ]]; then
#             os="linux"
#         elif [[ $os == "windows" ]]; then
#             os="win"
#         fi

#         if [[ $arch == "x86_64" ]]; then
#             arch="x64"
#         elif [[ $arch == "aarch64" ]]; then
#             arch="arm64"
#         elif [[ $arch == "x86" ]]; then
#             arch="x86"
#         elif [[ $arch == "x64" ]]; then
#             arch="x64"
#         fi

#         rid="$os-$arch"

#         echo "Copying $dir to sources/runtimes/$rid"
#         mkdir -p "sources/runtimes/$rid/native"
#         if [[ $os == "win" ]]; then
#             cp -r "$dir/release/dynamic/datadog_profiling_ffi.dll" "sources/runtimes/$rid/native/datadog_profiling_ffi.dll"
#         elif [[ $os == "osx" ]]; then
#             cp -r "$dir/lib/libdatadog_profiling.dylib" "sources/runtimes/$rid/native/datadog_profiling_ffi.dylib"
#         else
#             cp -r "$dir/lib/libdatadog_profiling.so" "sources/runtimes/$rid/native/datadog_profiling_ffi.so"
#         fi
#     fi
# done

# trim any aplha characters from the version
version=55986604.0.0
nuget pack sources/libdatadog.nuspec -OutputDirectory ../../../packages -Version $version

# update the version in the csproj file
# ../../../tracer/src/Datadog.Trace/Datadog.Trace.csproj
# ../../../build/nupkg_local/libdatadog/test/Console/Console.csproj
echo "Adding libdatadog package version $version to projects"
dotnet add ../../../tracer/src/Datadog.Trace/Datadog.Trace.csproj package libdatadog --version $version
dotnet add ../../../build/nupkg_local/libdatadog/test/Console/Console.csproj package libdatadog --version $version
