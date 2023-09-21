// <copyright file="LoggerFactoryConstructorIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.ILogger.DirectSubmission
{
    /// <summary>
    /// LoggerFactory() calltarget instrumentation for direct log submission
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "Microsoft.Extensions.Logging",
        TypeName = "Microsoft.Extensions.Logging.LoggerFactory",
        MethodName = ".ctor",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Collections.Generic.IEnumerable`1[Microsoft.Extensions.Logging.ILoggerProvider]", "Microsoft.Extensions.Options.IOptionsMonitor`1[Microsoft.Extensions.Logging.LoggerFilterOptions]" },
        MinimumVersion = "2.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = LoggerIntegrationCommon.IntegrationName)]
    [InstrumentMethod(
        AssemblyName = "Microsoft.Extensions.Logging",
        TypeName = "Microsoft.Extensions.Logging.LoggerFactory",
        MethodName = ".ctor",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { "System.Collections.Generic.IEnumerable`1[Microsoft.Extensions.Logging.ILoggerProvider]", "Microsoft.Extensions.Options.IOptionsMonitor`1[Microsoft.Extensions.Logging.LoggerFilterOptions]", "Microsoft.Extensions.Options.IOptions`1[Microsoft.Extensions.Logging.LoggerFactoryOptions]" },
        MinimumVersion = "5.0.0",
        MaximumVersion = "6.*.*",
        IntegrationName = LoggerIntegrationCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LoggerFactoryConstructorIntegration
    {
        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A default CallTargetReturn to satisfy the CallTarget contract</returns>
        public static CallTargetReturn OnMethodEnd<TTarget>(TTarget instance, Exception? exception, CallTargetState state)
        {
            if (!TracerManager.Instance.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.ILogger))
            {
                return CallTargetReturn.GetDefault();
            }

            if (exception is not null)
            {
                // If there's an exception during the constructor, things aren't going to work anyway
                return CallTargetReturn.GetDefault();
            }

            if (LoggerFactoryIntegrationCommon<TTarget>.TryAddDirectSubmissionLoggerProvider(instance))
            {
                TracerManager.Instance.Telemetry.IntegrationGeneratedSpan(IntegrationId.ILogger);
            }

            return CallTargetReturn.GetDefault();
        }
    }
}
