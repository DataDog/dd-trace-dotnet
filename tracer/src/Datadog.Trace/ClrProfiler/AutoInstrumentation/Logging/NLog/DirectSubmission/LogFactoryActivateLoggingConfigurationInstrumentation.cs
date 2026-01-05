// <copyright file="LogFactoryActivateLoggingConfigurationInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission;

/// <summary>
/// System.Void NLog.LogFactory::ActivateLoggingConfiguration(NLog.Config.LoggingConfiguration) calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "NLog",
    TypeName = "NLog.LogFactory",
    MethodName = "ActivateLoggingConfiguration",
    ReturnTypeName = ClrNames.Void,
    ParameterTypeNames = ["NLog.Config.LoggingConfiguration"],
    MinimumVersion = "6.0.0",
    MaximumVersion = "6.*.*",
    IntegrationName = NLogConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class LogFactoryActivateLoggingConfigurationInstrumentation
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LogFactoryActivateLoggingConfigurationInstrumentation));

    internal static CallTargetState OnMethodBegin<TTarget, TConfig>(TTarget instance, ref TConfig? configuration)
    {
        var tracerManager = TracerManager.Instance;

        // configuration should never be null for NLog v6 and above, so play it safe and bail out if it is
        if (configuration is null)
        {
            return CallTargetState.GetDefault();
        }

        if (tracerManager.PerTraceSettings.Settings.LogsInjectionEnabled)
        {
            LogsInjectionHelper<TTarget>.ConfigureLogsInjectionForLoggerConfiguration(configuration);
        }

        if (tracerManager.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.NLog))
        {
            // if configuration is not-null, we've already checked that NLog is enabled
            if (NLogCommon<TTarget>.AddDatadogTargetToLoggingConfiguration(configuration))
            {
                // Not really generating a span, but the point is it's enabled and added
                tracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.NLog);
            }
        }

        return CallTargetState.GetDefault();
    }
}
