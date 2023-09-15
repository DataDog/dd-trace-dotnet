// <copyright file="LogFactoryGetConfigurationForLoggerInstrumentation.cs" company="Datadog">
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

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission
{
    /// <summary>
    /// LogFactory.GetConfigurationForLogger calltarget instrumentation
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
    [InstrumentMethod(
        AssemblyName = "NLog",
        TypeName = "NLog.LogFactory",
        MethodName = "BuildLoggerConfiguration",
        ReturnTypeName = ClrNames.Void,
        ParameterTypeNames = new[] { ClrNames.String, "NLog.Config.LoggingConfiguration" },
        MinimumVersion = "5.0.0",
        MaximumVersion = "5.*.*",
        IntegrationName = NLogConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class LogFactoryGetConfigurationForLoggerInstrumentation
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LogFactoryGetConfigurationForLoggerInstrumentation));

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TLoggingConfiguration">Type of the LoggingConfiguration object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="name">The name of the logger</param>
        /// <param name="configuration">The logging configuration</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TLoggingConfiguration>(TTarget instance, string name, ref TLoggingConfiguration configuration)
        {
            if (!instance.TryDuckCast<ILogFactoryProxy>(out var logFactoryProxy))
            {
                Log.Warning("Failed to DuckCast the NLog LogFactory");
                return CallTargetState.GetDefault();
            }

            if (logFactoryProxy.IsDisposing)
            {
                // when logging is stopped (e.g., shutdown) the configuration will be set to null
                // and this instrumentation will get hit again, so to avoid creating a new configuration
                // and adding the Datadog Direct Submission target we just need to exit here.
                return CallTargetState.GetDefault();
            }

            var tracerManager = TracerManager.Instance;

            // extract the assembly here and the version
            var assembly = typeof(TTarget).Assembly;

            // we don't want to do logs injection with our custom configuration that we create as there won't be any targets
            if (tracerManager.Settings.LogsInjectionEnabledInternal && configuration is not null)
            {
                LogsInjectionHelper.ConfigureLogsInjection(configuration, assembly);
            }

            // if there isn't a configuration AND we have DirectLogSubmission enabled, create a configuration
            bool setConfigurationRequired = false; // indicate whether we need to set LogFactory.Configuration
            if (tracerManager.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.NLog)
             && configuration is null)
            {
                var loggingConfigurationInstance = Activator.CreateInstance(typeof(TLoggingConfiguration));
                if (loggingConfigurationInstance is null)
                {
                    Log.Warning("Failed to create instance of NLog.Config.LoggingConfiguration Type");
                    return CallTargetState.GetDefault();
                }

                setConfigurationRequired = true;

                // update the "ref" configuration passed to us with the new one
                // Note: we need to call LogFactory.Configuration's setter as well to allow NLog
                //       to hook everything up correctly for this newly created configuration
                //       but first we need to create and add the direct logs submission target
                configuration = (TLoggingConfiguration)loggingConfigurationInstance;
                Log.Information("Created custom NLog configuration");
            }

            if (tracerManager.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.NLog)
             && configuration is not null)
            {
                // if configuration is not-null, we've already checked that NLog is enabled
                var wasAdded = NLogCommon<TTarget>.AddDatadogTarget(configuration);
                if (wasAdded)
                {
                    // Not really generating a span, but the point is it's enabled and added
                    tracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.NLog);
                }

                if (wasAdded && setConfigurationRequired)
                {
                    // this is necessary when we create our own configuration as the setter here
                    // does a lot of additional calls to initialize the new configuration.
                    logFactoryProxy.Configuration = configuration;
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
