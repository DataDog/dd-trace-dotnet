// <copyright file="LogFactoryGetConfigurationForLoggerInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43;
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
        private static NLogVersion? _version;

        // TODO attempt to store the fact that we've configured a given configuration to not double configure
        internal static ConditionalWeakTable<object, object> ConfigurationTable { get; } = new();

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
            var tracerManager = TracerManager.Instance;

            if (!tracerManager.Settings.LogsInjectionEnabledInternal &&
                !tracerManager.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.NLog))
            {
                return CallTargetState.GetDefault();
            }

            if (_version is null)
            {
                _version = NLogVersionHelper<TTarget>.Version;
            }

            _ = instance.TryDuckCast<ILogFactoryProxy>(out var logFactoryProxy);
            _ = instance.TryDuckCast<ILogFactoryPre43Proxy>(out var logFactoryPre43Proxy);

            if (logFactoryPre43Proxy is null)
            {
                Log.Warning("Failed to DuckCast the log factory for NLog - Agentless logging and logs injection for NLog won't be functional.");
                return CallTargetState.GetDefault();
            }

            // when logging is being stopped/shutdown this instrumentation gets called again
            // for later versions of NLog, the configuration passed is "null"
            // but we have an "_isDisposing" field that is set to true in the LogFactory that we can check
            // for older versions of NLog, the configuration passed is the _previous_ configuration used
            // and there is no "_isDisposing" field that we can check, so using the ConfigurationTable to keep track

            if (configuration is not null && ConfigurationTable.TryGetValue(configuration, out _))
            {
                Log.Debug("We've created a configuration already for NLog - skipping.");
                return CallTargetState.GetDefault();
            }

            if (logFactoryProxy is not null && logFactoryProxy.IsDisposing)
            {
                // when logging is stopped (e.g., shutdown) the configuration will be set to null
                // and this instrumentation will get hit again, so to avoid creating a new configuration
                // and adding the Datadog Direct Submission target we just need to exit here.
                Log.Debug("NLog LogFactory was being disposed so skipping the logging configuration instrumentations.");
                return CallTargetState.GetDefault();
            }

            // we don't want to do logs injection with our custom configuration that we create as there won't be any targets
            if (tracerManager.Settings.LogsInjectionEnabledInternal && configuration is not null)
            {
                LogsInjectionHelper<TTarget>.ConfigureLogsInjection(configuration);
            }

            // if there isn't a configuration AND we have DirectLogSubmission enabled, create a configuration
            var setConfigurationRequired = false; // indicate whether we need to set LogFactory.Configuration
            if (tracerManager.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.NLog)
             && configuration is null)
            {
                Log.Debug("NLog configuration was null - creating one to allow Direct Log Submission");
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
                Log.Debug("Created custom NLog configuration");
            }

            if (configuration is not null && logFactoryProxy is null)
            {
                // TODO hack not sure what to do here really
                ConfigurationTable.Add(configuration, new());
            }

            if (tracerManager.DirectLogSubmission.Settings.IsIntegrationEnabled(IntegrationId.NLog)
             && configuration is not null)
            {
                Log.Debug("Setting up direct log submission for NLog");
                // if configuration is not-null, we've already checked that NLog is enabled
                var wasAdded = NLogCommon<TTarget>.AddDatadogTarget(configuration);
                if (wasAdded)
                {
                    Log.Debug("Added NLog direct log submission target");
                    // Not really generating a span, but the point is it's enabled and added
                    tracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId.NLog);
                }

                if (wasAdded && setConfigurationRequired)
                {
                    Log.Debug("Setting NLog LogFactory.Configuration to the Configuration we created for direct log submission.");
                    // the setter here does a lot of initialization of the configuration and the targets
                    logFactoryPre43Proxy.Configuration = configuration;
                }
            }

            return CallTargetState.GetDefault();
        }
    }
}
