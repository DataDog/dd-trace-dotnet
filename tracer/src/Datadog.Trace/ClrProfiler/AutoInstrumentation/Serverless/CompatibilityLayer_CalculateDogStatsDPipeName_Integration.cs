// <copyright file="CompatibilityLayer_CalculateDogStatsDPipeName_Integration.cs" company="Datadog">
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
    /// Instrumentation for Datadog.Serverless.CompatibilityLayer.CalculateDogStatsDPipeName method.
    /// This instrumentation overrides the return value with the tracer's pre-generated pipe name,
    /// ensuring the tracer's runtime metrics and the compat layer use the same pipe name.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Datadog.Serverless.Compat",
        TypeName = "Datadog.Serverless.CompatibilityLayer",
        MethodName = "CalculateDogStatsDPipeName",
        ReturnTypeName = ClrNames.String,
        ParameterTypeNames = new string[0],
        MinimumVersion = "1.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(IntegrationId.ServerlessCompat),
        InstrumentationCategory = InstrumentationCategory.Tracing)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class CompatibilityLayer_CalculateDogStatsDPipeName_Integration
    {
        private static string? _cachedDogStatsDPipeName;
        private static readonly object _lock = new object();

        /// <summary>
        /// OnMethodEnd callback - intercepts the return value and overrides it with a lazily-generated unique pipe name
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method (null for static methods)</param>
        /// <param name="returnValue">The pipe name calculated by the compat layer</param>
        /// <param name="exception">Exception instance in case the original code threw an exception</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A unique pipe name for coordination between tracer and compat layer</returns>
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
                // Get the tracer's pipe name (generated in ExporterSettings if on Windows with no explicit config)
                // Use lazy caching to avoid repeated lookups
                if (_cachedDogStatsDPipeName == null)
                {
                    lock (_lock)
                    {
                        if (_cachedDogStatsDPipeName == null)
                        {
                            var tracerInstance = Tracer.Instance;
                            var exporterSettings = tracerInstance?.Settings?.Exporter;
                            _cachedDogStatsDPipeName = exporterSettings?.MetricsPipeName;

                            if (string.IsNullOrEmpty(_cachedDogStatsDPipeName))
                            {
                                // Fallback: if tracer doesn't have a pipe name, generate one here
                                // This shouldn't happen in normal flow, but provides safety
                                _cachedDogStatsDPipeName = ServerlessCompatPipeNameHelper.GenerateUniquePipeName(
                                    returnValue,
                                    "dd_dogstatsd",
                                    "DogStatsD");
                                Log.Warning("ServerlessCompat integration: Tracer DogStatsD pipe name not available, generated fallback: {PipeName}", _cachedDogStatsDPipeName);
                            }
                            else
                            {
                                Log.Information("ServerlessCompat integration: Using tracer's DogStatsD pipe name: {TracerPipeName}", _cachedDogStatsDPipeName);
                            }
                        }
                    }
                }

                Log.Debug(
                    "ServerlessCompat integration: Overriding compat layer DogStatsD pipe name. " +
                    "Compat layer calculated: {CompatPipeName}, Tracer using: {TracerPipeName}",
                    returnValue,
                    _cachedDogStatsDPipeName);

                return new CallTargetReturn<string>(_cachedDogStatsDPipeName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ServerlessCompat integration: Error overriding DogStatsD pipe name");
            }

            // Fallback to compat layer's original value
            return new CallTargetReturn<string>(returnValue);
        }
    }
}
#endif
