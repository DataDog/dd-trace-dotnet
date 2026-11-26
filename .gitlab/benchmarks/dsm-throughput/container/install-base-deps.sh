    #!/usr/bin/env bash

    set -e

    apt-get update && apt-get install -y openjdk-11-jdk
    echo "java -version: $(java -version)"

    apt-get update && apt-get install -y dotnet-sdk-6.0
    dotnet --version

    dotnet tool install --global TimeItSharp