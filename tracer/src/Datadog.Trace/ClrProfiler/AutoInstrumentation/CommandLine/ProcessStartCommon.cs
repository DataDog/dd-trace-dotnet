// <copyright file="ProcessStartCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Process
{
    internal static class ProcessStartCommon
    {
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.CommandExecution;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ProcessStartIntegration));
        internal const string OperationName = "command_execution";
        internal const string ServiceName = "command";

        internal static Scope CreateScope(Tracer tracer, string filename, string arguments = null, string domain = null, string userName = null)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) || !tracer.Settings.IsIntegrationEnabled(IntegrationId.AdoNet))
            {
                // integration disabled, don't create a scope, skip this span
                return null;
            }

            Scope scope = null;

            try
            {
                Span parent = tracer.InternalActiveScope?.Span;

                if (parent is { Type: SpanTypes.System } &&
                    parent.OperationName == OperationName)
                {
                    // we are already instrumenting this,
                    // don't instrument nested methods that belong to the same stacktrace
                    // e.g. ExecuteReader() -> ExecuteReader(commandBehavior)
                    return null;
                }

                var tags = new ProcessCommandStartTags
                {
                    CommandLine = filename + (arguments != null ? " " + arguments : string.Empty),
                    Domain = domain,
                    UserName = userName
                };

                var serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
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
    }
}
