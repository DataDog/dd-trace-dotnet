// <copyright file="DatadogConsumePipeSpecification.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

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

    /// <summary>
    /// Applies the filter specification to the pipeline builder.
    /// This method is called by MassTransit when building the consume pipeline.
    /// </summary>
    /// <param name="builder">The pipe builder (IPipeBuilder{ConsumeContext})</param>
    [DuckReverseMethod(ParameterTypeNames = ["GreenPipes.IPipeBuilder`1[MassTransit.ConsumeContext], GreenPipes"])]
    public void Apply(object builder)
    {
        Log.Debug("DatadogConsumePipeSpecification.Apply() called - adding DatadogConsumeFilter to pipeline");

        var builderType = builder.GetType();
        var addFilterMethod = builderType.GetMethod("AddFilter", BindingFlags.Public | BindingFlags.Instance);

        if (addFilterMethod == null)
        {
            Log.Warning("DatadogConsumePipeSpecification: Could not find AddFilter method on builder");
            return;
        }

        var filter = MassTransitCommon.CreateFilterProxy(new DatadogConsumeFilter());
        if (filter == null)
        {
            Log.Warning("DatadogConsumePipeSpecification: Could not create filter proxy");
            return;
        }

        addFilterMethod.Invoke(builder, new object[] { filter });
        Log.Debug("DatadogConsumePipeSpecification: Successfully added DatadogConsumeFilter to pipeline");
    }

    /// <summary>
    /// Validates the specification. Returns empty since the filter is always valid.
    /// Returns an empty array of GreenPipes.ValidationResult type.
    /// </summary>
    /// <returns>Empty validation results</returns>
    [DuckReverseMethod]
    public IEnumerable Validate() => MassTransitCommon.GetEmptyValidationResults();
}
