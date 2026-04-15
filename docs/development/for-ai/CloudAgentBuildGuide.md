# Cloud Agent Build & Test Guide

Guide for building and testing the dd-trace-dotnet tracer in a cloud or headless environment without an IDE.

## Recommended Environment

There are two Docker-based paths. Both use .NET 10.0.100 SDK (pinned in `global.json`) and pre-build the Nuke build tool inside the image.

### Option 1: build_in_docker (one-shot builds)

Best for running specific Nuke targets headlessly. The scripts build an Alpine-based image and run Nuke directly:

```bash
# Linux/macOS
./tracer/build_in_docker.sh BuildTracerHome

# Windows (PowerShell)
./tracer/build_in_docker.ps1 BuildTracerHome
```

- **Dockerfile**: `tracer/build/_build/docker/alpine.dockerfile`
- **Base**: Alpine (musl libc) with clang pre-installed
- **Docker stage**: `builder` (SDK only, no extra ASP.NET runtimes)
- **Mounts**: Bind-mounts the repo to `/project`
- **Limitation**: The `builder` stage lacks ASP.NET Core runtimes 2.1-9.0, so integration tests targeting older TFMs will fail. Only the `tester` stage has those runtimes.

### Option 2: Dev Container (interactive sessions)

Best for longer sessions where you need to run multiple commands:

- **Dockerfile**: `tracer/build/_build/docker/debian.dockerfile`
- **Base**: Debian (buster-slim, glibc) with clang-16
- **Docker stage**: `tester` (includes ASP.NET Core runtimes 2.1-9.0)
- **Config**: `.devcontainer/devcontainer.json`
- **Network**: `--network=host`

The Dev Container is closer to production (glibc, not musl) and has the extra runtimes needed for full integration testing.

### Choosing between them

| Scenario | Recommended path |
|---|---|
| Quick managed-only validation | build_in_docker |
| Full integration tests | Dev Container (has extra runtimes) |
| Native build matching production | Dev Container (Debian/glibc) |
| Alpine-specific testing | build_in_docker |

## Without Docker: Host Prerequisites

If Docker is not available, install the following directly on the host.

### Required tools

- **.NET SDK 10.0.100** (pinned in `global.json`, `rollForward: minor`)
- **Clang/LLVM 16+** (native tracer and loader compilation)
- **CMake** (native build system)
- **make** + **build-essential** / equivalent (Linux/macOS build toolchain)

### System libraries

The full list of system-level packages depends on the OS. Rather than duplicating it here, refer to the Dockerfiles as the authoritative source:

- **Debian/Ubuntu (glibc)**: See `tracer/build/_build/docker/debian.dockerfile` (uuid-dev, zlib1g-dev, liblzma-dev, autoconf, libtool, etc.)
- **Alpine (musl)**: See `tracer/build/_build/docker/alpine.dockerfile` (util-linux-dev, zlib-dev, xz-dev, alpine-sdk, etc.)

### Extra ASP.NET Core runtimes (integration tests only)

Unit tests only need the build SDK. Integration tests targeting older TFMs need these additional runtimes, installable via the [dotnet-install script](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script):

```bash
# Channels: 2.1, 3.0, 3.1, 5.0, 6.0, 7.0, 8.0, 9.0
./dotnet-install.sh --runtime aspnetcore --channel <version> --install-dir <dotnet-root> --no-path
```

Note: ASP.NET Core 2.1 is not available for ARM64. The Dockerfiles install the base `dotnet` runtime instead on that architecture.

### Windows

On Windows without Docker, see `tracer/README.md` for the full Visual Studio 2022 requirements (Desktop C++, .NET desktop development workloads, and components listed in `.vsconfig`).

## Build Sequence

The build system uses [Nuke](https://nuke.build/). List all targets with:

```bash
./tracer/build.sh --help
```

### Target dependency chain

```
BuildTracerHome (default target)
  |- CompileManagedLoader
  |- BuildNativeTracerHome
  |    |- CompileTracerNativeSrc
  |    |- PublishNativeTracer
  |- BuildManagedTracerHome
  |    |- Restore
  |    |- CompileManagedSrc
  |    |- PublishManagedTracer
  |- BuildNativeLoader
       |- CompileNativeLoader
       |- PublishNativeLoader
```

`BuildTracerHome` is the main entry point. Most other targets (unit tests, integration tests, packaging) assume it has already been run.

### Common parameters

- `-BuildConfiguration [Release|Debug]` (default: Release)
- `-TargetPlatform [x86|x64|ARM64]` (default: current platform)
- `-Framework [net461|netcoreapp3_1|net6_0|etc]` (target specific framework)
- `-Filter "Category=MyCategory"` (filter tests)
- `-FastDevLoop true` (skips expensive clean steps during iteration)

## Quick Validation

For managed-only changes (no native code modified), skip the full `BuildTracerHome` and use:

```bash
# Fastest: build just the managed tracer for one TFM
dotnet build tracer/src/Datadog.Trace/ -c Release -f net8.0

# Then run managed unit tests
./tracer/build.sh BuildAndRunManagedUnitTests
```

For changes that touch native code, run the full build first:

```bash
./tracer/build.sh BuildTracerHome
./tracer/build.sh BuildAndRunManagedUnitTests
./tracer/build.sh RunNativeUnitTests
```

## Full Test Workflows

### Unit tests

```bash
# Managed unit tests (fastest feedback loop)
./tracer/build.sh BuildAndRunManagedUnitTests

# Native unit tests
./tracer/build.sh RunNativeUnitTests
```

No Docker services are required for unit tests.

### Integration tests

Integration tests require external services running via Docker Compose. Services are organized into profiles:

```bash
# Start Group 1 services (RabbitMQ, Redis, Postgres, MySQL, SQL Server, Couchbase, Kafka)
docker-compose --profile group1 up -d

# Start Group 2 services (Elasticsearch, MongoDB, LocalStack/AWS, Azure emulators)
docker-compose --profile group2 up -d

# Or use the orchestrator services that wait for health checks
docker-compose up StartDependencies.Group1
docker-compose up StartDependencies.Group2
```

Then run integration tests:

```bash
# Linux
./tracer/build.sh BuildAndRunLinuxIntegrationTests

# Windows
.\tracer\build.cmd BuildAndRunWindowsIntegrationTests

# macOS
./tracer/build.sh BuildAndRunOsxIntegrationTests
```

To run a targeted subset:

```bash
./tracer/build.sh BuildAndRunLinuxIntegrationTests \
  --framework "net8.0" \
  --filter "Datadog.Trace.ClrProfiler.IntegrationTests.RabbitMQTests" \
  --SampleName "Samples.Rabbit"
```

### Docker Compose service groups

<details>
<summary>Group 1: Message brokers, relational databases, key-value stores</summary>

- `rabbitmq`
- `stackexchangeredis`, `stackexchangeredis-replica`, `stackexchangeredis-single`, `servicestackredis`
- `postgres`
- `mysql`, `mysql57`
- `sqlserver`
- `couchbase`
- `kafka-zookeeper`, `kafka-broker`, `kafka-schema-registry`, `kafka-control-center`, `kafka-rest-proxy`

</details>

<details>
<summary>Group 2: Search engines, document stores, cloud emulators</summary>

- `elasticsearch5`, `elasticsearch6`, `elasticsearch7`
- `mongo`
- `localstack` (AWS: SNS, SQS, Kinesis, DynamoDB, Lambda, S3)
- `sqledge` (Azure SQL Edge)
- `azureservicebus-emulator`
- `azurite` (Azure Storage)
- `azure-eventhubs-emulator`
- `cosmosdb-emulator`

</details>

<details>
<summary>Always active (no profile required)</summary>

- `test-agent` (Datadog test agent)
- `oracle`
- `wcfservice`

</details>

### Packaging

After `BuildTracerHome` has been run:

```bash
./tracer/build.sh PackageTracerHome
```

This produces NuGet packages, MSIs, and zip distributions.

## Resource Requirements

- **RAM**: 16GB minimum. Builds can OOM below this (error: `MSB6006: "csc.dll" exited with code 137`).
- **Disk**: ~10GB for repo + build artifacts + NuGet cache + Docker images.

## Troubleshooting

- **`MSB6006: "csc.dll" exited with code 137`**: Out of memory. Increase container/VM memory to 16GB+.
- **Native build failures on macOS**: Delete `cmake-build-debug` and `obj_*` directories, then re-run `xcode-select --install`.
- **Missing runtimes in build_in_docker**: The default `builder` stage only has the build SDK. Integration tests need the `tester` stage with ASP.NET Core runtimes 2.1-9.0.
- **View current build configuration**: Run `./tracer/build.sh Info`.
