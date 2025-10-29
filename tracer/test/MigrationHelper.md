# TestContainers Migration Helper

## Quick Reference: Finding Tests to Migrate

### Find all tests with docker dependencies
```bash
# Find tests that need migration
grep -r "RequiresDockerDependency" tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/ | grep "\.cs:" | cut -d: -f1 | sort -u
```

### Check which tests are already migrated
```bash
# Find tests using ContainerFixtures
grep -r "IClassFixture<.*Fixture>" tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/ | grep "\.cs:"
```

### Find docker-compose environment variables used
```bash
# From docker-compose.yml IntegrationTests service
grep "^      [A-Z_]*=" docker-compose.yml
```

## Docker-Compose to Fixture Mapping

Based on docker-compose.yml environment variables, use these fixtures:

| Environment Variable | Fixture to Use | Constructor Change |
|---------------------|----------------|-------------------|
| `MONGO_HOST` | `MongoDbFixture` | Add fixture param + `ConfigureContainers(mongoFixture)` |
| `SERVICESTACK_REDIS_HOST` | `RedisFixture` | Add fixture param + `ConfigureContainers(redisFixture)` |
| `STACKEXCHANGE_REDIS_HOST` | `RedisFixture` | Add fixture param + `ConfigureContainers(redisFixture)` |
| `REDIS_HOST` | `RedisFixture` | Add fixture param + `ConfigureContainers(redisFixture)` |
| `ELASTICSEARCH5_HOST` | `Elasticsearch5Fixture` | Add fixture param + `ConfigureContainers(esFixture)` |
| `ELASTICSEARCH6_HOST` | `Elasticsearch6Fixture` | Add fixture param + `ConfigureContainers(esFixture)` |
| `ELASTICSEARCH7_HOST` | `Elasticsearch7Fixture` | Add fixture param + `ConfigureContainers(esFixture)` |
| `SQLSERVER_CONNECTION_STRING` | `SqlServerFixture` | Add fixture param + `ConfigureContainers(sqlFixture)` |
| `POSTGRES_HOST` | `PostgreSqlFixture` | Add fixture param + `ConfigureContainers(pgFixture)` |
| `MYSQL_HOST` | `MySqlFixture` | Add fixture param + `ConfigureContainers(mysqlFixture)` |
| `RABBITMQ_HOST` | `RabbitMqFixture` | Add fixture param + `ConfigureContainers(rabbitFixture)` |
| `KAFKA_BROKER_HOST` | `KafkaFixture` | Add fixture param + `ConfigureContainers(kafkaFixture)` |
| `AWS_SDK_HOST` | `LocalStackFixture` | Add fixture param + `ConfigureContainers(localStackFixture)` |
| `COUCHBASE_HOST` | `CouchbaseFixture` | Add fixture param + `ConfigureContainers(couchbaseFixture)` |
| `AEROSPIKE_HOST` | `AerospikeFixture` | Add fixture param + `ConfigureContainers(aeroFixture)` |

## Step-by-Step Migration

### 1. Identify Test File
Example: `ServiceStackRedisTests.cs`

### 2. Check What Dependencies It Uses
Look in the test methods for environment variable usage or check docker-compose.yml

### 3. Add Using Statement
```csharp
using Datadog.Trace.TestHelpers.AutoInstrumentation.Containers;
```

### 4. Add IClassFixture Interface
```csharp
// Before
public class ServiceStackRedisTests : TracingIntegrationTest

// After
public class ServiceStackRedisTests : TracingIntegrationTest, IClassFixture<RedisFixture>
```

### 5. Update Constructor
```csharp
// Before
public ServiceStackRedisTests(ITestOutputHelper output)
    : base("ServiceStack.Redis", output)
{
    SetServiceVersion("1.0.0");
}

// After
public ServiceStackRedisTests(ITestOutputHelper output, RedisFixture redisFixture)
    : base("ServiceStack.Redis", output)
{
    SetServiceVersion("1.0.0");
    ConfigureContainers(redisFixture);
}
```

### 6. Build and Test
```bash
cd tracer
./build.sh --target BuildAndRunManagedUnitTests --filter "FullyQualifiedName~ServiceStackRedisTests"
```

## Batch Migration Script

For teams wanting to migrate multiple tests at once, here's a pattern:

```bash
#!/bin/bash
# migrate-tests.sh - Helper to identify tests needing migration

echo "=== Tests with Docker Dependencies ==="
grep -l "RequiresDockerDependency" tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/*.cs | \
  xargs -I {} bash -c 'echo -n "{}: "; grep -c "IClassFixture" {} || echo "0"' | \
  grep ":0$" | cut -d: -f1

echo ""
echo "=== Already Migrated Tests ==="
grep -l "IClassFixture<.*Fixture>" tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/*.cs

echo ""
echo "=== Migration Progress ==="
total=$(grep -l "RequiresDockerDependency" tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/*.cs | wc -l)
migrated=$(grep -l "IClassFixture<.*Fixture>" tracer/test/Datadog.Trace.ClrProfiler.IntegrationTests/*.cs | wc -l)
echo "Migrated: $migrated / $total tests"
echo "Remaining: $((total - migrated)) tests"
```

## Common Patterns

### Redis Tests
```csharp
public class MyRedisTests : TracingIntegrationTest, IClassFixture<RedisFixture>
{
    public MyRedisTests(ITestOutputHelper output, RedisFixture redisFixture)
        : base("MyRedis", output)
    {
        ConfigureContainers(redisFixture);
    }
}
```

### Database Tests
```csharp
public class MyDatabaseTests : TracingIntegrationTest,
    IClassFixture<PostgreSqlFixture>,
    IClassFixture<MySqlFixture>
{
    public MyDatabaseTests(
        ITestOutputHelper output,
        PostgreSqlFixture postgresFixture,
        MySqlFixture mysqlFixture)
        : base("MyDatabase", output)
    {
        ConfigureContainers(postgresFixture, mysqlFixture);
    }
}
```

### Message Queue Tests
```csharp
public class MyMessageTests : TracingIntegrationTest,
    IClassFixture<RabbitMqFixture>,
    IClassFixture<KafkaFixture>
{
    public MyMessageTests(
        ITestOutputHelper output,
        RabbitMqFixture rabbitFixture,
        KafkaFixture kafkaFixture)
        : base("MyMessages", output)
    {
        ConfigureContainers(rabbitFixture, kafkaFixture);
    }
}
```

## Fixtures Still Needed

If you need to create a fixture that doesn't exist yet, follow the pattern in `TestContainersMigration.md`. Common ones that might still be needed:

- Oracle Database
- Azure SQL Edge
- Azure Service Bus Emulator
- Azure Event Hubs Emulator
- OpenLDAP
- Azurite (Azure Storage)

Template for new fixture:

```csharp
public class MyServiceFixture : ContainerFixture
{
    private const int ServicePort = XXXX;

    protected IContainer Container => GetResource<IContainer>("container");

    public override IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables()
    {
        yield return new("MY_SERVICE_HOST",
            $"{Container.Hostname}:{Container.GetMappedPublicPort(ServicePort)}");
    }

    protected override async Task InitializeResources(Action<string, object> registerResource)
    {
        // Keep synchronized with docker-compose.yml
        var container = new ContainerBuilder()
                       .WithImage("my-service:version")
                       .WithPortBinding(ServicePort, true)
                       .WithWaitStrategy(Wait.ForUnixContainer()
                           .UntilPortIsAvailable(ServicePort))
                       .Build();

        await container.StartAsync();
        registerResource("container", container);
    }
}
```

## Testing Your Migration

### Local Testing
```bash
# Run specific test
dotnet test --filter "FullyQualifiedName~YourTestClass"

# Run all docker-dependent tests
dotnet test --filter "RequiresDockerDependency=true"
```

### Verify Container Cleanup
```bash
# Check running containers before test
docker ps

# Run test
dotnet test --filter "FullyQualifiedName~YourTestClass"

# Check containers after - should be same as before
docker ps
```

## Troubleshooting

### "Container failed to start"
- Check wait strategy timeout
- Verify image exists: `docker pull <image>`
- Check container logs for errors

### "Environment variable not set in test"
- Ensure `ConfigureContainers(fixture)` is called in constructor
- Verify `GetEnvironmentVariables()` returns correct keys

### "Port already in use"
- Use `.WithPortBinding(port, true)` for automatic port assignment
- Check for leaked containers: `docker ps -a`

## Rollout Strategy

Recommended approach:

1. **Phase 1**: Migrate 5-10 simple tests (e.g., single Redis/MongoDB tests)
2. **Phase 2**: Migrate complex tests with multiple dependencies
3. **Phase 3**: Create missing fixtures for remaining services
4. **Phase 4**: Remove docker-compose dependency from CI

This allows validation at each stage without breaking existing tests.
