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

            // TODO I think we'll always have these?
            _jsonAttributeType = Type.GetType("NLog.Layouts.JsonAttribute, NLog", throwOnError: false);
            if (_jsonAttributeType is null)
            {
                Log.Warning("Failed to find NLog JsonAttribute type.");
            }

            _simpleLayoutType = Type.GetType("NLog.Layouts.SimpleLayout, NLog", throwOnError: false);
            if (_simpleLayoutType is null)
            {
                Log.Warning("Failed to find NLog SimpleLayoutType type.");
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

            // hacky implementation to bruteforce attributes
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
            // TODO move null check out of this probably; can they be null?
            if (_simpleLayoutType is null || _jsonAttributeType is null)
            {
                Log.Warning("Can't add logs injection to NLog JSON attribute type as we couldn't find the types.");
                return;
            }

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

        private static string CreateSimpleLayoutText(string attribute, bool addComma = true)
        {
            var useMdc = _version == NLogVersion.NLogPre43 || _version == NLogVersion.NLog43To45;
            string result;
            if (useMdc)
            {
                result = "," + attribute + @": ""${mdc:item=" + attribute + @"}""";
            }
            else
            {
                result = "," + attribute + @": ""${mdlc:item=" + attribute + @"}""";
            }

            return result;
        }

        private static void ConfigureSimpleLayout(ISimpleLayoutProxy simpleLayoutProxy)
        {
            var useMdc = _version == NLogVersion.NLogPre43 || _version == NLogVersion.NLog43To45;
            string text = string.Empty;

            // hacky implementation to get everything in
            if (!simpleLayoutProxy.Text.Contains(Environment))
            {
                text += CreateSimpleLayoutText(Environment);
            }

            if (!simpleLayoutProxy.Text.Contains(Service))
            {
                text += CreateSimpleLayoutText(Service);
            }

            if (!simpleLayoutProxy.Text.Contains(Version))
            {
                text += CreateSimpleLayoutText(Version);
            }

            if (!simpleLayoutProxy.Text.Contains(TraceId))
            {
                text += CreateSimpleLayoutText(TraceId);
            }

            if (!simpleLayoutProxy.Text.Contains(SpanId))
            {
                text += CreateSimpleLayoutText(SpanId);
            }

            if (!string.IsNullOrEmpty(text))
            {
                simpleLayoutProxy.Text += "{" + text + "}";
            }
        }
    }
}
