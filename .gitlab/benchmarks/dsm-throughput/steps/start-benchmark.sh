#!/usr/bin/env bash

set -ex

current_dir=$(pwd)
# to run timeit
export PATH="$PATH:$HOME/.dotnet/tools"

if [ "$ENABLE_TRACER" = "true" ]; then
    export NATIVE_PROFILER_PATH="dd-trace-dotnet/tracer/tracer-home-linux/linux-x64/Datadog.Trace.ClrProfiler.Native.so"
    export TRACER_HOME_PATH="dd-trace-dotnet/tracer/tracer-home-linux"
    export COR_PROFILER="{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
    export CORECLR_PROFILER="{846F5F1C-F9AE-4B07-969E-05C26BC060D8}"
    export COR_PROFILER_PATH="${current_dir}/${NATIVE_PROFILER_PATH}"
    export CORECLR_PROFILER_PATH="${current_dir}/${NATIVE_PROFILER_PATH}"
    export DD_DOTNET_TRACER_HOME="${current_dir}/${TRACER_HOME_PATH}"

    export CORECLR_ENABLE_PROFILING=1

    if [ "$IS_LOCAL_RUN" = "true" ]; then
        export CORECLR_PROFILER_PATH="/opt/datadog/Datadog.Trace.ClrProfiler.Native.so"
        export DD_DOTNET_TRACER_HOME="/opt/datadog"
    fi
fi

cd ./dd-trace-dotnet/tracer/test/test-applications/throughput/Samples.KafkaBenchmark

if [ ! -f "global.json" ]; then
    cat > global.json << 'EOF'
{
    "sdk": {
        "version": "6.0.0",
        "rollForward": "latestFeature"
    }
}
EOF
fi

# Create Kafka topics for benchmark
for i in $(seq 1 $NUM_THREADS); do
    /usr/local/kafka/bin/kafka-topics.sh --create \
        --bootstrap-server localhost:9092 \
        --replication-factor 1 \
        --partitions 1 \
        --topic "$EXPERIMENT_NAME-$i" \
        --if-not-exists
done

TOOLPATH="$PWD/.dotnet-tools"
rm -rf "$TOOLPATH"
dotnet tool install --tool-path "$TOOLPATH" timeitsharp --version 0.4.5
export PATH="$TOOLPATH:$PATH"

dotnet build -c Release

dotnet timeit --count 25 --warmup 5 --json-exporter -- bin/Release/net6.0/Samples.KafkaBenchmark

mkdir -p "$ARTIFACTS_DIR/$EXPERIMENT_NAME"

set +e

mv jsonexporter* "$ARTIFACTS_DIR/$EXPERIMENT_NAME.json"

# Copy dotnet trace logs to artifacts
if [ "$ENABLE_TRACER" = "true" ]; then
    echo "Copying tracer logs to artifacts..."
    if [ -f "$current_dir/tracer.log" ]; then
        cp "$current_dir/tracer.log" "$ARTIFACTS_DIR/$EXPERIMENT_NAME/tracer.log"
    else
        echo "Tracer.log not found"
    fi

    if [ -d "/var/log/datadog/dotnet" ]; then
        cp -r "/var/log/datadog/dotnet" "$ARTIFACTS_DIR/$EXPERIMENT_NAME/tracer"
    else
        echo "Tracer log dir not found"
    fi
fi

set -e