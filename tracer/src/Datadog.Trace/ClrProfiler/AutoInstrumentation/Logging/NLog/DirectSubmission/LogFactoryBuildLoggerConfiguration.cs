// <copyright file="LogFactoryBuildLoggerConfiguration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission;

/// <summary>
/// LogFactory.GetConfigurationForLogger calltarget instrumentation
/// </summary>
[InstrumentMethod(
    AssemblyName = "NLog",
    TypeName = "NLog.LogFactory",
    MethodName = "BuildLoggerConfiguration",
    ReturnTypeName = "NLog.Internal.TargetWithFilterChain[]",
    ParameterTypeNames = new[] { ClrNames.String, "System.Collections.Generic.List`1[NLog.Config.LoggingRule]" },
    MinimumVersion = "5.0.0",
    MaximumVersion = "5.*.*",
    IntegrationName = NLogConstants.IntegrationName)]
[Browsable(false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public class LogFactoryBuildLoggerConfiguration
{
    private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LogFactoryBuildLoggerConfiguration));

    internal static CallTargetState OnMethodBegin<TTarget, TLoggingRuleList>(TTarget instance, string name, ref TLoggingRuleList loggingRules)
    {
        var tracerManager = TracerManager.Instance;

        if (!tracerManager.Settings.LogsInjectionEnabledInternal &&
            !tracerManager.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.NLog))
        {
            return CallTargetState.GetDefault();
        }

        // when logging is being stopped/shutdown this instrumentation gets called again
        // The logging rules passed are "null"
        // but we have an "_isDisposing" field that is set to true in the LogFactory that we can check
        if (instance.TryDuckCast<ILogFactoryProxy>(out var logFactoryProxy) && logFactoryProxy.IsDisposing)
        {
            // when logging is stopped (e.g., shutdown) the configuration will be set to null
            // and this instrumentation will get hit again, so to avoid creating a new configuration
            // and adding the Datadog Direct Submission target we just need to exit here.
            return CallTargetState.GetDefault();
        }

        // we don't want to do logs injection with our custom configuration that we create as there won't be any targets
        var alreadyAddedOurTarget = false;
        if (tracerManager.Settings.LogsInjectionEnabledInternal)
        {
            LogsInjectionHelper<TTarget>.ConfigureLogsInjectionForLoggingRules(loggingRules, out alreadyAddedOurTarget);
        }

        if (!tracerManager.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.NLog) || alreadyAddedOurTarget)
        {
            return CallTargetState.GetDefault();
        }

        // first check to see if the configuration is null
        if (logFactoryProxy is not null && logFactoryProxy.ConfigurationField is null)
        {
            // with 5.3 a change was made to call _config?.BuildLoggingConfiguration instead of static methods
            // if a user doesn't have a configuration for NLog setup the BuildLoggingConfiguration won't be called
            object? loggingConfigurationInstance = null;
            try
            {
                var loggingType = Type.GetType("NLog.Config.LoggingConfiguration, NLog", throwOnError: false);
                if (loggingType is not null)
                {
                    loggingConfigurationInstance = Activator.CreateInstance(loggingType);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create new instance of NLog's LoggingConfiguration");
            }

            if (loggingConfigurationInstance is not null)
            {
                // this will do _a lot_ of reconfiguration within NLog
                logFactoryProxy.Configuration = loggingConfigurationInstance;
            }
        }

        // if logging rules is null we need to create an instance of the list
        if (loggingRules is null)
        {
            try
            {
                var newLoggingRules = Activator.CreateInstance(typeof(TLoggingRuleList));

                if (newLoggingRules is null)
                {
                    Log.Warning("Failed to create new instance of List<LoggingRule> - instance was null");
                    return CallTargetState.GetDefault();
                }

                loggingRules = (TLoggingRuleList)newLoggingRules;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to create new instance of List<LoggingRule>");
                return CallTargetState.GetDefault();
            }
        }

        var wasAdded = NLogCommon<TTarget>.AddDatadogTargetToLoggingRulesList(loggingRules);
        if (wasAdded)
        {
            // Not really generating a span, but the point is it's enabled and added
            tracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.NLog);
        }

        return CallTargetState.GetDefault();
    }
}
