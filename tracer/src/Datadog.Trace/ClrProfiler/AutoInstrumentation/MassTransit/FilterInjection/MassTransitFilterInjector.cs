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
    /// Injects Datadog consume and send filter specifications into the MassTransit bus factory configurator.
    /// Must be called during configuration time (inside UsingInMemory/UsingRabbitMq/etc callback).
    /// </summary>
    /// <param name="configurator">The bus factory configurator (e.g., IInMemoryBusFactoryConfigurator)</param>
    /// <returns>True if at least one filter was injected successfully, false otherwise</returns>
    public static bool InjectConsumeFilter(object configurator)
    {
        if (configurator == null)
        {
            Log.Debug("MassTransitFilterInjector: Configurator is null, skipping injection");
            return false;
        }

        var consumeSuccess = false;
        var sendSuccess = false;

        try
        {
            Log.Debug("MassTransitFilterInjector: Attempting to inject filters into {ConfiguratorType}", configurator.GetType().FullName);

            var busConfiguration = GetBusConfiguration(configurator);
            if (busConfiguration == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find _busConfiguration field");
                return false;
            }

            // Inject consume filter
            consumeSuccess = InjectConsumeFilterInternal(busConfiguration);

            // Inject send filter (for explicit Send() calls)
            sendSuccess = InjectSendFilterInternal(busConfiguration);

            // Inject publish filter (for Publish() calls)
            var publishSuccess = InjectPublishFilterInternal(busConfiguration);

            if (consumeSuccess || sendSuccess || publishSuccess)
            {
                Log.Information("MassTransitFilterInjector: Successfully injected filters (consume: {ConsumeSuccess}, send: {SendSuccess}, publish: {PublishSuccess})", consumeSuccess, sendSuccess, publishSuccess);
            }

            return consumeSuccess || sendSuccess || publishSuccess;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "MassTransitFilterInjector: Failed to inject filters");
            return false;
        }
    }

    private static bool InjectConsumeFilterInternal(object busConfiguration)
    {
        try
        {
            // Navigation path:
            // BusConfiguration
            //   └── <Consume>k__BackingField (ConsumePipeConfiguration)
            //       └── Specification (ConsumePipeSpecification)
            //           └── _specifications (List<IPipeSpecification<ConsumeContext>>)

            var consumePipeConfig = GetConsumePipeConfiguration(busConfiguration);
            if (consumePipeConfig == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find consume pipe configuration");
                return false;
            }

            var specification = GetSpecification(consumePipeConfig);
            if (specification == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find consume specification");
                return false;
            }

            var specifications = GetSpecificationsList(specification);
            if (specifications == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find consume _specifications list");
                return false;
            }

            var filterSpec = CreateDatadogConsumeFilterSpecification(specification);
            if (filterSpec == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not create consume filter specification");
                return false;
            }

            specifications.Insert(0, filterSpec);
            Log.Debug("MassTransitFilterInjector: Successfully injected Datadog consume filter specification");
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MassTransitFilterInjector: Failed to inject consume filter");
            return false;
        }
    }

    private static bool InjectSendFilterInternal(object busConfiguration)
    {
        try
        {
            // Navigation path:
            // BusConfiguration
            //   └── <Send>k__BackingField (SendPipeConfiguration)
            //       └── Specification (SendPipeSpecification)
            //           └── _specifications (List<IPipeSpecification<SendContext>>)

            var sendPipeConfig = GetSendPipeConfiguration(busConfiguration);
            if (sendPipeConfig == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find send pipe configuration");
                return false;
            }

            var specification = GetSpecification(sendPipeConfig);
            if (specification == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find send specification");
                return false;
            }

            var specifications = GetSpecificationsList(specification);
            if (specifications == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find send _specifications list");
                return false;
            }

            var filterSpec = CreateDatadogSendFilterSpecification(specification);
            if (filterSpec == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not create send filter specification");
                return false;
            }

            specifications.Insert(0, filterSpec);
            Log.Debug("MassTransitFilterInjector: Successfully injected Datadog send filter specification");
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MassTransitFilterInjector: Failed to inject send filter");
            return false;
        }
    }

    private static bool InjectPublishFilterInternal(object busConfiguration)
    {
        try
        {
            // Navigation path:
            // BusConfiguration
            //   └── <Publish>k__BackingField (PublishPipeConfiguration)
            //       └── Specification (PublishPipeSpecification)
            //           └── _specifications (List<IPipeSpecification<PublishContext>>)

            var publishPipeConfig = GetPublishPipeConfiguration(busConfiguration);
            if (publishPipeConfig == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find publish pipe configuration");
                return false;
            }

            var specification = GetSpecification(publishPipeConfig);
            if (specification == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find publish specification");
                return false;
            }

            var specifications = GetSpecificationsList(specification);
            if (specifications == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find publish _specifications list");
                return false;
            }

            var filterSpec = CreateDatadogPublishFilterSpecification(specification);
            if (filterSpec == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not create publish filter specification");
                return false;
            }

            specifications.Insert(0, filterSpec);
            Log.Debug("MassTransitFilterInjector: Successfully injected Datadog publish filter specification");
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MassTransitFilterInjector: Failed to inject publish filter");
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

    private static object? GetSendPipeConfiguration(object busConfiguration)
    {
        var busConfigType = busConfiguration.GetType();

        // First try the SendPipeConfiguration property
        var sendPipeProperty = busConfigType.GetProperty("SendPipeConfiguration", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (sendPipeProperty != null)
        {
            var config = sendPipeProperty.GetValue(busConfiguration);
            if (config != null)
            {
                Log.Debug("MassTransitFilterInjector: Found SendPipeConfiguration property: {ConfigType}", config.GetType().FullName);
                return config;
            }
        }

        // Try _sendPipeConfiguration field
        var currentType = busConfigType;
        while (currentType != null && currentType != typeof(object))
        {
            var sendPipeField = currentType.GetField("_sendPipeConfiguration", BindingFlags.NonPublic | BindingFlags.Instance);
            if (sendPipeField != null)
            {
                var config = sendPipeField.GetValue(busConfiguration);
                if (config != null)
                {
                    Log.Debug("MassTransitFilterInjector: Found _sendPipeConfiguration field: {ConfigType}", config.GetType().FullName);
                    return config;
                }
            }

            currentType = currentType.BaseType;
        }

        // Try the backing field for the Send property (found in EndpointConfiguration base class)
        currentType = busConfigType;
        while (currentType != null && currentType != typeof(object))
        {
            var sendBackingField = currentType.GetField("<Send>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (sendBackingField != null)
            {
                var config = sendBackingField.GetValue(busConfiguration);
                if (config != null)
                {
                    Log.Debug("MassTransitFilterInjector: Found <Send>k__BackingField: {ConfigType}", config.GetType().FullName);
                    return config;
                }
            }

            currentType = currentType.BaseType;
        }

        return null;
    }

    private static object? GetPublishPipeConfiguration(object busConfiguration)
    {
        var busConfigType = busConfiguration.GetType();

        // First try the PublishPipeConfiguration property
        var publishPipeProperty = busConfigType.GetProperty("PublishPipeConfiguration", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (publishPipeProperty != null)
        {
            var config = publishPipeProperty.GetValue(busConfiguration);
            if (config != null)
            {
                Log.Debug("MassTransitFilterInjector: Found PublishPipeConfiguration property: {ConfigType}", config.GetType().FullName);
                return config;
            }
        }

        // Try _publishPipeConfiguration field
        var currentType = busConfigType;
        while (currentType != null && currentType != typeof(object))
        {
            var publishPipeField = currentType.GetField("_publishPipeConfiguration", BindingFlags.NonPublic | BindingFlags.Instance);
            if (publishPipeField != null)
            {
                var config = publishPipeField.GetValue(busConfiguration);
                if (config != null)
                {
                    Log.Debug("MassTransitFilterInjector: Found _publishPipeConfiguration field: {ConfigType}", config.GetType().FullName);
                    return config;
                }
            }

            currentType = currentType.BaseType;
        }

        // Try the backing field for the Publish property (found in EndpointConfiguration base class)
        currentType = busConfigType;
        while (currentType != null && currentType != typeof(object))
        {
            var publishBackingField = currentType.GetField("<Publish>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            if (publishBackingField != null)
            {
                var config = publishBackingField.GetValue(busConfiguration);
                if (config != null)
                {
                    Log.Debug("MassTransitFilterInjector: Found <Publish>k__BackingField: {ConfigType}", config.GetType().FullName);
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

    private static object? CreateDatadogConsumeFilterSpecification(object specification)
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

            Log.Debug("MassTransitFilterInjector: Created consume filter specification: {FilterSpecType}", filterSpec?.GetType().FullName);
            return filterSpec;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MassTransitFilterInjector: Failed to create consume filter specification");
            return null;
        }
    }

    private static object? CreateDatadogSendFilterSpecification(object specification)
    {
        try
        {
            var specType = specification.GetType();
            var massTransitAssembly = specType.Assembly;

            var greenPipesAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(asm => asm.GetName().Name == "GreenPipes");

            if (greenPipesAssembly == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find GreenPipes assembly for send filter");
                return null;
            }

            var sendContextType = massTransitAssembly.GetType("MassTransit.SendContext");
            if (sendContextType == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find SendContext type");
                return null;
            }

            var pipeSpecOpenType = greenPipesAssembly.GetType("GreenPipes.IPipeSpecification`1");
            if (pipeSpecOpenType == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find IPipeSpecification<> type for send filter");
                return null;
            }

            var pipeSpecType = pipeSpecOpenType.MakeGenericType(sendContextType);
            Log.Debug("MassTransitFilterInjector: Creating reverse duck type for send filter: {InterfaceType}", pipeSpecType.FullName);

            var filterSpecImpl = new DatadogSendPipeSpecification();
            var filterSpec = DuckType.CreateReverse(pipeSpecType, filterSpecImpl);

            Log.Debug("MassTransitFilterInjector: Created send filter specification: {FilterSpecType}", filterSpec?.GetType().FullName);
            return filterSpec;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MassTransitFilterInjector: Failed to create send filter specification");
            return null;
        }
    }

    private static object? CreateDatadogPublishFilterSpecification(object specification)
    {
        try
        {
            var specType = specification.GetType();
            var massTransitAssembly = specType.Assembly;

            var greenPipesAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(asm => asm.GetName().Name == "GreenPipes");

            if (greenPipesAssembly == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find GreenPipes assembly for publish filter");
                return null;
            }

            // PublishContext inherits from SendContext, so we can use the same filter
            // but need to create a specification for the correct context type
            var publishContextType = massTransitAssembly.GetType("MassTransit.PublishContext");
            if (publishContextType == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find PublishContext type");
                return null;
            }

            var pipeSpecOpenType = greenPipesAssembly.GetType("GreenPipes.IPipeSpecification`1");
            if (pipeSpecOpenType == null)
            {
                Log.Debug("MassTransitFilterInjector: Could not find IPipeSpecification<> type for publish filter");
                return null;
            }

            var pipeSpecType = pipeSpecOpenType.MakeGenericType(publishContextType);
            Log.Debug("MassTransitFilterInjector: Creating reverse duck type for publish filter: {InterfaceType}", pipeSpecType.FullName);

            // Use the publish pipe specification (which creates a filter for PublishContext)
            var filterSpecImpl = new DatadogPublishPipeSpecification();
            var filterSpec = DuckType.CreateReverse(pipeSpecType, filterSpecImpl);

            Log.Debug("MassTransitFilterInjector: Created publish filter specification: {FilterSpecType}", filterSpec?.GetType().FullName);
            return filterSpec;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MassTransitFilterInjector: Failed to create publish filter specification");
            return null;
        }
    }
}
