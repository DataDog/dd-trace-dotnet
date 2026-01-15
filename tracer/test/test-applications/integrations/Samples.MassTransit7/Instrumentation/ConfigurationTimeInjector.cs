using System.Collections;
using System.Reflection;
using GreenPipes;
using MassTransit;

namespace Samples.MassTransit7.Instrumentation;

/// <summary>
/// Demonstrates how to inject filters during the MassTransit configuration phase.
/// This is the approach that works for automatic instrumentation because the pipeline
/// hasn't been built yet at this point.
///
/// For automatic instrumentation via CallTarget, we would hook into:
/// 1. The UsingInMemory/UsingRabbitMq/etc callback invocation
/// 2. The IBusFactoryConfigurator methods
/// </summary>
public static class ConfigurationTimeInjector
{
    /// <summary>
    /// Injects filters during the configuration callback (e.g., inside UsingInMemory).
    /// At this point, the configurator has specification collections that haven't been
    /// compiled into the final pipeline yet.
    /// </summary>
    /// <param name="configurator">The bus factory configurator (e.g., IInMemoryBusFactoryConfigurator)</param>
    public static void InjectDuringConfiguration(object configurator)
    {
        Console.WriteLine($"\n[ConfigurationTimeInjector] === CONFIGURATION-TIME INJECTION ===");
        Console.WriteLine($"[ConfigurationTimeInjector] Configurator type: {configurator.GetType().FullName}");

        try
        {
            // The configurator hierarchy in MassTransit 7.x:
            // IInMemoryBusFactoryConfigurator
            //   -> InMemoryBusFactoryConfigurator : BusFactoryConfigurator<IInMemoryReceiveEndpointConfigurator>
            //      -> Has _busConfiguration (InMemoryBusConfiguration)
            //         -> Has _consumePipeConfiguration (IConsumePipeConfiguration)
            //            -> Has Specification (IConsumePipeSpecification)
            //               -> Has _specifications (List<IPipeSpecification<ConsumeContext>>)

            var configuratorType = configurator.GetType();

            // Try to find _busConfiguration field (inherited from base class)
            object? busConfiguration = null;
            var currentType = configuratorType;
            while (currentType != null && currentType != typeof(object))
            {
                var busConfigField = currentType.GetField("_busConfiguration", BindingFlags.NonPublic | BindingFlags.Instance);
                if (busConfigField != null)
                {
                    busConfiguration = busConfigField.GetValue(configurator);
                    Console.WriteLine($"[ConfigurationTimeInjector] Found _busConfiguration in {currentType.Name}: {busConfiguration?.GetType().FullName}");
                    break;
                }
                currentType = currentType.BaseType;
            }

            if (busConfiguration == null)
            {
                Console.WriteLine("[ConfigurationTimeInjector] Could not find _busConfiguration field");
                LogAllFields(configuratorType, "Configurator");
                return;
            }

            // Now find the consume pipe configuration
            var busConfigType = busConfiguration.GetType();
            object? consumePipeConfig = null;

            // Try _consumePipeConfiguration property or field
            var consumePipeProperty = busConfigType.GetProperty("ConsumePipeConfiguration", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (consumePipeProperty != null)
            {
                consumePipeConfig = consumePipeProperty.GetValue(busConfiguration);
                Console.WriteLine($"[ConfigurationTimeInjector] Found ConsumePipeConfiguration property: {consumePipeConfig?.GetType().FullName}");
            }

            if (consumePipeConfig == null)
            {
                currentType = busConfigType;
                while (currentType != null && currentType != typeof(object))
                {
                    var consumePipeField = currentType.GetField("_consumePipeConfiguration", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (consumePipeField != null)
                    {
                        consumePipeConfig = consumePipeField.GetValue(busConfiguration);
                        Console.WriteLine($"[ConfigurationTimeInjector] Found _consumePipeConfiguration field: {consumePipeConfig?.GetType().FullName}");
                        break;
                    }
                    currentType = currentType.BaseType;
                }
            }

            // Based on runtime analysis, the InMemoryBusConfiguration inherits from InMemoryEndpointConfiguration
            // which inherits from EndpointConfiguration which has <Consume>k__BackingField
            if (consumePipeConfig == null)
            {
                // Try the backing field for the Consume property
                currentType = busConfigType;
                while (currentType != null && currentType != typeof(object))
                {
                    var consumeBackingField = currentType.GetField("<Consume>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (consumeBackingField != null)
                    {
                        consumePipeConfig = consumeBackingField.GetValue(busConfiguration);
                        Console.WriteLine($"[ConfigurationTimeInjector] Found <Consume>k__BackingField: {consumePipeConfig?.GetType().FullName}");
                        break;
                    }
                    currentType = currentType.BaseType;
                }
            }

            if (consumePipeConfig == null)
            {
                Console.WriteLine("[ConfigurationTimeInjector] Could not find consume pipe configuration");
                LogAllFields(busConfigType, "BusConfiguration");
                return;
            }

            // Get the Specification property (IConsumePipeSpecification)
            var consumePipeConfigType = consumePipeConfig.GetType();
            var specProperty = consumePipeConfigType.GetProperty("Specification", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            object? specification = null;

            if (specProperty != null)
            {
                specification = specProperty.GetValue(consumePipeConfig);
                Console.WriteLine($"[ConfigurationTimeInjector] Found Specification property: {specification?.GetType().FullName}");
            }

            if (specification == null)
            {
                // Try _specification field
                currentType = consumePipeConfigType;
                while (currentType != null && currentType != typeof(object))
                {
                    var specField = currentType.GetField("_specification", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (specField != null)
                    {
                        specification = specField.GetValue(consumePipeConfig);
                        Console.WriteLine($"[ConfigurationTimeInjector] Found _specification field: {specification?.GetType().FullName}");
                        break;
                    }
                    currentType = currentType.BaseType;
                }
            }

            if (specification == null)
            {
                Console.WriteLine("[ConfigurationTimeInjector] Could not find specification");
                LogAllFields(consumePipeConfigType, "ConsumePipeConfiguration");
                return;
            }

            // Now get the _specifications list
            InjectIntoSpecification(specification);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConfigurationTimeInjector] Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static void InjectIntoSpecification(object specification)
    {
        var specType = specification.GetType();
        var currentType = specType;

        while (currentType != null && currentType != typeof(object))
        {
            var specificationsField = currentType.GetField("_specifications", BindingFlags.NonPublic | BindingFlags.Instance);
            if (specificationsField != null)
            {
                var specifications = specificationsField.GetValue(specification);
                if (specifications != null)
                {
                    Console.WriteLine($"[ConfigurationTimeInjector] Found _specifications: {specifications.GetType().FullName}");

                    if (specifications is IList list)
                    {
                        Console.WriteLine($"[ConfigurationTimeInjector] Specifications count BEFORE: {list.Count}");
                        foreach (var spec in list)
                        {
                            Console.WriteLine($"[ConfigurationTimeInjector]   - {spec?.GetType().FullName}");
                        }

                        // Create and add our filter specification
                        var filterSpec = new DatadogFilterPipeSpecification<ConsumeContext>();
                        list.Insert(0, filterSpec); // Insert at beginning to run first
                        Console.WriteLine($"[ConfigurationTimeInjector] >>> INJECTED DatadogFilterSpecification at configuration time! <<<");
                        Console.WriteLine($"[ConfigurationTimeInjector] Specifications count AFTER: {list.Count}");
                    }
                }
                return;
            }

            currentType = currentType.BaseType;
        }

        Console.WriteLine("[ConfigurationTimeInjector] Could not find _specifications field");
        LogAllFields(specType, "Specification");
    }

    private static void LogAllFields(Type type, string label)
    {
        Console.WriteLine($"\n[ConfigurationTimeInjector] === {label} all fields ===");
        var currentType = type;
        while (currentType != null && currentType != typeof(object))
        {
            var fields = currentType.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (fields.Length > 0)
            {
                Console.WriteLine($"[ConfigurationTimeInjector] {currentType.Name} fields:");
                foreach (var field in fields.Take(15))
                {
                    Console.WriteLine($"[ConfigurationTimeInjector]   {field.Name}: {field.FieldType.Name}");
                }
            }
            currentType = currentType.BaseType;
        }
        Console.WriteLine();
    }
}
