// <copyright file="LogsInjectionHelper.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.Pre43;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using ILoggingRuleProxy = Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.DirectSubmission.Proxies.ILoggingRuleProxy;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Logging.NLog.LogsInjection
{
    /// <summary>
    /// Helper class to add necessary configuration when logs injection is enabled.
    /// </summary>
    internal static class LogsInjectionHelper<TTarget>
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(LogsInjectionHelper<TTarget>));
        private static readonly Type? _jsonAttributeType;
        private static readonly Type? _simpleLayoutType;

        static LogsInjectionHelper()
        {
            // this is not available in older versions of NLog (e.g., v2.1 doesn't have JSON support)
            _jsonAttributeType = Type.GetType("NLog.Layouts.JsonAttribute, NLog", throwOnError: false);
            if (_jsonAttributeType is null)
            {
                return;
            }

            // this simple layout should exist for all versions
            _simpleLayoutType = Type.GetType("NLog.Layouts.SimpleLayout, NLog", throwOnError: false);
        }

        /// <summary>
        ///     Adds necessary configuration elements to inject trace information in logs.
        /// </summary>
        /// <param name="loggingConfiguration">The NLog LoggingConfiguration to configure.</param>
        public static void ConfigureLogsInjectionForLoggerConfiguration(object? loggingConfiguration)
        {
            if (loggingConfiguration is null || _jsonAttributeType is null || _simpleLayoutType is null)
            {
                return;
            }

            if (!loggingConfiguration.TryDuckCast<IBasicLoggingConfigurationProxy>(out var loggingConfigurationProxy))
            {
                Log.Warning("Failed to DuckCast LoggingConfiguration");
                return;
            }

            ConfigureTargets(loggingConfigurationProxy.ConfiguredNamedTargets, out _);
        }

        public static void ConfigureLogsInjectionForLoggingRules<TLoggingRules>(TLoggingRules? loggingRules, out bool foundDirectSubmissionTarget)
        {
            foundDirectSubmissionTarget = false;
            if (loggingRules is not IList { Count: > 0 } loggingRulesList || _jsonAttributeType is null || _simpleLayoutType is null)
            {
                return;
            }

            foreach (var loggingRule in loggingRulesList)
            {
                var loggingRuleProxy = loggingRule.DuckCast<ILoggingRuleProxy>();
                if (loggingRuleProxy?.Targets is { } targets)
                {
                    ConfigureTargets(targets, out var foundOurTarget);
                    foundDirectSubmissionTarget |= foundOurTarget;
                }
            }
        }

        private static void ConfigureTargets(IEnumerable configuredNamedTargets, out bool foundDirectSubmissionTarget)
        {
            foundDirectSubmissionTarget = false;
            foreach (var target in configuredNamedTargets)
            {
                if (target is IDuckType { Instance: DirectSubmissionNLogV5Target } ||
                    target is IDuckType { Instance: DirectSubmissionNLogTarget } ||
                    target is IDuckType { Instance: DirectSubmissionNLogLegacyTarget })
                {
                    // don't want to configure our own target
                    foundDirectSubmissionTarget = true;
                    continue;
                }

                if (target.TryDuckCast<ITargetWithLayoutProxy>(out var targetWithLayout))
                {
                    var layout = targetWithLayout.Layout;

                    if (layout.TryDuckCast<IJson5LayoutProxy>(out var layoutWithScope))
                    {
                        layoutWithScope.IncludeScopePropertiesField = true;
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
                }
            }
        }

        private static void ConfigureJson4Layout(IJsonLayout4Proxy layoutWithAttributes)
        {
            if (_jsonAttributeType is null || _simpleLayoutType is null)
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

            try
            {
                AddAttributeToJson4Layout(layoutWithAttributes, CorrelationIdentifier.EnvKey);
                AddAttributeToJson4Layout(layoutWithAttributes, CorrelationIdentifier.TraceIdKey);
                AddAttributeToJson4Layout(layoutWithAttributes, CorrelationIdentifier.SpanIdKey);
                AddAttributeToJson4Layout(layoutWithAttributes, CorrelationIdentifier.VersionKey);
                AddAttributeToJson4Layout(layoutWithAttributes, CorrelationIdentifier.ServiceKey);
            }
            catch (Exception ex)
            {
                Log.Error("Failed to automatically configure NLog JsonLayout attributes for logs injection: {Message}", ex.Message);
                return;
            }
        }

        private static void AddAttributeToJson4Layout(IJsonLayout4Proxy layout, string attribute)
        {
            // _simpleLayoutType and _jsonAttributeType should be checked for null in callee
            var newSimpleLayout = Activator.CreateInstance(_simpleLayoutType!, new object[] { @"${mdc:item=" + $"{attribute}" + "}" })!;
            var newAttribute = Activator.CreateInstance(_jsonAttributeType!, new object[] { attribute, newSimpleLayout });

            layout.Attributes.Add(newAttribute);
        }
    }
}
