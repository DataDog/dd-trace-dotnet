#!/bin/bash
set -euxo pipefail

# The purpose of this bash is to build two docker images based on the current git branch
# The first image has SCRATCH as a based image and contains only the /dd-tracer-dotnet 
# folder with the publish version of the tracer. Useful to use in a dockerfile as a CopyFrom image.
# The second image has the dotnet sdk 3.1 as a base image + the /dd-tracer-dotnet folder.
# Because is a linux build we try to use BuildKit as engine to parallelize the building process.

BRANCH="$(git symbolic-ref HEAD | sed -e 's,.*/\(.*\),\1,')"
SOURCEFOLDER="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"
ROOT="$( cd "$( dirname "${BASH_SOURCE[0]}" )/.." >/dev/null && pwd )"
export DOCKER_BUILDKIT=1

docker build -f $SOURCEFOLDER/linux-build.dockerfile --target tracer-build -t dd-trace-dotnet:$BRANCH $ROOT
docker build -f $SOURCEFOLDER/linux-build.dockerfile -t dotnet-sdk-with-dd-tracer:$BRANCH $ROOT
