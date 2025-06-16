set -x

./tracer/build_in_docker.sh BuildTracerHome

# will fail to create the tar, but we don't care
./tracer/build_in_docker.sh ZipMonitoringHome

cp tracer/bin/artifacts/linux-arm64/datadog-dotnet-apm-3.19.0-1.aarch64.rpm tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts

