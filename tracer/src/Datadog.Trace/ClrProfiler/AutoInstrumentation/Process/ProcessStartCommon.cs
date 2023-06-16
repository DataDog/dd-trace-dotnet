// <copyright file="ProcessStartCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Process
{
    internal static class ProcessStartCommon
    {
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.Process;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProcessStartCommon));
        internal const string OperationName = "command_execution";
        internal const string ServiceName = "command";
        internal const int MaxCommandLineLength = 4096;

        internal static Scope CreateScope(ProcessStartInfo info)
        {
            if (info != null)
            {
                return CreateScope(info.FileName, info.UseShellExecute ? null : info.Environment);
            }

            return null;
        }

        internal static Scope CreateScope(string filename, IDictionary<string, string> environmentVariables)
        {
            var tracer = Tracer.Instance;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this span
                return null;
            }

            Scope scope = null;

            try
            {
                var variablesTruncated = EnvironmentVariablesScrubber.ScrubEnvironmentVariables(environmentVariables);
                variablesTruncated = Truncate(variablesTruncated, MaxCommandLineLength);

                var tags = new ProcessCommandStartTags
                {
                    EnvironmentVariables = variablesTruncated,
                };

                var serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, ServiceName);
                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                scope = tracer.StartActiveInternal(OperationName, serviceName: serviceName, tags: tags);
                scope.Span.ResourceName = filename;
                scope.Span.Type = SpanTypes.System;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating execute command scope.");
            }

            return scope;
        }

        internal static string Truncate(string value, int maxLength)
        {
            return (string.IsNullOrEmpty(value) || value.Length <= maxLength) ? value : value.Substring(0, maxLength);
        }
    }
}
