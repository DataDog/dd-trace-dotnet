// <copyright file="LogFactoryGetConfigurationForLoggerInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43;
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

            // we don't want to do logs injection with our custom configuration that we create as there won't be any targets
            if (tracerManager.Settings.LogsInjectionEnabledInternal && configuration is not null)
            {
                ConfigureLogsInjection(configuration);
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

        internal static void ConfigureLogsInjection(object loggingConfiguration)
        {
            if (loggingConfiguration.TryDuckCast<IBasicLoggingConfigurationProxy>(out var loggingConfigurationProxy))
            {
                foreach (var target in loggingConfigurationProxy.ConfiguredNamedTargets)
                {
                    if (target.TryDuckCast<ITargetWithLayoutProxy>(out var targetWithLayout))
                    {
                        var layout = targetWithLayout.Layout;

                        if (layout.TryDuckCast<IJson5LayoutProxy>(out var layoutWithScope))
                        {
                            layoutWithScope.IncludeScopeProperties = true;
                        }
                        else if (layout.TryDuckCast<IJsonLayoutProxy>(out var layoutWithMdc))
                        {
                            layoutWithMdc.IncludeMdc = true;
                            layoutWithMdc.IncludeMdlc = true;
                        }
                        else if (layout.TryDuckCast<IJsonLayout4Proxy>(out var layoutWithAttributes))
                        {
                            // TODO probably use DuckType and GetCreateProxyType instead of just reflection
                            var jsonAttributeType = Type.GetType("NLog.Layouts.JsonAttribute, NLog", throwOnError: false);
                            if (jsonAttributeType is null)
                            {
                                Log.Warning("Failed to NLog JsonAttribute type.");
                                break;
                            }

                            var simpleLayoutType = Type.GetType("NLog.Layouts.SimpleLayout, NLog", throwOnError: false);
                            if (simpleLayoutType is null)
                            {
                                Log.Warning("Failed to NLog SimpleLayoutType type.");
                                break;
                            }

                            bool containsTraceId = false;
                            bool containsSpanId = false;
                            bool containsVersion = false;
                            bool containsService = false;
                            bool containsEnv = false;

                            // hacky implementation to bruteforce attributes
                            foreach (var attribute in layoutWithAttributes.Attributes)
                            {
                                if (!attribute.TryDuckCast<IJsonAttributeProxy>(out var jsonAttributeProxy))
                                {
                                    continue;
                                }

                                // TODO name or actual string layout?
                                switch (jsonAttributeProxy.Name)
                                {
                                    case "dd.env":
                                        containsEnv = true;
                                        continue;
                                    case "dd.service":
                                        containsService = true;
                                        continue;
                                    case "dd.version":
                                        containsVersion = true;
                                        continue;
                                    case "dd.trace_id":
                                        containsTraceId = true;
                                        continue;
                                    case "dd.span_id":
                                        containsSpanId = true;
                                        continue;
                                }
                            }

                            if (!containsEnv)
                            {
                                var newSimpleLayout = Activator.CreateInstance(simpleLayoutType, new object[] { @"${mdc:item=dd.env}" });
                                if (newSimpleLayout is null)
                                {
                                    Log.Warning("Failed to create DD environment attribute for NLog");
                                    break;
                                }

                                var newAttribute = Activator.CreateInstance(jsonAttributeType, new object[] { "dd.env", newSimpleLayout });
                                if (newAttribute is null)
                                {
                                    Log.Warning("Failed to create NLog Attribute for DD Environment");
                                    break;
                                }

                                var attrProxy = newAttribute.DuckCast<IJsonAttributeProxy>();
                                if (attrProxy is null)
                                {
                                    Log.Warning("null");
                                }
                                else
                                {
                                    layoutWithAttributes.Attributes.Add(newAttribute);
                                }
                            }

                            if (!containsTraceId)
                            {
                                var newSimpleLayout = Activator.CreateInstance(simpleLayoutType, new object[] { @"${mdc:item=dd.trace)id}" });
                                if (newSimpleLayout is null)
                                {
                                    Log.Warning("Failed to create DD TraceId attribute for NLog");
                                    break;
                                }

                                var newAttribute = Activator.CreateInstance(jsonAttributeType, new object[] { "dd.trace_id", newSimpleLayout });
                                if (newAttribute is null)
                                {
                                    Log.Warning("Failed to create NLog Attribute for DD TraceId");
                                    break;
                                }

                                layoutWithAttributes.Attributes.Add(newAttribute);
                            }

                            if (!containsSpanId)
                            {
                                var newSimpleLayout = Activator.CreateInstance(simpleLayoutType, new object[] { @"${mdc:item=dd.span_id}" });
                                if (newSimpleLayout is null)
                                {
                                    Log.Warning("Failed to create DD SpanId attribute for NLog");
                                    break;
                                }

                                var newAttribute = Activator.CreateInstance(jsonAttributeType, new object[] { "dd.span_id", newSimpleLayout });
                                if (newAttribute is null)
                                {
                                    Log.Warning("Failed to create NLog Attribute for DD SpanId");
                                    break;
                                }

                                layoutWithAttributes.Attributes.Add(newAttribute);
                            }

                            if (!containsVersion)
                            {
                                var newSimpleLayout = Activator.CreateInstance(simpleLayoutType, new object[] { @"${mdc:item=dd.version}" });
                                if (newSimpleLayout is null)
                                {
                                    Log.Warning("Failed to create DD version attribute for NLog");
                                    break;
                                }

                                var newAttribute = Activator.CreateInstance(jsonAttributeType, new object[] { "dd.version", newSimpleLayout });
                                if (newAttribute is null)
                                {
                                    Log.Warning("Failed to create NLog Attribute for DD Version");
                                    break;
                                }

                                layoutWithAttributes.Attributes.Add(newAttribute);
                            }

                            if (!containsService)
                            {
                                var newSimpleLayout = Activator.CreateInstance(simpleLayoutType, new object[] { @"${mdc:item=dd.service}" });
                                if (newSimpleLayout is null)
                                {
                                    Log.Warning("Failed to create DD Service attribute for NLog");
                                    break;
                                }

                                var newAttribute = Activator.CreateInstance(jsonAttributeType, new object[] { "dd.service", newSimpleLayout });
                                if (newAttribute is null)
                                {
                                    Log.Warning("Failed to create NLog Attribute for DD Service");
                                    break;
                                }

                                layoutWithAttributes.Attributes.Add(newAttribute);
                            }
                        }
                        else if (layout.TryDuckCast<ISimpleLayoutProxy>(out var simpleLayoutProxy))
                        {
                            var currentVersion = loggingConfiguration.GetType().Assembly.GetName().Version;
                            var v43 = new Version("4.3.0");

                            bool useMdc = currentVersion < v43;

                            // hacky implementation to get everything in
                            if (!simpleLayoutProxy.Text.Contains("dd.env"))
                            {
                                if (useMdc)
                                {
                                    simpleLayoutProxy.Text += @"{dd.env: ""${mdc:item=dd.env}"",";
                                }
                                else
                                {
                                    simpleLayoutProxy.Text += @"{dd.env: ""${mdlc:item=dd.env}"",";
                                }
                            }

                            if (!simpleLayoutProxy.Text.Contains("dd.service"))
                            {
                                if (useMdc)
                                {
                                    simpleLayoutProxy.Text += @"dd.service: ""${mdc:item=dd.service}"",";
                                }
                                else
                                {
                                    simpleLayoutProxy.Text += @"dd.service: ""${mdlc:item=dd.service}"",";
                                }
                            }

                            if (!simpleLayoutProxy.Text.Contains("dd.version"))
                            {
                                if (useMdc)
                                {
                                    simpleLayoutProxy.Text += @"dd.version: ""${mdc:item=dd.version}"",";
                                }
                                else
                                {
                                    simpleLayoutProxy.Text += @"dd.version: ""${mdlc:item=dd.version}"",";
                                }
                            }

                            if (!simpleLayoutProxy.Text.Contains("dd.trace_id"))
                            {
                                if (useMdc)
                                {
                                    simpleLayoutProxy.Text += @"dd.trace_id: ""${mdc:item=dd.trace_id}"",";
                                }
                                else
                                {
                                    simpleLayoutProxy.Text += @"dd.trace_id: ""${mdlc:item=dd.trace_id}"",";
                                }
                            }

                            if (!simpleLayoutProxy.Text.Contains("dd.span_id"))
                            {
                                if (useMdc)
                                {
                                    simpleLayoutProxy.Text += @"dd.span_id: ""${mdc:item=dd.span_id}""";
                                }
                                else
                                {
                                    simpleLayoutProxy.Text += @"dd.span_id: ""${mdlc:item=dd.span_id}""";
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
