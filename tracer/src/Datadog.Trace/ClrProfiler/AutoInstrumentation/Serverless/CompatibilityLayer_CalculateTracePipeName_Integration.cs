// <copyright file="CompatibilityLayer_CalculateTracePipeName_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Serverless
{
    /// <summary>
    /// Instrumentation for Datadog.Serverless.CompatibilityLayer.CalculateTracePipeName method.
    /// This instrumentation overrides the return value with the tracer's pre-generated pipe name,
    /// ensuring both the tracer and the compat layer use the same pipe name for communication.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Datadog.Serverless.Compat",
        TypeName = "Datadog.Serverless.CompatibilityLayer",
        MethodName = "CalculateTracePipeName",
        ReturnTypeName = ClrNames.String,
        ParameterTypeNames = new string[0],
        MinimumVersion = "1.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(IntegrationId.ServerlessCompat),
        InstrumentationCategory = InstrumentationCategory.Tracing)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class CompatibilityLayer_CalculateTracePipeName_Integration
    {
        /// <summary>
        /// OnMethodEnd callback - intercepts the return value and overrides it with the tracer's pipe name
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method (null for static methods)</param>
        /// <param name="returnValue">The pipe name calculated by the compat layer</param>
        /// <param name="exception">Exception instance in case the original code threw an exception</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>The tracer's pre-generated pipe name, overriding the compat layer's calculation</returns>
        internal static CallTargetReturn<string> OnMethodEnd<TTarget>(
            TTarget instance,
            string returnValue,
            Exception exception,
            in CallTargetState state)
        {
            if (exception != null)
            {
                // If there was an exception, pass it through
                return new CallTargetReturn<string>(returnValue);
            }

            try
            {
                // Get the tracer's pre-generated pipe name from ExporterSettings
                var tracerInstance = Tracer.Instance;
                var exporterSettings = tracerInstance?.Settings?.Exporter;
                var tracerPipeName = exporterSettings?.TracesPipeName;

                if (!string.IsNullOrEmpty(tracerPipeName))
                {
                    Log.Debug(
                        "ServerlessCompat integration: Overriding compat layer trace pipe name. " +
                        "Compat layer calculated: {CompatPipeName}, Tracer using: {TracerPipeName}",
                        returnValue,
                        tracerPipeName);

                    // Override with tracer's value
                    return new CallTargetReturn<string>(tracerPipeName);
                }
                else
                {
                    Log.Warning(
                        "ServerlessCompat integration: Tracer pipe name is null or empty. " +
                        "Using compat layer's calculated value: {CompatPipeName}",
                        returnValue);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ServerlessCompat integration: Error overriding trace pipe name");
            }

            // Fallback to compat layer's original value
            return new CallTargetReturn<string>(returnValue);
        }
    }
}
#endif
