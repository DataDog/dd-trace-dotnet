To run the smoke tests locally, you can do the following.

```bash
mkdir -p tracer/test/test-applications/regression/AspNetCoreSmokeTest/artifacts
mkdir -p tracer/build_data/snapshots
mkdir -p tracer/build_data/logs
```

Download build artifact `linux-packages-debian/datadog-dotnet-apm_{currentversion}_amd64` and copy it to the artifacts folder created above.
Set `SNAPSHOT_CI` to 0 in docker-compose.yml, in the test-agent target.

Then run the following

```bash
# explicitly pull the latest agent version
docker pull ghcr.io/datadog/dd-apm-test-agent/ddapm-test-agent:latest

# build the smoke test docker image (using the artifacts)
docker-compose build --build-arg DOTNETSDK_VERSION=8.0.100 --build-arg RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:6.0-bullseye-slim --build-arg PUBLISH_FRAMEWORK=net6.0 --build-arg INSTALL_CMD="dpkg -i ./datadog-dotnet-apm*_amd64.deb" smoke-tests

# start the test-agent (you may get an error on Windows, just ignore it)
docker-compose run --rm start-test-agent

# start the session
docker-compose exec -T test-agent /usr/bin/curl --fail "http://localhost:8126/test/session/start?test_session_token=LOCALTEST"

# run the tests
docker-compose run --rm -e dockerTag=dd-trace-dotnet/not-set-tester smoke-tests

# end session
docker-compose exec -T test-agent /usr/bin/curl --fail "http://localhost:8126/test/session/snapshot?test_session_token=LOCALTEST&file=/snapshots/smoke_test_snapshots"

# open the url in your browser to see the actual diff
open http://localhost:8126/test/session/snapshot?test_session_token=LOCALTEST&file=/snapshots/smoke_test_snapshots
```
The diff should be explained in the url opened above.


To test and update the .NET Core 2.1 snapshots, use the following steps instead

```bash
# explicitly pull the latest agent version
docker pull ghcr.io/datadog/dd-apm-test-agent/ddapm-test-agent:latest

# build the .NET Core 2.1 smoke test docker image (using the artifacts)
docker-compose build --build-arg DOTNETSDK_VERSION=8.0.100 --build-arg RUNTIME_IMAGE=mcr.microsoft.com/dotnet/aspnet:2.1-bionic --build-arg PUBLISH_FRAMEWORK=netcoreapp2.1 --build-arg INSTALL_CMD="dpkg -i ./datadog-dotnet-apm*_amd64.deb" smoke-tests

# start the test-agent (you may get an error on Windows, just ignore it)
docker-compose run --rm start-test-agent
# start the session
docker-compose exec -T test-agent /usr/bin/curl --fail "http://localhost:8126/test/session/start?test_session_token=LOCALTEST"
# run the tests
docker-compose run --rm -e dockerTag=dd-trace-dotnet/not-set-tester smoke-tests

# end session (.NET Core 2.1)
docker-compose exec -T test-agent /usr/bin/curl --fail "http://localhost:8126/test/session/snapshot?test_session_token=LOCALTEST&file=/snapshots/smoke_test_snapshots_2_1"

# open the url in your browser to see the actual diff
open http://localhost:8126/test/session/snapshot?test_session_token=LOCALTEST&file=/snapshots/smoke_test_snapshots_2_1
```

The diff should be explained in the url opened above.
