#!/bin/sh
set -eu

if [ "${USE_NATIVE_SDK_VERSION:-false}" = true ]; then
    sdk_version=${LINUX_NATIVE_DOTNET_SDK_VERSION:?Missing native SDK metadata}
else
    sdk_version=${LINUX_DOTNET_SDK_VERSION:?Missing managed SDK metadata}
fi

# Build outside the checkout so its global.json cannot select the managed SDK
# for CentOS/universal toolchains. Never copy producer bin/obj into this build.
mkdir -p /build
tar -C /project/tracer/build/_build --exclude='./bin' --exclude='./obj' -cf /tmp/nuke-source.tar .
tar -C /build -xf /tmp/nuke-source.tar
rm /tmp/nuke-source.tar
printf '{"sdk":{"version":"%s","rollForward":"disable"}}\n' "$sdk_version" > /build/global.json
cd /build
dotnet build _build.csproj --configuration Debug --nologo

cd /project
exec dotnet /build/bin/Debug/_build.dll "$@"
