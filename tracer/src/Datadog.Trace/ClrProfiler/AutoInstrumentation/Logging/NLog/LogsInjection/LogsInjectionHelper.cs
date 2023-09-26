// <copyright file="LogsInjectionHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission;
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
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LogsInjectionHelper<TTarget>));
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

            if (!loggingConfiguration.TryDuckCast<IBasicLoggingConfigurationProxy>(out var loggingConfigurationProxy))
            {
                Log.Warning("Failed to DuckCast LoggingConfiguration");
                return;
            }

            // this is not available in older versions of NLog (e.g., v2.1 doesn't have JSON support)
            _jsonAttributeType = Type.GetType("NLog.Layouts.JsonAttribute, NLog", throwOnError: false);

            _simpleLayoutType = Type.GetType("NLog.Layouts.SimpleLayout, NLog", throwOnError: false);
            if (_simpleLayoutType is null)
            {
                // every version we support should have this
                Log.Warning("Failed to find NLog SimpleLayoutType type");
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
            if (_jsonAttributeType is null)
            {
                Log.Warning("Can't configure NLog JsonLayout as the JsonAttribute wasn't found in NLog.");
                return;
            }

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
                    case CorrelationIdentifier.EnvKey:
                        containsEnv = true;
                        continue;
                    case CorrelationIdentifier.ServiceKey:
                        containsService = true;
                        continue;
                    case CorrelationIdentifier.VersionKey:
                        containsVersion = true;
                        continue;
                    case CorrelationIdentifier.TraceIdKey:
                        containsTraceId = true;
                        continue;
                    case CorrelationIdentifier.SpanIdKey:
                        containsSpanId = true;
                        continue;
                }
            }

            if (containsEnv ||
                containsTraceId ||
                containsSpanId ||
                containsVersion ||
                containsService)
            {
                return;
            }

            AddAttributeToJson4Layout(layoutWithAttributes, CorrelationIdentifier.EnvKey);
            AddAttributeToJson4Layout(layoutWithAttributes, CorrelationIdentifier.TraceIdKey);
            AddAttributeToJson4Layout(layoutWithAttributes, CorrelationIdentifier.SpanIdKey);
            AddAttributeToJson4Layout(layoutWithAttributes, CorrelationIdentifier.VersionKey);
            AddAttributeToJson4Layout(layoutWithAttributes, CorrelationIdentifier.ServiceKey);
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
            var useMdc = NLogCommon<TTarget>.Version == NLogVersion.NLogPre43 || NLogCommon<TTarget>.Version == NLogVersion.NLog43To45;
            var context = useMdc ? "mdc" : "mdlc";
            // example where attribute == dd.trace_id -> ` dd.trace_id: "${mdc:item=dd.trace_id}"`
            var result = " " + attribute + @": ""${" + $"{context}" + ":item=" + attribute + @"}""";
            return result;
        }

        private static string CreateSimpleLayoutContextText(string attribute)
        {
            var useMdc = NLogCommon<TTarget>.Version == NLogVersion.NLogPre43 || NLogCommon<TTarget>.Version == NLogVersion.NLog43To45;
            var context = useMdc ? "mdc" : "mdlc";
            return @": ""${" + $"{context}" + ":item=" + attribute + @"}""";
        }

        private static void ConfigureSimpleLayout(ISimpleLayoutProxy simpleLayoutProxy)
        {
            // does the current layout have any "mdc" or "mdlc" stuff for logs injection already
            // if so, don't add to it
            var envContext = CreateSimpleLayoutContextText(CorrelationIdentifier.EnvKey);
            var serviceContext = CreateSimpleLayoutContextText(CorrelationIdentifier.ServiceKey);
            var versionContext = CreateSimpleLayoutContextText(CorrelationIdentifier.VersionKey);
            var traceIdContext = CreateSimpleLayoutContextText(CorrelationIdentifier.TraceIdKey);
            var spanIdContext = CreateSimpleLayoutContextText(CorrelationIdentifier.SpanIdKey);

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
                return;
            }

            var text = string.Empty;

            text += CreateSimpleLayoutText(CorrelationIdentifier.EnvKey);
            text += CreateSimpleLayoutText(CorrelationIdentifier.ServiceKey);
            text += CreateSimpleLayoutText(CorrelationIdentifier.VersionKey);
            text += CreateSimpleLayoutText(CorrelationIdentifier.TraceIdKey);
            text += CreateSimpleLayoutText(CorrelationIdentifier.SpanIdKey);

            simpleLayoutProxy.Text += " {" + text + " }";
        }
    }
}
