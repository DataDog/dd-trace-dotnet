# TestContainers Migration Guide

## Overview

This guide documents the migration from docker-compose to TestContainers for integration tests. TestContainers provides better resource management, isolation, and reduces CI flake by starting only the dependencies needed for each test.

## Benefits

- **Reduced Resource Usage**: Only start containers needed for specific tests
- **Better Isolation**: Each test class can have its own container instance
- **Faster CI**: Containers start/stop as needed instead of all at once
- **Less Flake**: Reduces timeout issues from resource contention
- **Self-Documenting**: Test dependencies are explicit in test class declarations
- **Lifecycle Management**: Containers automatically disposed after tests complete

## Architecture

### ContainerFixture Base Class

All container fixtures inherit from `ContainerFixture` which provides:

```csharp
public abstract class ContainerFixture : IAsyncLifetime
{
    // Async initialization
    public async Task InitializeAsync() { ... }

    // Returns environment variables for test configuration
    public virtual IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables() { ... }

    // Implement to create and register container resources
    protected abstract Task InitializeResources(Action<string, object> registerResource);

    // Access registered resources
    protected T GetResource<T>(string key) { ... }
}
```

### ContainersRegistry

Manages container lifecycle with reference counting:

- **Singleton Pattern**: One container instance per fixture type shared across tests
- **Thread-Safe**: Uses `ConcurrentDictionary` for parallel test execution
- **Reference Counting**: Tracks usage and disposes when no longer needed
- **Automatic Disposal**: Cleans up containers when tests complete

## Creating a New Fixture

### Basic Example: Redis

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;

public class RedisFixture : ContainerFixture
{
    private const int RedisPort = 6379;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        var host = $"{Container.Hostname}:{Container.GetMappedPublicPort(RedisPort)}";
        yield return new("REDIS_HOST", host);
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage("redis:4-alpine")
                       .WithPortBinding(RedisPort, true)
                       .WithWaitStrategy(Wait.ForUnixContainer()
                           .UntilCommandIsCompleted("redis-cli", "ping"))
                       .Build();

        await container.StartAsync();
        registerResource("container", container);
    }
}
```

### Advanced Example: Multi-Container (Kafka + Zookeeper)

```csharp
public class KafkaFixture : ContainerFixture
{
    private const int ZookeeperPort = 2181;
    private const int KafkaPort = 9092;

    protected INetwork Network => GetResource<INetwork>("network");
    protected IContainer ZookeeperContainer => GetResource<IContainer>("zookeeper");
    protected IContainer KafkaContainer => GetResource<IContainer>("kafka");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("KAFKA_BROKER_HOST",
            $"{KafkaContainer.Hostname}:{KafkaContainer.GetMappedPublicPort(KafkaPort)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Create network for inter-container communication
        var network = new NetworkBuilder()
                     .WithName($"kafka-network-{Guid.NewGuid():N}")
                     .Build();
        await network.CreateAsync();
        registerResource("network", network);

        // Start Zookeeper first
        var zookeeperContainer = new ContainerBuilder()
                                .WithImage("confluentinc/cp-zookeeper:6.1.1")
                                .WithNetwork(network)
                                .WithNetworkAliases("zookeeper")
                                .WithPortBinding(ZookeeperPort, true)
                                .WithEnvironment("ZOOKEEPER_CLIENT_PORT", ZookeeperPort.ToString())
                                .WithWaitStrategy(Wait.ForUnixContainer()
                                    .UntilPortIsAvailable(ZookeeperPort))
                                .Build();
        await zookeeperContainer.StartAsync();
        registerResource("zookeeper", zookeeperContainer);

        // Start Kafka broker
        var kafkaContainer = new ContainerBuilder()
                            .WithImage("confluentinc/cp-kafka:6.1.1")
                            .WithNetwork(network)
                            .WithEnvironment("KAFKA_ZOOKEEPER_CONNECT", $"zookeeper:{ZookeeperPort}")
                            .WithPortBinding(KafkaPort, true)
                            .WithWaitStrategy(Wait.ForUnixContainer()
                                .UntilPortIsAvailable(KafkaPort))
                            .Build();
        await kafkaContainer.StartAsync();
        registerResource("kafka", kafkaContainer);
    }
}
```

## Migrating Integration Tests

### Before (docker-compose based)

```csharp
[Trait("RequiresDockerDependency", "true")]
public class ServiceStackRedisTests : TracingIntegrationTest
{
    public ServiceStackRedisTests(ITestOutputHelper output)
        : base("ServiceStack.Redis", output)
    {
        SetServiceVersion("1.0.0");
    }

    [SkippableTheory]
    [MemberData(nameof(GetEnabledConfig))]
    public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
    {
        // Test reads SERVICESTACK_REDIS_HOST from environment
        // Environment set by docker-compose.yml
    }
}
```

### After (TestContainers)

```csharp
[Trait("RequiresDockerDependency", "true")]
public class ServiceStackRedisTests : TracingIntegrationTest, IClassFixture<RedisFixture>
{
    public ServiceStackRedisTests(ITestOutputHelper output, RedisFixture redisFixture)
        : base("ServiceStack.Redis", output)
    {
        SetServiceVersion("1.0.0");
        ConfigureContainers(redisFixture);  // Injects environment variables
    }

    [SkippableTheory]
    [MemberData(nameof(GetEnabledConfig))]
    public async Task SubmitsTraces(string packageVersion, string metadataSchemaVersion)
    {
        // Test automatically gets SERVICESTACK_REDIS_HOST from fixture
        // No change to test logic required
    }
}
```

### Multiple Fixtures

```csharp
public class ComplexIntegrationTests : TracingIntegrationTest,
    IClassFixture<MongoDbFixture>,
    IClassFixture<RedisFixture>,
    IClassFixture<PostgreSqlFixture>
{
    public ComplexIntegrationTests(
        ITestOutputHelper output,
        MongoDbFixture mongoFixture,
        RedisFixture redisFixture,
        PostgreSqlFixture postgresFixture)
        : base("ComplexIntegration", output)
    {
        ConfigureContainers(mongoFixture, redisFixture, postgresFixture);
    }
}
```

## Available Fixtures

| Fixture | Image | Environment Variables | Notes |
|---------|-------|----------------------|-------|
| `RedisFixture` | redis:4-alpine | REDIS_HOST, SERVICESTACK_REDIS_HOST, STACKEXCHANGE_REDIS_HOST | Single Redis instance |
| `MongoDbFixture` | mongo:4.0.9 | MONGO_HOST | MongoDB 4.0 |
| `PostgreSqlFixture` | postgres:10.5 | POSTGRES_HOST, POSTGRES_PORT | PostgreSQL 10 |
| `MySqlFixture` | mysql:8.0 | MYSQL_HOST, MYSQL_PORT | MySQL 8.0 |
| `SqlServerFixture` | mcr.microsoft.com/mssql/server:2022-latest | SQLSERVER_CONNECTION_STRING | SQL Server 2022 |
| `Elasticsearch5Fixture` | elasticsearch:5.6.16 | ELASTICSEARCH5_HOST | ES 5.x |
| `Elasticsearch6Fixture` | elasticsearch:6.8.23 | ELASTICSEARCH6_HOST | ES 6.x |
| `Elasticsearch7Fixture` | elasticsearch:7.17.18 | ELASTICSEARCH7_HOST | ES 7.x |
| `RabbitMqFixture` | rabbitmq:3-management | RABBITMQ_HOST, RABBITMQ_PORT | RabbitMQ with management |
| `KafkaFixture` | confluentinc/cp-kafka:6.1.1 | KAFKA_BROKER_HOST | Kafka + Zookeeper |
| `CouchbaseFixture` | couchbase:latest | COUCHBASE_HOST, COUCHBASE_PORT | Couchbase server |
| `LocalStackFixture` | localstack/localstack:latest | AWS_SDK_HOST | AWS service emulation |
| `AerospikeFixture` | aerospike/aerospike-server:6.2.0.6 | AEROSPIKE_HOST | Aerospike database |

## Wait Strategies

TestContainers supports various wait strategies to ensure containers are ready:

### Command Completion
```csharp
.WithWaitStrategy(Wait.ForUnixContainer()
    .UntilCommandIsCompleted("redis-cli", "ping"))
```

### Port Availability
```csharp
.WithWaitStrategy(Wait.ForUnixContainer()
    .UntilPortIsAvailable(3000))
```

### HTTP Endpoint
```csharp
.WithWaitStrategy(Wait.ForUnixContainer()
    .UntilHttpRequestIsSucceeded(r => r
        .ForPort(9200)
        .ForPath("/_cluster/health")
        .ForStatusCode(System.Net.HttpStatusCode.OK)))
```

### Log Message
```csharp
.WithWaitStrategy(Wait.ForUnixContainer()
    .UntilMessageIsLogged("database system is ready to accept connections"))
```

## Best Practices

### 1. Keep Image Versions Synchronized
Always add a comment to keep versions in sync with docker-compose.yml:
```csharp
// Keep synchronized image version with docker-compose.yml
var container = new ContainerBuilder()
               .WithImage("redis:4-alpine")
```

### 2. Use Specific Image Tags
Avoid `latest` tags for reproducibility:
```csharp
.WithImage("postgres:10.5")  // Good - specific version
.WithImage("postgres:latest") // Bad - version can change
```

### 3. Implement Proper Wait Strategies
Don't rely on port availability alone - verify the service is actually ready:
```csharp
// Bad - port may be open but service not ready
.WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(5432))

// Good - verify service responds correctly
.WithWaitStrategy(Wait.ForUnixContainer()
    .UntilCommandIsCompleted("pg_isready", "-U", "postgres"))
```

### 4. Clean Resource Names
Use descriptive resource keys:
```csharp
registerResource("container", container);      // Good
registerResource("zookeeper", zkContainer);    // Good
registerResource("kafka", kafkaContainer);     // Good
registerResource("c1", container);             // Bad
```

### 5. Handle Multi-Container Dependencies
Start dependencies in order:
```csharp
// Start Zookeeper before Kafka
await zookeeperContainer.StartAsync();
registerResource("zookeeper", zookeeperContainer);

await kafkaContainer.StartAsync();
registerResource("kafka", kafkaContainer);
```

## Migration Checklist

For each integration test file:

- [ ] Identify required docker-compose services
- [ ] Create fixture(s) if they don't exist
- [ ] Add `IClassFixture<FixtureName>` to test class
- [ ] Add fixture parameter to constructor
- [ ] Call `ConfigureContainers(fixture)` in constructor
- [ ] Verify environment variables match docker-compose.yml
- [ ] Test locally to ensure containers start properly
- [ ] Remove dependency on docker-compose for this test

## CI/CD Integration

### Current State
Tests currently require `docker-compose up -d` before running.

### After Migration
Once all tests are migrated:
1. Remove docker-compose startup from CI pipelines
2. Tests will start only the containers they need
3. Update build scripts to remove docker-compose references

### Partial Migration
During migration, both approaches can coexist:
- Migrated tests use TestContainers
- Unmigrated tests still use docker-compose
- Eventually docker-compose can be removed

## Troubleshooting

### Container Fails to Start
Check the wait strategy - it may be timing out:
```csharp
// Add logging to debug
.WithWaitStrategy(Wait.ForUnixContainer()
    .UntilPortIsAvailable(3000)
    .WithStartupTimeout(TimeSpan.FromMinutes(2)))
```

### Environment Variables Not Set
Verify `ConfigureContainers()` is called in constructor:
```csharp
public MyTests(ITestOutputHelper output, RedisFixture fixture)
    : base("MyIntegration", output)
{
    ConfigureContainers(fixture);  // Don't forget this!
}
```

### Container Stays Running After Tests
The `ContainersRegistry` handles disposal automatically. If containers aren't being cleaned up, check:
- `IAsyncLifetime` is implemented correctly
- Test runner is completing normally (not crashing)

### Port Conflicts
TestContainers automatically assigns random host ports. If you need specific ports:
```csharp
.WithPortBinding(6379, 6379)  // host:container - may conflict
.WithPortBinding(6379, true)  // Assign random host port - preferred
```

## Example PRs

See these examples for reference:
- ServiceStackRedisTests migration (example commit)
- AerospikeTests (original implementation)

## Additional Resources

- [TestContainers .NET Documentation](https://dotnet.testcontainers.org/)
- [DotNet.Testcontainers GitHub](https://github.com/testcontainers/testcontainers-dotnet)
- Original discussion in Slack (link to relevant thread)

## Summary

TestContainers provides a modern, efficient approach to integration testing that:
- Reduces CI resource usage and flake
- Improves test isolation and reliability
- Makes test dependencies explicit and self-documenting
- Simplifies local development (no need to manually start docker-compose)

Migration is straightforward and can be done incrementally without breaking existing tests.
