#!/bin/bash

set -e

# Parse arguments
while [[ "$#" -gt 0 ]]; do
    case $1 in
        --profile) profile="$2"; shift ;;
        --pipeline-id) pipeline_id="$2"; shift ;;
        --object-prefix) object_prefix="$2"; shift ;;
        *) echo "Unknown parameter passed: $1"; exit 1 ;;
    esac
    shift
done

# Check if profile argument is passed
if [ -z "$profile" ]; then
    echo "Usage: $0 --profile <aws-profile> --pipeline-id <pipeline-id> --object-prefix <object-prefix>"
    exit 1
fi

# Get the latest libdatadog build
files=$(aws s3 ls s3://$object_prefix/libdatadog_$pipeline_id --profile $profile | awk '{print $4}')
for file in $files; do
    # check if we have the file already
    if [ -f "release/$file" ]; then
        echo "Skipping download of $file"
        continue
    fi
    aws s3 cp s3://$object_prefix/$file release/ --profile $profile
done

# # extract the files
mkdir -p release/unzipped
files=$(ls release/*.tar.gz)
for file in $files; do
    tar -xzf "$file" -C release/unzipped
done

# copy required binaries to their rid format inside sources/runtimes
dirs=$(ls -d release/unzipped/*/)
for dir in $dirs; do
    echo "Processing $dir"
    base_dir=$(basename $dir)
    if [ $base_dir == "aarch64-alpine-linux-musl" ]; then
        rid="linux-musl-arm64"
    elif [ $base_dir == "aarch64-apple-darwin" ]; then
        rid="osx-arm64"
    elif [ $base_dir == "aarch64-unknown-linux-gnu" ]; then
        rid="linux-arm64"
    elif [ $base_dir == "i686-alpine-linux-musl" ]; then
        echo "Skipping $base_dir"
        continue
    elif [ $base_dir == "i686-unknown-linux-gnu" ]; then
        echo "Skipping $base_dir"
        continue
    elif [ $base_dir == "libdatadog-x64-windows" ]; then
        rid="win-x64"
    elif [ $base_dir == "libdatadog-x86-windows" ]; then
        rid="win-x86"
    elif [ $base_dir == "x86_64-alpine-linux-musl" ]; then
        rid="linux-musl-x64"
    elif [ $base_dir == "x86_64-apple-darwin" ]; then
        rid="osx-x64"
    elif [ $base_dir == "x86_64-unknown-linux-gnu" ]; then
        rid="linux-x64"
    else
        echo "Unknown rid $base_dir"
        continue
    fi


    mkdir -p "sources/runtimes/$rid/native"
    # if linux
    if [[ $rid == *"linux"* ]]; then
        # copy the libdatadog.so to the correct runtime directory
        cp -r "$dir/lib/libdatadog_profiling.so" "sources/runtimes/$rid/native/datadog_profiling_ffi.so"
    elif [[ $rid == *"win"* ]]; then
        # copy the libdatadog.dll to the correct runtime directory
        cp -r "$dir/debug/dynamic/datadog_profiling_ffi.dll" "sources/runtimes/$rid/native/datadog_profiling_ffi.dll"
        cp -r "$dir/debug/dynamic/datadog_profiling_ffi.pdb" "sources/runtimes/$rid/native/datadog_profiling_ffi.pdb"
    elif [[ $rid == *"osx"* ]]; then
        # copy the libdatadog.dylib to the correct runtime directory
        cp -r "$dir/lib/libdatadog_profiling.dylib" "sources/runtimes/$rid/native/datadog_profiling_ffi.dylib"
    fi
done

# trim any aplha characters from the version
version=$pipeline_id.0.0
rm -f ../../../packages/libdatadog.*.nupkg
nuget pack sources/libdatadog.nuspec -OutputDirectory ../../../packages -Version $version
git add ../../../packages/libdatadog.$version.nupkg -f

# update the version in the csproj file
# ../../../tracer/src/Datadog.Trace/Datadog.Trace.csproj
# ../../../build/nupkg_local/libdatadog/test/Console/Console.csproj
echo "Adding libdatadog package version $version to projects"
dotnet add ../../../tracer/src/Datadog.Trace/Datadog.Trace.csproj package libdatadog --version $version
dotnet add ../../../build/nupkg_local/libdatadog/test/Console/Console.csproj package libdatadog --version $version
dotnet run --project ../../../build/nupkg_local/libdatadog/test/Console/Console.csproj
