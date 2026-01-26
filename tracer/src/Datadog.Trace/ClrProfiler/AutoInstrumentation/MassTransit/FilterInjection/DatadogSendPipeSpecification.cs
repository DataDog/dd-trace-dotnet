// <copyright file="DatadogSendPipeSpecification.cs" company="Datadog">
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
/// A pipe specification that adds the DatadogSendFilter to the MassTransit send pipeline.
/// This implements GreenPipes.IPipeSpecification{SendContext} for MassTransit 7.x.
/// </summary>
internal sealed class DatadogSendPipeSpecification
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DatadogSendPipeSpecification));
    private static IEnumerable? _emptyValidationResults;

    /// <summary>
    /// Applies the filter specification to the pipeline builder.
    /// This method is called by MassTransit when building the send pipeline.
    /// </summary>
    /// <param name="builder">The pipe builder (IPipeBuilder{SendContext})</param>
    [DuckReverseMethod(ParameterTypeNames = ["GreenPipes.IPipeBuilder`1[MassTransit.SendContext], GreenPipes"])]
    public void Apply(object builder)
    {
        Log.Debug("DatadogSendPipeSpecification.Apply() called - adding DatadogSendFilter to pipeline");

        try
        {
            var builderType = builder.GetType();
            var addFilterMethod = builderType.GetMethod("AddFilter", BindingFlags.Public | BindingFlags.Instance);

            if (addFilterMethod == null)
            {
                Log.Warning("DatadogSendPipeSpecification: Could not find AddFilter method on builder");
                return;
            }

            // Create the DatadogSendFilter instance and wrap it in a reverse duck type
            var filterImpl = new DatadogSendFilter();
            var filter = CreateFilterProxy(filterImpl);

            if (filter == null)
            {
                Log.Warning("DatadogSendPipeSpecification: Could not create filter proxy");
                return;
            }

            addFilterMethod.Invoke(builder, new object[] { filter });

            Log.Debug("DatadogSendPipeSpecification: Successfully added DatadogSendFilter to pipeline");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "DatadogSendPipeSpecification: Failed to add filter to pipeline");
        }
    }

    private static object? CreateFilterProxy(DatadogSendFilter filterImpl)
    {
        try
        {
            var greenPipesAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(asm => asm.GetName().Name == "GreenPipes");

            if (greenPipesAssembly == null)
            {
                Log.Debug("DatadogSendPipeSpecification: Could not find GreenPipes assembly");
                return null;
            }

            var massTransitAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(asm => asm.GetName().Name == "MassTransit");

            if (massTransitAssembly == null)
            {
                Log.Debug("DatadogSendPipeSpecification: Could not find MassTransit assembly");
                return null;
            }

            var sendContextType = massTransitAssembly.GetType("MassTransit.SendContext");
            if (sendContextType == null)
            {
                Log.Debug("DatadogSendPipeSpecification: Could not find SendContext type");
                return null;
            }

            var filterOpenType = greenPipesAssembly.GetType("GreenPipes.IFilter`1");
            if (filterOpenType == null)
            {
                Log.Debug("DatadogSendPipeSpecification: Could not find IFilter<> type");
                return null;
            }

            var filterType = filterOpenType.MakeGenericType(sendContextType);
            Log.Debug("DatadogSendPipeSpecification: Creating reverse duck type for {FilterType}", filterType.FullName);

            var filter = DuckType.CreateReverse(filterType, filterImpl);

            Log.Debug("DatadogSendPipeSpecification: Created filter proxy: {FilterProxyType}", filter?.GetType().FullName);
            return filter;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogSendPipeSpecification: Failed to create filter proxy");
            return null;
        }
    }

    /// <summary>
    /// Validates the specification. Returns empty since the filter is always valid.
    /// </summary>
    /// <returns>Empty validation results</returns>
    [DuckReverseMethod]
    public IEnumerable Validate()
    {
        if (_emptyValidationResults != null)
        {
            return _emptyValidationResults;
        }

        try
        {
            var greenPipesAssembly = AppDomain.CurrentDomain
                .GetAssemblies()
                .FirstOrDefault(asm => asm.GetName().Name == "GreenPipes");

            if (greenPipesAssembly != null)
            {
                var validationResultType = greenPipesAssembly.GetType("GreenPipes.ValidationResult");
                if (validationResultType != null)
                {
                    _emptyValidationResults = Array.CreateInstance(validationResultType, 0);
                    Log.Debug("DatadogSendPipeSpecification: Created empty ValidationResult array");
                    return _emptyValidationResults;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "DatadogSendPipeSpecification: Failed to create ValidationResult array");
        }

        _emptyValidationResults = Array.Empty<object>();
        return _emptyValidationResults;
    }
}
