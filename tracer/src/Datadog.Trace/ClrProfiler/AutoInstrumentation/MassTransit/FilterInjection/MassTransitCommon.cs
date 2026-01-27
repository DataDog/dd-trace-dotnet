// <copyright file="MassTransitCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using System.Reflection;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MassTransit.FilterInjection;

/// <summary>
/// Common helper class for creating MassTransit filters via reflection.
/// Centralizes assembly lookup and reverse duck type creation for MassTransit 7.x filter injection.
/// </summary>
internal static class MassTransitCommon
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MassTransitCommon));

    // Cached assemblies to avoid repeated lookups
    private static Assembly? _massTransitAssembly;
    private static Assembly? _greenPipesAssembly;
    private static Assembly? _diAssembly;

    // Cached types
    private static Type? _consumeContextType;
    private static Type? _pipeSpecificationType;
    private static Type? _filterType;
    private static Type? _validationResultType;
    private static Type? _configureReceiveEndpointType;
    private static Type? _serviceDescriptorType;
    private static Type? _serviceLifetimeType;

    // Cached empty validation results array
    private static IEnumerable? _emptyValidationResults;

    /// <summary>
    /// Creates the Datadog IConfigureReceiveEndpoint proxy for MassTransit filter injection.
    /// This is the main entry point for filter creation, similar to HangfireCommon.CreateDatadogFilter().
    /// </summary>
    /// <param name="configureReceiveEndpoint">The created IConfigureReceiveEndpoint proxy, or null on failure</param>
    internal static void CreateDatadogConfigureReceiveEndpoint(out object? configureReceiveEndpoint)
    {
        configureReceiveEndpoint = null;

        var massTransitAssembly = GetMassTransitAssembly();
        var greenPipesAssembly = GetGreenPipesAssembly();

        if (massTransitAssembly == null || greenPipesAssembly == null)
        {
            Log.Debug("MassTransitCommon: Could not find required MassTransit/GreenPipes assemblies. MassTransit integration is not enabled.");
            return;
        }

        var configureType = GetConfigureReceiveEndpointType();
        if (configureType == null)
        {
            Log.Debug("MassTransitCommon: Could not find IConfigureReceiveEndpoint type. MassTransit integration is not enabled.");
            return;
        }

        configureReceiveEndpoint = DuckType.CreateReverse(configureType, new DatadogConfigureReceiveEndpoint());
    }

    /// <summary>
    /// Finds and caches the MassTransit assembly.
    /// </summary>
    internal static Assembly? GetMassTransitAssembly()
    {
        if (_massTransitAssembly != null)
        {
            return _massTransitAssembly;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name == "MassTransit")
            {
                _massTransitAssembly = assembly;
                return _massTransitAssembly;
            }
        }

        Log.Debug("MassTransitCommon: Could not find MassTransit assembly");
        return null;
    }

    /// <summary>
    /// Finds and caches the GreenPipes assembly.
    /// </summary>
    internal static Assembly? GetGreenPipesAssembly()
    {
        if (_greenPipesAssembly != null)
        {
            return _greenPipesAssembly;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name == "GreenPipes")
            {
                _greenPipesAssembly = assembly;
                return _greenPipesAssembly;
            }
        }

        Log.Debug("MassTransitCommon: Could not find GreenPipes assembly");
        return null;
    }

    /// <summary>
    /// Finds and caches the DI Abstractions assembly.
    /// </summary>
    internal static Assembly? GetDiAbstractionsAssembly()
    {
        if (_diAssembly != null)
        {
            return _diAssembly;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.GetName().Name == "Microsoft.Extensions.DependencyInjection.Abstractions")
            {
                _diAssembly = assembly;
                return _diAssembly;
            }
        }

        Log.Debug("MassTransitCommon: Could not find DI Abstractions assembly");
        return null;
    }

    /// <summary>
    /// Gets the MassTransit.ConsumeContext type.
    /// </summary>
    internal static Type? GetConsumeContextType()
    {
        if (_consumeContextType != null)
        {
            return _consumeContextType;
        }

        var assembly = GetMassTransitAssembly();
        _consumeContextType = assembly?.GetType("MassTransit.ConsumeContext");

        if (_consumeContextType == null)
        {
            Log.Debug("MassTransitCommon: Could not find ConsumeContext type");
        }

        return _consumeContextType;
    }

    /// <summary>
    /// Gets the GreenPipes.IPipeSpecification{ConsumeContext} type.
    /// </summary>
    internal static Type? GetPipeSpecificationType()
    {
        if (_pipeSpecificationType != null)
        {
            return _pipeSpecificationType;
        }

        var greenPipesAssembly = GetGreenPipesAssembly();
        var consumeContextType = GetConsumeContextType();

        if (greenPipesAssembly == null || consumeContextType == null)
        {
            return null;
        }

        var pipeSpecOpenType = greenPipesAssembly.GetType("GreenPipes.IPipeSpecification`1");
        if (pipeSpecOpenType == null)
        {
            Log.Debug("MassTransitCommon: Could not find IPipeSpecification<> type");
            return null;
        }

        _pipeSpecificationType = pipeSpecOpenType.MakeGenericType(consumeContextType);
        return _pipeSpecificationType;
    }

    /// <summary>
    /// Gets the GreenPipes.IFilter{ConsumeContext} type.
    /// </summary>
    internal static Type? GetFilterType()
    {
        if (_filterType != null)
        {
            return _filterType;
        }

        var greenPipesAssembly = GetGreenPipesAssembly();
        var consumeContextType = GetConsumeContextType();

        if (greenPipesAssembly == null || consumeContextType == null)
        {
            return null;
        }

        var filterOpenType = greenPipesAssembly.GetType("GreenPipes.IFilter`1");
        if (filterOpenType == null)
        {
            Log.Debug("MassTransitCommon: Could not find IFilter<> type");
            return null;
        }

        _filterType = filterOpenType.MakeGenericType(consumeContextType);
        return _filterType;
    }

    /// <summary>
    /// Gets the MassTransit.IConfigureReceiveEndpoint type.
    /// </summary>
    internal static Type? GetConfigureReceiveEndpointType()
    {
        if (_configureReceiveEndpointType != null)
        {
            return _configureReceiveEndpointType;
        }

        var assembly = GetMassTransitAssembly();
        _configureReceiveEndpointType = assembly?.GetType("MassTransit.IConfigureReceiveEndpoint");

        if (_configureReceiveEndpointType == null)
        {
            Log.Debug("MassTransitCommon: Could not find IConfigureReceiveEndpoint type");
        }

        return _configureReceiveEndpointType;
    }

    /// <summary>
    /// Gets the ServiceDescriptor type from DI abstractions.
    /// </summary>
    internal static Type? GetServiceDescriptorType()
    {
        if (_serviceDescriptorType != null)
        {
            return _serviceDescriptorType;
        }

        var assembly = GetDiAbstractionsAssembly();
        _serviceDescriptorType = assembly?.GetType("Microsoft.Extensions.DependencyInjection.ServiceDescriptor");

        if (_serviceDescriptorType == null)
        {
            Log.Debug("MassTransitCommon: Could not find ServiceDescriptor type");
        }

        return _serviceDescriptorType;
    }

    /// <summary>
    /// Gets the ServiceLifetime enum type from DI abstractions.
    /// </summary>
    internal static Type? GetServiceLifetimeType()
    {
        if (_serviceLifetimeType != null)
        {
            return _serviceLifetimeType;
        }

        var assembly = GetDiAbstractionsAssembly();
        _serviceLifetimeType = assembly?.GetType("Microsoft.Extensions.DependencyInjection.ServiceLifetime");

        if (_serviceLifetimeType == null)
        {
            Log.Debug("MassTransitCommon: Could not find ServiceLifetime type");
        }

        return _serviceLifetimeType;
    }

    /// <summary>
    /// Creates a reverse duck type proxy that implements IFilter{ConsumeContext}.
    /// </summary>
    /// <param name="filterImpl">The DatadogConsumeFilter implementation</param>
    /// <returns>A proxy object implementing IFilter{ConsumeContext}, or null on failure</returns>
    internal static object? CreateFilterProxy(DatadogConsumeFilter filterImpl)
    {
        try
        {
            var filterType = GetFilterType();
            if (filterType == null)
            {
                return null;
            }

            var filter = DuckType.CreateReverse(filterType, filterImpl);
            Log.Debug("MassTransitCommon: Created filter proxy: {FilterProxyType}", filter?.GetType().FullName);
            return filter;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MassTransitCommon: Failed to create filter proxy");
            return null;
        }
    }

    /// <summary>
    /// Creates a reverse duck type proxy that implements IPipeSpecification{ConsumeContext}.
    /// </summary>
    /// <param name="specImpl">The DatadogConsumePipeSpecification implementation</param>
    /// <returns>A proxy object implementing IPipeSpecification{ConsumeContext}, or null on failure</returns>
    internal static object? CreatePipeSpecificationProxy(DatadogConsumePipeSpecification specImpl)
    {
        try
        {
            var pipeSpecType = GetPipeSpecificationType();
            if (pipeSpecType == null)
            {
                return null;
            }

            var proxy = DuckType.CreateReverse(pipeSpecType, specImpl);
            Log.Debug("MassTransitCommon: Created pipe specification proxy");
            return proxy;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MassTransitCommon: Failed to create pipe specification proxy");
            return null;
        }
    }

    /// <summary>
    /// Creates a reverse duck type proxy that implements IConfigureReceiveEndpoint.
    /// </summary>
    /// <param name="impl">The DatadogConfigureReceiveEndpoint implementation</param>
    /// <returns>A proxy object implementing IConfigureReceiveEndpoint, or null on failure</returns>
    internal static object? CreateConfigureReceiveEndpointProxy(DatadogConfigureReceiveEndpoint impl)
    {
        try
        {
            var configureType = GetConfigureReceiveEndpointType();
            if (configureType == null)
            {
                return null;
            }

            var proxy = DuckType.CreateReverse(configureType, impl);
            Log.Debug("MassTransitCommon: Created IConfigureReceiveEndpoint proxy");
            return proxy;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MassTransitCommon: Failed to create IConfigureReceiveEndpoint proxy");
            return null;
        }
    }

    /// <summary>
    /// Gets an empty array of GreenPipes.ValidationResult type for use in IPipeSpecification.Validate().
    /// </summary>
    /// <returns>An empty IEnumerable of ValidationResult</returns>
    internal static IEnumerable GetEmptyValidationResults()
    {
        if (_emptyValidationResults != null)
        {
            return _emptyValidationResults;
        }

        try
        {
            var greenPipesAssembly = GetGreenPipesAssembly();
            if (greenPipesAssembly != null)
            {
                _validationResultType ??= greenPipesAssembly.GetType("GreenPipes.ValidationResult");
                if (_validationResultType != null)
                {
                    _emptyValidationResults = Array.CreateInstance(_validationResultType, 0);
                    Log.Debug("MassTransitCommon: Created empty ValidationResult array");
                    return _emptyValidationResults;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "MassTransitCommon: Failed to create ValidationResult array");
        }

        // Fallback to empty object array
        _emptyValidationResults = Array.Empty<object>();
        return _emptyValidationResults;
    }

    /// <summary>
    /// Finds the AddPipeSpecification method on a configurator type.
    /// </summary>
    /// <param name="configuratorType">The type of the configurator</param>
    /// <returns>The AddPipeSpecification method, or null if not found</returns>
    internal static MethodInfo? FindAddPipeSpecificationMethod(Type configuratorType)
    {
        var pipeSpecType = GetPipeSpecificationType();
        if (pipeSpecType == null)
        {
            return null;
        }

        // Try on the type directly
        var method = configuratorType.GetMethod("AddPipeSpecification", new[] { pipeSpecType });
        if (method != null)
        {
            return method;
        }

        // Try on interfaces
        foreach (var iface in configuratorType.GetInterfaces())
        {
            method = iface.GetMethod("AddPipeSpecification", new[] { pipeSpecType });
            if (method != null)
            {
                return method;
            }
        }

        Log.Debug("MassTransitCommon: Could not find AddPipeSpecification method on {Type}", configuratorType.FullName);
        return null;
    }
}
