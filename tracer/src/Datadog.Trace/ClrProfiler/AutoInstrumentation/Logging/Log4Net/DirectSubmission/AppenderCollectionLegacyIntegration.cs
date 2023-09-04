// <copyright file="AppenderCollectionLegacyIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Logging.DirectSubmission;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.Log4Net.DirectSubmission
{
    /// <summary>
    /// AppenderCollection.ToArray() calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "log4net",
        TypeName = "log4net.Appender.AppenderCollection",
        MethodName = "ToArray",
        ReturnTypeName = "log4net.Appender.IAppender[]",
        MinimumVersion = "1.0.0",
        MaximumVersion = "1.*.*",
        IntegrationName = nameof(IntegrationId.Log4Net))]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class AppenderCollectionLegacyIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<AppenderCollectionLegacyIntegration>();
        private static bool _logWritten = false;

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">The returned ILoggerWrapper </param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A default CallTargetReturn to satisfy the CallTarget contract</returns>
        public static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, CallTargetState state)
        {
            if (!TracerManager.Instance.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.Log4Net))
            {
                return new CallTargetReturn<TResponse>(response);
            }

            if (Log4NetCommon<TResponse>.TryAddAppenderToResponse(response, DirectSubmissionLog4NetLegacyAppender.Instance, out var updatedResponse))
            {
                if (!_logWritten)
                {
                    _logWritten = true;
                    TracerManager.Instance.Telemetry.IntegrationGeneratedSpan(IntegrationId.Log4Net);
                    Log.Information("Direct log submission via Log4Net Legacy enabled");
                }
            }

            return new CallTargetReturn<TResponse>(updatedResponse);
        }
    }
}
