VERSION=${1:-'dev'}

echo "Load $VERSION binary "

mkdir -p tooling/ci/binaries
cd tooling/ci/binaries

if [ $VERSION = 'dev' ]; then
    SHA=$(curl --silent https://apmdotnetci.blob.core.windows.net/apm-dotnet-ci-artifacts-master/sha.txt)
    ARCHIVE=$(curl --silent https://apmdotnetci.blob.core.windows.net/apm-dotnet-ci-artifacts-master/index.txt | grep '^datadog-dotnet-apm-[0-9.]*\.tar\.gz$')
    URL=https://apmdotnetci.blob.core.windows.net/apm-dotnet-ci-artifacts-master/$SHA/$ARCHIVE

    echo "Load $URL"
    curl -L --silent $URL --output datadog-dotnet-apm.tar.gz
    DDTRACE_VERSION=$(echo $ARCHIVE | cut -d - -f 4 )
    DDTRACE_VERSION=${DDTRACE_VERSION%.tar.gz}
elif [ $VERSION = 'prod' ]; then
    DDTRACE_VERSION=$(curl -H "Authorization: token $GH_TOKEN" "https://api.github.com/repos/DataDog/dd-trace-dotnet/releases/latest" | grep '"tag_name":' | sed -E 's/.*"v([^"]+)".*/\1/')
    curl -L https://github.com/DataDog/dd-trace-dotnet/releases/download/v${DDTRACE_VERSION}/datadog-dotnet-apm-${DDTRACE_VERSION}.tar.gz --output datadog-dotnet-apm.tar.gz 
else
    echo "Don't know how to load version $VERSION for $TARGET"
    exit -1
fi
echo $DDTRACE_VERSION > LIBRARY_VERSION