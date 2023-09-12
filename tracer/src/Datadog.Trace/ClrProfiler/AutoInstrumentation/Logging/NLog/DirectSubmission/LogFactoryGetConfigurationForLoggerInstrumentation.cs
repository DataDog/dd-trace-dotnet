// <copyright file="LogFactoryGetConfigurationForLoggerInstrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;

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
            var tracerManager = TracerManager.Instance;

            if (tracerManager.Settings.LogsInjectionEnabledInternal && configuration is not null)
            {
                ConfigureLogsInjection(configuration);
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
            }

            return CallTargetState.GetDefault();
        }

        internal static void ConfigureLogsInjection(object loggingConfiguration)
        {
            var loggingConfigurationProxy = loggingConfiguration.DuckCast<ILoggingConfigurationProxy>();
            if (loggingConfigurationProxy.ConfiguredNamedTargets is not null)
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
                        else if (layout.TryDuckCast<ISimpleLayoutProxy>(out var simpleLayoutProxy))
                        {
                            // hacky implementation to get everything in
                            if (!simpleLayoutProxy.Text.Contains("dd.env"))
                            {
                                simpleLayoutProxy.Text += @"{dd.env: ""${mdlc:item=dd.env}\"",";
                            }

                            if (!simpleLayoutProxy.Text.Contains("dd.service"))
                            {
                                simpleLayoutProxy.Text += @"dd.service: ""${mdlc:item=dd.service}"",";
                            }

                            if (!simpleLayoutProxy.Text.Contains("dd.version"))
                            {
                                simpleLayoutProxy.Text += @"dd.version: ""${mdlc:item=dd.version}"",";
                            }

                            if (!simpleLayoutProxy.Text.Contains("dd.trace_id"))
                            {
                                simpleLayoutProxy.Text += @"dd.trace_id: ""${mdlc:item=dd.trace_id}"",";
                            }

                            if (!simpleLayoutProxy.Text.Contains("dd.span_id"))
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
