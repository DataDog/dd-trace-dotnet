# MassTransit 7.x Filter Injection Research

## Overview

This document describes how filters and specifications are stored in MassTransit 7.x, and how they can be injected via reflection for instrumentation purposes.

## Filter/Specification Architecture

MassTransit 7.x uses a **specification pattern** where filters are not directly registered. Instead, specifications that describe filters are collected and then compiled into an actual filter pipeline when the bus starts.

### Key Classes and Collections

#### 1. ReceiveEndpointConfiguration

**Location:** `MassTransit.Configuration.Configuration.ReceiveEndpointConfiguration`

```csharp
readonly IList<IReceiveEndpointSpecification> _specifications;
```

**Key methods:**
- `AddEndpointSpecification(IReceiveEndpointSpecification specification)` - Adds a new specification to the collection
- `ApplySpecifications(IReceiveEndpointBuilder builder)` - Iterates through all specifications and applies them to the builder

#### 2. ConsumePipeSpecification

**Location:** `MassTransit.Configuration.ConsumePipeSpecifications.ConsumePipeSpecification`

```csharp
readonly List<IPipeSpecification<ConsumeContext>> _specifications;
readonly ConcurrentDictionary<Type, IMessageConsumePipeSpecification> _messageSpecifications;
```

- `_specifications` - Global pipe specifications applied to all consume contexts
- `_messageSpecifications` - Message-type-specific specifications (keyed by message type)

#### 3. SpecificationPipeBuilder

**Location:** `MassTransit.Configuration.PipeBuilders.SpecificationPipeBuilder<TContext>`

```csharp
readonly List<IFilter<TContext>> _filters;
```

This is where the actual filter instances are accumulated during the build phase. The `Build()` method chains these filters together using `FilterPipe` wrappers.

## Filter Pipeline Flow

```
Specifications (configuration time)
        ↓
   ApplySpecifications()
        ↓
  SpecificationPipeBuilder._filters
        ↓
      Build()
        ↓
   IPipe<TContext> (runtime filter chain)
```

## Timing for Injection

**Critical:** Filters must be injected **before** the bus is built and started. When using `AddMassTransit` with DI, the bus configuration is compiled during the DI container build phase, **before** you can access `IBusControl`.

### The Problem with Post-Build Injection

Our experiments show that by the time you get `IBusControl` from DI:
- The `ConsumePipe` has already been built from its specifications
- The `_endpoints` collection in `BaseHostConfiguration` is **null** (endpoints are registered differently in DI)
- Adding to `_specifications` at this point has no effect because the pipe is already compiled

### Verified Internal Structure

From runtime reflection, we confirmed the following internal structure:

```
MassTransitBus
├── _consumePipe (ConsumePipe)
│   └── _specification (ConsumePipeSpecification)
│       └── _specifications (List<IPipeSpecification<ConsumeContext>>) ← Injection point, but too late
├── _host (InMemoryHost)
│   └── _hostConfiguration (InMemoryHostConfiguration : BaseHostConfiguration)
│       └── _endpoints (IList) ← NULL when using DI registration
```

## Successful Injection Points

For reflection-based injection to work, you need to hook into the configuration **during** `AddMassTransit`:

### Option 1: Configuration-Time Injection via `UseConsumeFilter`

The supported way in MassTransit 7.x:

```csharp
services.AddMassTransit(x =>
{
    x.AddConsumer<GettingStartedConsumer>();

    x.UsingInMemory((context, cfg) =>
    {
        // Add filter to all consumers
        cfg.UseConsumeFilter(typeof(DatadogFilter<>), context);

        cfg.ConfigureEndpoints(context);
    });
});
```

**Note:** `UseConsumeFilter` requires a filter that can be instantiated for each message type, not for `ConsumeContext` directly.

### Option 2: Via `IPipeSpecificationObserver`

MassTransit has observer interfaces that are notified when specifications are added:
- `IConsumePipeSpecificationObserver`
- `ISendPipeSpecificationObserver`
- `IPublishPipeSpecificationObserver`

### Option 3: Reflection During Configuration Callback (VERIFIED WORKING)

Hook into the `UsingInMemory` (or other transport) callback and inject via reflection. This approach has been verified to work.

**Successful reflection path:**
```
InMemoryBusFactoryConfigurator
    └── _busConfiguration (InMemoryBusConfiguration)
        └── <Consume>k__BackingField (ConsumePipeConfiguration)
            └── Specification (ConsumePipeSpecification)
                └── _specifications (List<IPipeSpecification<ConsumeContext>>)
```

**Working code:**
```csharp
x.UsingInMemory((context, cfg) =>
{
    // cfg is IInMemoryBusFactoryConfigurator
    // Access internal configurators via reflection at configuration time
    ConfigurationTimeInjector.InjectDuringConfiguration(cfg);

    cfg.ConfigureEndpoints(context);
});
```

**Verified output:**
```
[ConfigurationTimeInjector] Configurator type: MassTransit.Transports.InMemory.Configurators.InMemoryBusFactoryConfigurator
[ConfigurationTimeInjector] Found _busConfiguration in InMemoryBusFactoryConfigurator: MassTransit.Transports.InMemory.Configuration.InMemoryBusConfiguration
[ConfigurationTimeInjector] Found <Consume>k__BackingField: MassTransit.Configuration.ConsumePipeConfiguration
[ConfigurationTimeInjector] Found Specification property: MassTransit.ConsumePipeSpecifications.ConsumePipeSpecification
[ConfigurationTimeInjector] Found _specifications: System.Collections.Generic.List`1[[GreenPipes.IPipeSpecification`1[[MassTransit.ConsumeContext, ...
[ConfigurationTimeInjector] >>> INJECTED DatadogFilterSpecification at configuration time! <<<
[DatadogFilterPipeSpecification] Apply() called - adding DatadogFilter to pipeline
[DatadogFilter] BEFORE - Context type: MessageConsumeContext`1
```

## Filter Implementation

To create a filter for MassTransit 7.x, implement `IFilter<T>` from GreenPipes:

```csharp
using GreenPipes;

public class DatadogFilter<T> : IFilter<T>
    where T : class, PipeContext
{
    public void Probe(ProbeContext context)
    {
        context.CreateFilterScope("datadog");
    }

    public async Task Send(T context, IPipe<T> next)
    {
        // Pre-processing: create span, extract headers, etc.
        Console.WriteLine($"[DatadogFilter] BEFORE - {context.GetType().Name}");

        try
        {
            await next.Send(context);
            // Post-processing on success
        }
        catch (Exception ex)
        {
            // Post-processing on failure
            throw;
        }
    }
}
```

And wrap it in a specification:

```csharp
public class DatadogFilterPipeSpecification<T> : IPipeSpecification<T>
    where T : class, PipeContext
{
    public void Apply(IPipeBuilder<T> builder)
    {
        builder.AddFilter(new DatadogFilter<T>());
    }

    public IEnumerable<ValidationResult> Validate()
    {
        return Enumerable.Empty<ValidationResult>();
    }
}
```

## MassTransit 7.x vs 8.x Differences

| Aspect | MassTransit 7.x | MassTransit 8.x |
|--------|----------------|-----------------|
| DI Extensions | Separate package (`MassTransit.Extensions.DependencyInjection`) | Included in main package |
| Bus Auto-Start | Manual via `IBusControl.StartAsync()` | Automatic with Generic Host |
| Pipeline Library | Uses GreenPipes (external) | Internalized pipeline |
| Filter Interface | `GreenPipes.IFilter<T>` | `MassTransit.IFilter<T>` |
| Specification Interface | `GreenPipes.IPipeSpecification<T>` | `MassTransit.IPipeSpecification<T>` |

## Files in This Sample

- `Instrumentation/DatadogFilter.cs` - Sample filter implementation using GreenPipes `IFilter<T>`
- `Instrumentation/DatadogFilterPipeSpecification.cs` - Pipe specification wrapper (in FilterInjector.cs)
- `Instrumentation/FilterInjector.cs` - Post-build injection POC (demonstrates the structure but injection happens too late)
- `Instrumentation/ConfigurationTimeInjector.cs` - **Configuration-time injection (WORKING)** - Injects filters during the UsingInMemory callback

## Key Findings

1. **Post-build injection doesn't work** - The consume pipe is already compiled when you get `IBusControl`
2. **Endpoint configurations are null** - When using DI registration, `BaseHostConfiguration._endpoints` is null
3. **Configuration-time injection WORKS** - Injecting during the `UsingInMemory`/transport callback works because the pipeline hasn't been compiled yet
4. **Successful path**: `cfg._busConfiguration.<Consume>k__BackingField.Specification._specifications`
5. **Filter is properly invoked** - The injected filter wraps message processing with BEFORE/AFTER hooks

## Automatic Instrumentation Implementation

The Datadog tracer implements automatic filter injection using the configuration-time approach. The implementation is in:

**Tracer Source Files:**
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/FilterInjection/UsingInMemoryIntegration.cs` - CallTarget integration that hooks into `UsingInMemory`
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/FilterInjection/MassTransitFilterInjector.cs` - Reflection-based filter injection
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/FilterInjection/DatadogConsumePipeSpecification.cs` - Filter specification
- `tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/MassTransit/FilterInjection/DatadogConsumeFilter.cs` - The filter that creates spans

**How it works:**
1. `UsingInMemoryIntegration` uses `[InstrumentMethod]` to hook `ServiceCollectionBusConfigurator.UsingInMemory`
2. The integration wraps the user's callback using `DelegateInstrumentation`
3. When the callback executes, `MassTransitFilterInjector.InjectConsumeFilter()` is called first
4. The injector navigates the internal MassTransit configuration via reflection and adds `DatadogConsumePipeSpecification`
5. When MassTransit builds the pipeline, `DatadogConsumePipeSpecification.Apply()` adds `DatadogConsumeFilter`
6. `DatadogConsumeFilter.Send()` creates Datadog spans around message processing

**Running the Sample App:**
```bash
# With manual injection (for testing without Datadog tracer)
MASSTRANSIT_MANUAL_INJECTION=true dotnet run

# With automatic instrumentation (requires Datadog tracer)
dotnet run  # Tracer automatically injects filters via CallTarget
```

## Recommendations for Datadog Tracer

For automatic instrumentation without user code changes:

1. **Method instrumentation** (current approach) - Hook `ConsumerSplitFilter.Send`, `SendEndpointPipe.Send`, etc. via CallTarget
   - Pros: No need to inject filters, works with any MassTransit configuration
   - Cons: Limited to instrumenting specific methods, may miss some operations

2. **Configuration-time filter injection** (IMPLEMENTED) - Hook the transport callback (e.g., `UsingInMemory`) via CallTarget and inject filters
   - **Hook point**: `ServiceCollectionBusConfigurator.UsingInMemory` wrapped via `DelegateInstrumentation`
   - **Injection path**: `cfg._busConfiguration.<Consume>k__BackingField.Specification._specifications`
   - Pros: Filter wraps entire message processing, access to full context
   - Cons: Requires hooking into configuration phase, reflection-based

3. **Assembly scanning** - Find and wrap consumer classes at startup

## References

- [MassTransit v7.0.0 Source](https://github.com/MassTransit/MassTransit/tree/v7.0.0)
- Key source files:
  - `src/MassTransit/Configuration/Configuration/ReceiveEndpointConfiguration.cs`
  - `src/MassTransit/Configuration/ConsumePipeSpecifications/ConsumePipeSpecification.cs`
  - `src/MassTransit/Configuration/PipeBuilders/SpecificationPipeBuilder.cs`
  - `src/MassTransit/Pipeline/Filters/` (filter implementations)
