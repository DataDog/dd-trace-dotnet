// <copyright file="LogsInjectionHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection
{
    /// <summary>
    /// Helper class to add necessary configuration when logs injection is enabled.
    /// </summary>
    internal static class LogsInjectionHelper
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LogsInjectionHelper));
        private static NLogVersion? _nLogVersion = null;

        private enum NLogVersion
        {
            NLog50,
            NLog45,
            NLog43To45,
            NLogPre43,
        }

        /// <summary>
        ///     Adds necessary configuration elements to inject trace information in logs.
        /// </summary>
        /// <param name="loggingConfiguration">The NLog LoggingConfiguration to configure.</param>
        public static void ConfigureLogsInjection(object loggingConfiguration)
        {
            if (loggingConfiguration == null)
            {
                return;
            }

            if (_nLogVersion is null)
            {
                var assembly = loggingConfiguration.GetType().Assembly;
                if (assembly is null)
                {
                    Log.Warning("Failed to get the NLog Assembly in ConfigureLogsInjection");
                    return;
                }

                var targetWithContext = assembly.GetType("NLog.Targets.TargetWithContext");

                if (targetWithContext?.GetProperty("IncludeScopeProperties") is not null)
                {
                    _nLogVersion = NLogVersion.NLog50;
                }
                else if (targetWithContext is not null)
                {
                    _nLogVersion = NLogVersion.NLog45;
                }
                else if (targetWithContext is null)
                {
                    // Type was added in NLog 4.3, so we can use it to safely determine the version
                    var testType = assembly.GetType("NLog.Config.ExceptionRenderingFormat");
                    _nLogVersion = testType is null ? NLogVersion.NLogPre43 : NLogVersion.NLog43To45;
                }

                Log.Information("Setting NLog version to {Version}", _nLogVersion.ToString());
            }

            if (!loggingConfiguration.TryDuckCast<IBasicLoggingConfigurationProxy>(out var loggingConfigurationProxy))
            {
                Log.Warning("Failed to DuckCast LoggingConfiguration");
                return;
            }

            ConfigureTargets(loggingConfigurationProxy.ConfiguredNamedTargets);
        }

        private static void ConfigureTargets(IEnumerable configuredNamedTargets)
        {
            foreach (var target in configuredNamedTargets)
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
                        bool useMdc = _nLogVersion == NLogVersion.NLogPre43 || _nLogVersion == NLogVersion.NLog43To45;

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
