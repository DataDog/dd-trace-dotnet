// <copyright file="LoggerConfigurationInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Serilog
{
    /// <summary>
    /// LoggerConfiguration.CreateLogger() calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Serilog",
        TypeName = "Serilog.LoggerConfiguration",
        MethodName = "CreateLogger",
        ReturnTypeName = "Serilog.Core.Logger",
        ParameterTypeNames = new string[0],
        MinimumVersion = "1.0.0",
        MaximumVersion = "2.*.*",
        IntegrationName = nameof(IntegrationId.Serilog))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LoggerConfigurationInstrumentation
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<LoggerConfigurationInstrumentation>();

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget>(TTarget instance)
            where TTarget : ILoggerConfiguration, IDuckType
        {
            if (TracerManager.Instance.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.Serilog))
            {
                TryAddSink(instance);
            }

            return CallTargetState.GetDefault();
        }

        private static void TryAddSink<TTarget>(TTarget instance)
            where TTarget : ILoggerConfiguration, IDuckType
        {
            var sinkAlreadyAdded = false;

            // if we've already added the sink, nothing more to do.
            foreach (var logEventSink in instance.LogEventSinks)
            {
                if (logEventSink is DirectSubmissionSerilogSink
                 || logEventSink?.GetType().FullName == "Serilog.Sinks.Datadog.Logs.DatadogSink")
                {
                    sinkAlreadyAdded = true;
                    break;
                }
            }

            if (!sinkAlreadyAdded)
            {
                var targetType = instance.Type.Assembly.GetType("Serilog.Core.ILogEventSink");
                var sink = new DirectSubmissionSerilogSink(
                    TracerManager.Instance.DirectLogSubmission.Sink,
                    TracerManager.Instance.DirectLogSubmission.Settings.MinimumLevel);

                var proxy = sink.DuckImplement(targetType);
                instance.LogEventSinks.Add(proxy);
                Log.Information("Direct log submission via Serilog enabled");
            }
        }
    }
}
