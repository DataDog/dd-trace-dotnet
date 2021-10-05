// <copyright file="LogFactoryGetConfigurationForLoggerInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging.DirectSubmission;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission
{
    /// <summary>
    /// LoggerImplInstrumentation calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "NLog",
        TypeName = "NLog.LogFactory",
        MethodName = "GetConfigurationForLogger",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.String, "NLog.Config.LoggingConfiguration" },
        MinimumVersion = "2.1.0",
        MaximumVersion = "4.*.*",
        IntegrationName = NLogConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LogFactoryGetConfigurationForLoggerInstrumentation
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TLoggingConfiguration">Type of the LoggingConfiguration object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="name">The name of the logger</param>
        /// <param name="configuration">The logging configuration</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TLoggingConfiguration>(TTarget instance, string name, TLoggingConfiguration configuration)
        {
            if (TracerManager.Instance.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.NLog)
             && configuration is not null)
            {
                // if configuration is not-null, we've already checked that NLog is enabled
                NLogCommon<TTarget>.AddDatadogTarget(configuration);
            }

            return CallTargetState.GetDefault();
        }
    }
}
