set -x

# explicitly pull the latest agent version
docker pull ghcr.io/datadog/dd-apm-test-agent/ddapm-test-agent:latest

# build the smoke test docker image (using the artifacts)
docker-compose build --build-arg DOTNETSDK_VERSION=9.0.203 --build-arg RUNTIME_IMAGE=andrewlock/dotnet-fedora-arm64:35-7.0 --build-arg PUBLISH_FRAMEWORK=net7.0 --build-arg INSTALL_CMD="rpm -Uvh ./datadog-dotnet-apm*-1.aarch64.rpm" smoke-tests

# start the test-agent (you may get an error on Windows, just ignore it)
docker-compose run --rm start-test-agent

# start the session
docker-compose exec -T test-agent /usr/bin/curl --fail "http://localhost:8126/test/session/start?test_session_token=LOCALTEST"

# run the tests
docker-compose run --rm -e dockerTag=dd-trace-dotnet/not-set-tester smoke-tests
# -e LD_DEBUG=libs,symbols,bindings 

# end session
docker-compose exec -T test-agent /usr/bin/curl --fail "http://localhost:8126/test/session/snapshot?test_session_token=LOCALTEST&file=/snapshots/smoke_test_snapshots"
