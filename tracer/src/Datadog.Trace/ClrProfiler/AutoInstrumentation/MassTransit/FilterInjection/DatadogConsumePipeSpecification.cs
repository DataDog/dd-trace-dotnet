// <copyright file="DatadogConsumePipeSpecification.cs" company="Datadog">
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
/// A pipe specification that adds the DatadogConsumeFilter to the MassTransit consume pipeline.
/// This implements GreenPipes.IPipeSpecification{ConsumeContext} for MassTransit 7.x.
///
/// Note: This class is designed to be added to the _specifications list via reflection.
/// It implements the interface duck-typed since we can't reference GreenPipes directly.
/// </summary>
internal sealed class DatadogConsumePipeSpecification
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatadogConsumePipeSpecification));
    private static IEnumerable? _emptyValidationResults;

    /// <summary>
    /// Applies the filter specification to the pipeline builder.
    /// This method is called by MassTransit when building the consume pipeline.
    /// </summary>
    /// <param name="builder">The pipe builder (IPipeBuilder{ConsumeContext})</param>
    [DuckReverseMethod(ParameterTypeNames = ["GreenPipes.IPipeBuilder`1[MassTransit.ConsumeContext], GreenPipes"])]
    public void Apply(object builder)
    {
        Log.Debug("DatadogConsumePipeSpecification.Apply() called - adding DatadogConsumeFilter to pipeline");

        try
        {
            // Call builder.AddFilter(filter) using reflection
            var builderType = builder.GetType();
            var addFilterMethod = builderType.GetMethod("AddFilter", BindingFlags.Public | BindingFlags.Instance);

            if (addFilterMethod == null)
            {
                Log.Warning("DatadogConsumePipeSpecification: Could not find AddFilter method on builder");
                return;
            }

            // Create the DatadogConsumeFilter instance and wrap it in a reverse duck type
            // to implement IFilter<ConsumeContext>
            var filterImpl = new DatadogConsumeFilter();
            var filter = CreateFilterProxy(filterImpl);

            if (filter == null)
            {
                Log.Warning("DatadogConsumePipeSpecification: Could not create filter proxy");
                return;
            }

            addFilterMethod.Invoke(builder, new object[] { filter });

            Log.Debug("DatadogConsumePipeSpecification: Successfully added DatadogConsumeFilter to pipeline");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DatadogConsumePipeSpecification: Failed to add filter to pipeline");
        }
    }

    private static object? CreateFilterProxy(DatadogConsumeFilter filterImpl)
    {
        try
        {
            // Find the GreenPipes and MassTransit assemblies
            Assembly? greenPipesAssembly = null;
            Assembly? massTransitAssembly = null;

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var assemblyName = assembly.GetName().Name;
                if (assemblyName == "GreenPipes")
                {
                    greenPipesAssembly = assembly;
                }
                else if (assemblyName == "MassTransit")
                {
                    massTransitAssembly = assembly;
                }

                if (greenPipesAssembly != null && massTransitAssembly != null)
                {
                    break;
                }
            }

            if (greenPipesAssembly == null)
            {
                Log.Debug("DatadogConsumePipeSpecification: Could not find GreenPipes assembly");
                return null;
            }

            if (massTransitAssembly == null)
            {
                Log.Debug("DatadogConsumePipeSpecification: Could not find MassTransit assembly");
                return null;
            }

            var consumeContextType = massTransitAssembly.GetType("MassTransit.ConsumeContext");
            if (consumeContextType == null)
            {
                Log.Debug("DatadogConsumePipeSpecification: Could not find ConsumeContext type");
                return null;
            }

            // Get IFilter<ConsumeContext> type
            var filterOpenType = greenPipesAssembly.GetType("GreenPipes.IFilter`1");
            if (filterOpenType == null)
            {
                Log.Debug("DatadogConsumePipeSpecification: Could not find IFilter<> type");
                return null;
            }

            var filterType = filterOpenType.MakeGenericType(consumeContextType);
            Log.Debug("DatadogConsumePipeSpecification: Creating reverse duck type for {FilterType}", filterType.FullName);

            // Create a reverse duck type that implements IFilter<ConsumeContext>
            var filter = DuckType.CreateReverse(filterType, filterImpl);

            Log.Debug("DatadogConsumePipeSpecification: Created filter proxy: {FilterProxyType}", filter?.GetType().FullName);
            return filter;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogConsumePipeSpecification: Failed to create filter proxy");
            return null;
        }
    }

    /// <summary>
    /// Validates the specification. Returns empty since the filter is always valid.
    /// Returns an empty array of GreenPipes.ValidationResult type.
    /// </summary>
    /// <returns>Empty validation results</returns>
    [DuckReverseMethod]
    public IEnumerable Validate()
    {
        // Return a cached empty array of the correct ValidationResult type
        if (_emptyValidationResults != null)
        {
            return _emptyValidationResults;
        }

        try
        {
            // Find the GreenPipes assembly and ValidationResult type
            Assembly? greenPipesAssembly = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name == "GreenPipes")
                {
                    greenPipesAssembly = assembly;
                    break;
                }
            }

            if (greenPipesAssembly != null)
            {
                var validationResultType = greenPipesAssembly.GetType("GreenPipes.ValidationResult");
                if (validationResultType != null)
                {
                    // Create an empty array of ValidationResult type
                    _emptyValidationResults = Array.CreateInstance(validationResultType, 0);
                    Log.Debug("DatadogConsumePipeSpecification: Created empty ValidationResult array");
                    return _emptyValidationResults;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogConsumePipeSpecification: Failed to create ValidationResult array");
        }

        // Fallback to empty object array (shouldn't happen if GreenPipes is loaded)
        _emptyValidationResults = Array.Empty<object>();
        return _emptyValidationResults;
    }
}
