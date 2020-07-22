#!/bin/bash
set -euxo pipefail

BRANCH="$(git symbolic-ref HEAD | sed -e 's,.*/\(.*\),\1,')"
SOURCEFOLDER="$( cd "$( dirname "${BASH_SOURCE[0]}" )" >/dev/null && pwd )"
ROOT="$( cd "$( dirname "${BASH_SOURCE[0]}" )/.." >/dev/null && pwd )"
export DOCKER_BUILDKIT=1

docker build -f $SOURCEFOLDER/linux-build.dockerfile --target tracer-build -t dd-trace-dotnet:$BRANCH $ROOT
docker build -f $SOURCEFOLDER/linux-build.dockerfile -t dotnet-sdk-with-dd-tracer:$BRANCH $ROOT
