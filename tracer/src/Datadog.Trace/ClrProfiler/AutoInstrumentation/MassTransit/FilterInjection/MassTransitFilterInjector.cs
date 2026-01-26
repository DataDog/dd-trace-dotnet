// <copyright file="MassTransitFilterInjector.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.FilterInjection;

/// <summary>
/// Injects Datadog filter specifications into the MassTransit 7.x pipeline during configuration time.
/// This class uses reflection to navigate the MassTransit internal configuration structure
/// and add filter specifications before the pipeline is compiled.
/// </summary>
internal static class MassTransitFilterInjector
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MassTransitFilterInjector));

    /// <summary>
    /// Injects a Datadog consume filter specification into the MassTransit bus factory configurator.
    /// Must be called during configuration time (inside UsingInMemory/UsingRabbitMq/etc callback).
    /// </summary>
    /// <param name="configurator">The bus factory configurator (e.g., IInMemoryBusFactoryConfigurator)</param>
    /// <returns>True if injection was successful, false otherwise</returns>
    public static bool InjectConsumeFilter(object configurator)
    {
        if (configurator == null)
        {
            Log.Debug("MassTransitFilterInjector: Configurator is null, skipping injection");
            return false;
        }

        try
        {
            Log.Debug("MassTransitFilterInjector: Attempting to inject filter into {ConfiguratorType}", configurator.GetType().FullName);

            // Navigation path (verified working in Samples.MassTransit7):
            // InMemoryBusFactoryConfigurator
            //   └── _busConfiguration (InMemoryBusConfiguration)
            //       └── <Consume>k__BackingField (ConsumePipeConfiguration)
            //           └── Specification (ConsumePipeSpecification)
            //               └── _specifications (List<IPipeSpecification<ConsumeContext>>)

            var busConfiguration = GetBusConfiguration(configurator);
            if (busConfiguration == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find _busConfiguration field");
                return false;
            }

            var consumePipeConfig = GetConsumePipeConfiguration(busConfiguration);
            if (consumePipeConfig == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find consume pipe configuration");
                return false;
            }

            var specification = GetSpecification(consumePipeConfig);
            if (specification == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find specification");
                return false;
            }

            var specifications = GetSpecificationsList(specification);
            if (specifications == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find _specifications list");
                return false;
            }

            // Create and inject the Datadog filter specification
            var filterSpec = CreateDatadogFilterSpecification(specification);
            if (filterSpec == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not create filter specification");
                return false;
            }

            // Insert at beginning to run first in the pipeline
            specifications.Insert(0, filterSpec);
            Log.Information("MassTransitFilterInjector: Successfully injected Datadog filter specification");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MassTransitFilterInjector: Failed to inject filter");
            return false;
        }
    }

    private static object? GetBusConfiguration(object configurator)
    {
        var configuratorType = configurator.GetType();
        var currentType = configuratorType;

        while (currentType != null && currentType != typeof(object))
        {
            var busConfigField = currentType.GetField("_busConfiguration", BindingFlags.NonPublic | BindingFlags.Instance);
            if (busConfigField != null)
            {
                var busConfig = busConfigField.GetValue(configurator);
                Log.Debug<string, string?>(
                    "MassTransitFilterInjector: Found _busConfiguration in {TypeName}: {BusConfigType}",
                    currentType.Name,
                    busConfig?.GetType().FullName);
                return busConfig;
            }

            currentType = currentType.BaseType;
        }

        return null;
    }

    private static object? GetConsumePipeConfiguration(object busConfiguration)
    {
        var busConfigType = busConfiguration.GetType();

        // First try the ConsumePipeConfiguration property
        var consumePipeProperty = busConfigType.GetProperty("ConsumePipeConfiguration", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (consumePipeProperty != null)
        {
            var config = consumePipeProperty.GetValue(busConfiguration);
            if (config != null)
            {
                Log.Debug("MassTransitFilterInjector: Found ConsumePipeConfiguration property: {ConfigType}", config.GetType().FullName);
                return config;
            }
        }

        // Try _consumePipeConfiguration field
        var currentType = busConfigType;
        while (currentType != null && currentType != typeof(object))
        {
            var consumePipeField = currentType.GetField("_consumePipeConfiguration", BindingFlags.NonPublic | BindingFlags.Instance);
            if (consumePipeField != null)
            {
                var config = consumePipeField.GetValue(busConfiguration);
                if (config != null)
                {
                    Log.Debug("MassTransitFilterInjector: Found _consumePipeConfiguration field: {ConfigType}", config.GetType().FullName);
                    return config;
                }
            }

            currentType = currentType.BaseType;
        }

        // Try the backing field for the Consume property (found in EndpointConfiguration base class)
        currentType = busConfigType;
        while (currentType != null && currentType != typeof(object))
        {
            var consumeBackingField = currentType.GetField("<Consume>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (consumeBackingField != null)
            {
                var config = consumeBackingField.GetValue(busConfiguration);
                if (config != null)
                {
                    Log.Debug("MassTransitFilterInjector: Found <Consume>k__BackingField: {ConfigType}", config.GetType().FullName);
                    return config;
                }
            }

            currentType = currentType.BaseType;
        }

        return null;
    }

    private static object? GetSpecification(object consumePipeConfig)
    {
        var consumePipeConfigType = consumePipeConfig.GetType();

        // Try the Specification property
        var specProperty = consumePipeConfigType.GetProperty("Specification", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (specProperty != null)
        {
            var spec = specProperty.GetValue(consumePipeConfig);
            if (spec != null)
            {
                Log.Debug("MassTransitFilterInjector: Found Specification property: {SpecType}", spec.GetType().FullName);
                return spec;
            }
        }

        // Try _specification field
        var currentType = consumePipeConfigType;
        while (currentType != null && currentType != typeof(object))
        {
            var specField = currentType.GetField("_specification", BindingFlags.NonPublic | BindingFlags.Instance);
            if (specField != null)
            {
                var spec = specField.GetValue(consumePipeConfig);
                if (spec != null)
                {
                    Log.Debug("MassTransitFilterInjector: Found _specification field: {SpecType}", spec.GetType().FullName);
                    return spec;
                }
            }

            currentType = currentType.BaseType;
        }

        return null;
    }

    private static IList? GetSpecificationsList(object specification)
    {
        var specType = specification.GetType();
        var currentType = specType;

        while (currentType != null && currentType != typeof(object))
        {
            var specificationsField = currentType.GetField("_specifications", BindingFlags.NonPublic | BindingFlags.Instance);
            if (specificationsField != null)
            {
                var specifications = specificationsField.GetValue(specification);
                if (specifications is IList list)
                {
                    Log.Debug<int>("MassTransitFilterInjector: Found _specifications list with {Count} items", list.Count);
                    return list;
                }
            }

            currentType = currentType.BaseType;
        }

        return null;
    }

    private static object? CreateDatadogFilterSpecification(object specification)
    {
        try
        {
            // We need to create an IPipeSpecification<ConsumeContext> that adds our DatadogConsumeFilter
            // The specification type tells us which assembly's types to use
            var specType = specification.GetType();
            var massTransitAssembly = specType.Assembly;

            // Find the GreenPipes assembly
            var greenPipesAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(asm => asm.GetName().Name == "GreenPipes");

            if (greenPipesAssembly == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find GreenPipes assembly");
                return null;
            }

            // Find the ConsumeContext type in the MassTransit assembly
            var consumeContextType = massTransitAssembly.GetType("MassTransit.ConsumeContext");
            if (consumeContextType == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find ConsumeContext type");
                return null;
            }

            // Find IPipeSpecification<ConsumeContext> interface type
            var pipeSpecOpenType = greenPipesAssembly.GetType("GreenPipes.IPipeSpecification`1");
            if (pipeSpecOpenType == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find IPipeSpecification<> type");
                return null;
            }

            var pipeSpecType = pipeSpecOpenType.MakeGenericType(consumeContextType);
            Log.Debug("MassTransitFilterInjector: Creating reverse duck type for {InterfaceType}", pipeSpecType.FullName);

            // Create a reverse duck type that implements IPipeSpecification<ConsumeContext>
            // by wrapping our DatadogConsumePipeSpecification
            var filterSpecImpl = new DatadogConsumePipeSpecification();
            var filterSpec = DuckType.CreateReverse(pipeSpecType, filterSpecImpl);

            Log.Debug("MassTransitFilterInjector: Created filter specification: {FilterSpecType}", filterSpec?.GetType().FullName);
            return filterSpec;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MassTransitFilterInjector: Failed to create filter specification");
            return null;
        }
    }
}
