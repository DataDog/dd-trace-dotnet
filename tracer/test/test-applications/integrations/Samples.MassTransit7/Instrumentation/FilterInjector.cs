using System.Collections;
using System.Reflection;
using GreenPipes;
using MassTransit;

namespace Samples.MassTransit7.Instrumentation;

/// <summary>
/// Injects Datadog filters into the MassTransit pipeline via reflection.
/// This is a proof-of-concept for instrumenting MassTransit 7.x without modifying user code.
/// </summary>
public static class FilterInjector
{
    /// <summary>
    /// Injects filters into the MassTransit bus configuration before it starts.
    /// Must be called AFTER the bus is configured but BEFORE StartAsync().
    /// </summary>
    public static void InjectFilters(IBusControl busControl)
    {
        Console.WriteLine($"[FilterInjector] Attempting to inject filters into bus: {busControl.GetType().FullName}");

        try
        {
            var busType = busControl.GetType();

            // Get the _host field (IHost -> InMemoryHost)
            var hostField = busType.GetField("_host", BindingFlags.NonPublic | BindingFlags.Instance);
            if (hostField != null)
            {
                var host = hostField.GetValue(busControl);
                if (host != null)
                {
                    Console.WriteLine($"[FilterInjector] Found host: {host.GetType().FullName}");
                    InjectIntoHost(host);
                }
            }

            // Also get the _consumePipe to access the IConsumePipeSpecification
            var consumePipeField = busType.GetField("_consumePipe", BindingFlags.NonPublic | BindingFlags.Instance);
            if (consumePipeField != null)
            {
                var consumePipe = consumePipeField.GetValue(busControl);
                if (consumePipe != null)
                {
                    Console.WriteLine($"[FilterInjector] Found consume pipe: {consumePipe.GetType().FullName}");
                    InjectIntoConsumePipeSpecification(consumePipe);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FilterInjector] Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void InjectIntoHost(object host)
    {
        var hostType = host.GetType();

        // Get _hostConfiguration (from the host itself or from base class)
        var configField = hostType.GetField("_hostConfiguration", BindingFlags.NonPublic | BindingFlags.Instance);
        if (configField == null)
        {
            // Try base type
            var baseType = hostType.BaseType;
            if (baseType != null)
            {
                configField = baseType.GetField("_hostConfiguration", BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        if (configField != null)
        {
            var hostConfig = configField.GetValue(host);
            if (hostConfig != null)
            {
                Console.WriteLine($"[FilterInjector] Found host configuration: {hostConfig.GetType().FullName}");
                InjectIntoHostConfiguration(hostConfig);
            }
        }
    }

    private static void InjectIntoHostConfiguration(object hostConfig)
    {
        var configType = hostConfig.GetType();
        LogAllFields(configType, "HostConfiguration");

        // The _endpoints field is in the base class BaseHostConfiguration
        // We need to search through the type hierarchy
        var currentType = configType;
        while (currentType != null && currentType != typeof(object))
        {
            var endpointsField = currentType.GetField("_endpoints", BindingFlags.NonPublic | BindingFlags.Instance);
            if (endpointsField != null)
            {
                Console.WriteLine($"[FilterInjector] Found _endpoints field in {currentType.Name}");
                var endpoints = endpointsField.GetValue(hostConfig);
                Console.WriteLine($"[FilterInjector] _endpoints value: {(endpoints == null ? "null" : endpoints.GetType().FullName)}");

                if (endpoints != null)
                {
                    if (endpoints is IList list)
                    {
                        Console.WriteLine($"[FilterInjector] Endpoints count: {list.Count}");
                        foreach (var endpoint in list)
                        {
                            if (endpoint != null)
                            {
                                Console.WriteLine($"[FilterInjector] Processing endpoint: {endpoint.GetType().FullName}");
                                InjectIntoReceiveEndpointConfiguration(endpoint);
                            }
                        }
                    }
                    else
                    {
                        // Maybe it's IEnumerable but not IList
                        Console.WriteLine($"[FilterInjector] _endpoints is not IList, trying IEnumerable");
                        if (endpoints is IEnumerable enumerable)
                        {
                            foreach (var endpoint in enumerable)
                            {
                                if (endpoint != null)
                                {
                                    Console.WriteLine($"[FilterInjector] Processing endpoint: {endpoint.GetType().FullName}");
                                    InjectIntoReceiveEndpointConfiguration(endpoint);
                                }
                            }
                        }
                    }
                }
                return;
            }
            currentType = currentType.BaseType;
        }
        Console.WriteLine($"[FilterInjector] Could not find _endpoints field");
    }

    private static void InjectIntoReceiveEndpointConfiguration(object endpointConfig)
    {
        var configType = endpointConfig.GetType();
        Console.WriteLine($"[FilterInjector] Endpoint config type: {configType.FullName}");

        // Navigate up the inheritance chain to find _consumePipe
        // InMemoryReceiveEndpointConfiguration has _consumePipe (ConsumePipeConfiguration)
        var currentType = configType;
        while (currentType != null && currentType != typeof(object))
        {
            var consumePipeField = currentType.GetField("_consumePipe", BindingFlags.NonPublic | BindingFlags.Instance);
            if (consumePipeField != null)
            {
                var consumePipeConfig = consumePipeField.GetValue(endpointConfig);
                if (consumePipeConfig != null)
                {
                    Console.WriteLine($"[FilterInjector] Found _consumePipe in endpoint: {consumePipeConfig.GetType().FullName}");
                    InjectIntoConsumePipeConfiguration(consumePipeConfig);
                    return;
                }
            }

            currentType = currentType.BaseType;
        }

        // Log all fields for debugging
        LogAllFields(configType, "ReceiveEndpointConfig");
    }

    private static void InjectIntoConsumePipeSpecification(object consumePipe)
    {
        var pipeType = consumePipe.GetType();

        // ConsumePipe has _specification (IConsumePipeSpecification)
        var specField = pipeType.GetField("_specification", BindingFlags.NonPublic | BindingFlags.Instance);
        if (specField != null)
        {
            var specification = specField.GetValue(consumePipe);
            if (specification != null)
            {
                Console.WriteLine($"[FilterInjector] Found _specification: {specification.GetType().FullName}");
                InjectIntoConsumePipeConfiguration(specification);
            }
        }
    }

    private static void InjectIntoConsumePipeConfiguration(object consumePipeConfig)
    {
        var configType = consumePipeConfig.GetType();

        // Look for _specifications (List<IPipeSpecification<ConsumeContext>>)
        var currentType = configType;
        while (currentType != null && currentType != typeof(object))
        {
            var specificationsField = currentType.GetField("_specifications", BindingFlags.NonPublic | BindingFlags.Instance);
            if (specificationsField != null)
            {
                var specifications = specificationsField.GetValue(consumePipeConfig);
                if (specifications != null)
                {
                    Console.WriteLine($"[FilterInjector] Found consume pipe _specifications: {specifications.GetType().FullName}");

                    if (specifications is IList list)
                    {
                        Console.WriteLine($"[FilterInjector] Consume pipe specifications count BEFORE: {list.Count}");
                        foreach (var spec in list)
                        {
                            Console.WriteLine($"[FilterInjector]   - {spec?.GetType().FullName}");
                        }

                        // Create and add our filter specification
                        var filterSpec = CreateDatadogFilterSpecification();
                        if (filterSpec != null)
                        {
                            list.Add(filterSpec);
                            Console.WriteLine($"[FilterInjector] >>> INJECTED DatadogFilterSpecification! <<<");
                            Console.WriteLine($"[FilterInjector] Consume pipe specifications count AFTER: {list.Count}");
                        }
                    }
                }
                return;
            }

            currentType = currentType.BaseType;
        }

        // Log all fields for debugging
        LogAllFields(configType, "ConsumePipeConfig");
    }

    private static object? CreateDatadogFilterSpecification()
    {
        try
        {
            // We need to create an IPipeSpecification<ConsumeContext> that adds our DatadogFilter
            var spec = new DatadogFilterPipeSpecification<ConsumeContext>();
            Console.WriteLine($"[FilterInjector] Created filter specification: {spec.GetType().FullName}");
            return spec;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[FilterInjector] Failed to create filter specification: {ex.Message}");
            return null;
        }
    }

    private static void LogAllFields(Type type, string label)
    {
        Console.WriteLine($"\n[FilterInjector] === {label} all fields ===");
        var currentType = type;
        while (currentType != null && currentType != typeof(object))
        {
            var fields = currentType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (fields.Length > 0)
            {
                Console.WriteLine($"[FilterInjector] {currentType.Name} fields:");
                foreach (var field in fields.Take(15))
                {
                    Console.WriteLine($"[FilterInjector]   {field.Name}: {field.FieldType.Name}");
                }
            }
            currentType = currentType.BaseType;
        }
        Console.WriteLine();
    }
}

/// <summary>
/// A pipe specification that adds the DatadogFilter to the pipeline.
/// This implements IPipeSpecification{T} which is what MassTransit uses to build the filter chain.
/// </summary>
/// <typeparam name="T">The context type</typeparam>
public class DatadogFilterPipeSpecification<T> : IPipeSpecification<T>
    where T : class, PipeContext
{
    public void Apply(IPipeBuilder<T> builder)
    {
        Console.WriteLine($"[DatadogFilterPipeSpecification] Apply() called - adding DatadogFilter to pipeline");
        builder.AddFilter(new DatadogFilter<T>());
    }

    public IEnumerable<ValidationResult> Validate()
    {
        return Enumerable.Empty<ValidationResult>();
    }
}
