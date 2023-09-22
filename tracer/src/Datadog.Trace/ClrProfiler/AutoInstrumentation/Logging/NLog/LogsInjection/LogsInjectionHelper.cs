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
    internal static class LogsInjectionHelper<TTarget>
    {
        private const string TraceId = "dd.trace_id";
        private const string SpanId = "dd.span_id";
        private const string Environment = "dd.env";
        private const string Version = "dd.version";
        private const string Service = "dd.service";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LogsInjectionHelper<TTarget>));
        private static NLogVersion _version;
        private static Type _jsonAttributeType;
        private static Type _simpleLayoutType;

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

            _version = NLogVersionHelper<TTarget>.Version;

            if (!loggingConfiguration.TryDuckCast<IBasicLoggingConfigurationProxy>(out var loggingConfigurationProxy))
            {
                Log.Warning("Failed to DuckCast LoggingConfiguration");
                return;
            }

            _jsonAttributeType = Type.GetType("NLog.Layouts.JsonAttribute, NLog", throwOnError: false);
            if (_jsonAttributeType is null)
            {
                Log.Warning("Failed to find NLog JsonAttribute type.");
                return;
            }

            _simpleLayoutType = Type.GetType("NLog.Layouts.SimpleLayout, NLog", throwOnError: false);
            if (_simpleLayoutType is null)
            {
                Log.Warning("Failed to find NLog SimpleLayoutType type.");
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
                        ConfigureJson4Layout(layoutWithAttributes);
                    }
                    else if (layout.TryDuckCast<ISimpleLayoutProxy>(out var simpleLayoutProxy))
                    {
                        ConfigureSimpleLayout(simpleLayoutProxy);
                    }
                }
            }
        }

        private static void ConfigureJson4Layout(IJsonLayout4Proxy layoutWithAttributes)
        {
            var containsTraceId = false;
            var containsSpanId = false;
            var containsVersion = false;
            var containsService = false;
            var containsEnv = false;

            foreach (var attribute in layoutWithAttributes.Attributes)
            {
                if (!attribute.TryDuckCast<IJsonAttributeProxy>(out var jsonAttributeProxy))
                {
                    continue;
                }

                switch (jsonAttributeProxy.Name)
                {
                    case Environment:
                        containsEnv = true;
                        continue;
                    case Service:
                        containsService = true;
                        continue;
                    case Version:
                        containsVersion = true;
                        continue;
                    case TraceId:
                        containsTraceId = true;
                        continue;
                    case SpanId:
                        containsSpanId = true;
                        continue;
                }
            }

            if (!containsEnv)
            {
                AddAttributeToJson4Layout(layoutWithAttributes, Environment);
            }

            if (!containsTraceId)
            {
                AddAttributeToJson4Layout(layoutWithAttributes, TraceId);
            }

            if (!containsSpanId)
            {
                AddAttributeToJson4Layout(layoutWithAttributes, SpanId);
            }

            if (!containsVersion)
            {
                AddAttributeToJson4Layout(layoutWithAttributes, Version);
            }

            if (!containsService)
            {
                AddAttributeToJson4Layout(layoutWithAttributes, Service);
            }
        }

        private static void AddAttributeToJson4Layout(IJsonLayout4Proxy layout, string attribute)
        {
            var newSimpleLayout = Activator.CreateInstance(_simpleLayoutType, new object[] { @"${mdc:item=" + $"{attribute}" + "}" });
            if (newSimpleLayout is null)
            {
                Log.Warning("Failed to create DD Service attribute for NLog");
                return;
            }

            var newAttribute = Activator.CreateInstance(_jsonAttributeType, new object[] { attribute, newSimpleLayout });
            if (newAttribute is null)
            {
                Log.Warning("Failed to create NLog Attribute for DD Service");
                return;
            }

            layout.Attributes.Add(newAttribute);
        }

        private static string CreateSimpleLayoutText(string attribute)
        {
            var useMdc = _version == NLogVersion.NLogPre43 || _version == NLogVersion.NLog43To45;
            var context = useMdc ? "mdc" : "mdlc";
            // example where attribute == dd.trace_id -> ` dd.trace_id: "${mdc:item=dd.trace_id}"`
            var result = " " + attribute + @": ""${" + $"{context}" + ":item=" + attribute + @"}""";
            return result;
        }

        private static string CreateSimpleLayoutContextText(string attribute)
        {
            var useMdc = _version == NLogVersion.NLogPre43 || _version == NLogVersion.NLog43To45;
            var context = useMdc ? "mdc" : "mdlc";
            return @": ""${" + $"{context}" + ":item=" + attribute + @"}""";
        }

        private static void ConfigureSimpleLayout(ISimpleLayoutProxy simpleLayoutProxy)
        {
            // does the current layout have any "mdc" or "mdlc" stuff for logs injection already
            // if so, don't add to it
            var envContext = CreateSimpleLayoutContextText(Environment);
            var serviceContext = CreateSimpleLayoutContextText(Service);
            var versionContext = CreateSimpleLayoutContextText(Version);
            var traceIdContext = CreateSimpleLayoutContextText(TraceId);
            var spanIdContext = CreateSimpleLayoutContextText(SpanId);

            var hasEnvironment = simpleLayoutProxy.Text.Contains(envContext);
            var hasService = simpleLayoutProxy.Text.Contains(serviceContext);
            var hasVersion = simpleLayoutProxy.Text.Contains(versionContext);
            var hasTraceId = simpleLayoutProxy.Text.Contains(traceIdContext);
            var hasSpanId = simpleLayoutProxy.Text.Contains(spanIdContext);

            if (hasEnvironment ||
                hasService ||
                hasVersion ||
                hasTraceId ||
                hasSpanId)
            {
                Log.Information("Not reconfiguring an NLogs SimpleLayout for logs injection because it looks like it already is.");
                return;
            }

            var text = string.Empty;

            text += CreateSimpleLayoutText(Environment);
            text += CreateSimpleLayoutText(Service);
            text += CreateSimpleLayoutText(Version);
            text += CreateSimpleLayoutText(TraceId);
            text += CreateSimpleLayoutText(SpanId);

            simpleLayoutProxy.Text += " {" + text + " }";
        }
    }
}
