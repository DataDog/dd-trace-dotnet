// <copyright file="QuartzCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Quartz
{
    internal static class QuartzCommon
    {
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.Quartz;
        internal const string QuartzServiceName = "Quartz";
        internal const string QuartzType = "job";
        internal const string OnJobExecuteOperation = "quartz.job.execute";
        internal const string OnJobVetoOperation = "quartz.job.veto";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(QuartzCommon));

        public static Scope? CreateScope(Tracer tracer, object quartzArgs, string operationName, ISpanContext? parentContext = null, bool finishOnClose = true)
        {
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope? scope = null;

            try
            {
                var serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, QuartzServiceName);
                scope = tracer.StartActiveInternal(operationName, parent: parentContext, serviceName: serviceName);
                var span = scope.Span;
                span.Type = QuartzType;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
                if (quartzArgs.TryDuckCast<IJobDiagnosticDataProxy>(out var typedArg))
                {
                    PopulateJobSpanTags(scope, typedArg, operationName);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating Quartz scope.");
            }

            return scope;
        }

        internal static void PopulateJobSpanTags(IScope scope, IJobDiagnosticDataProxy jobDiagnosticData, string operationName)
        {
            var resourceNameValue = operationName switch
            {
                OnJobExecuteOperation => $"execute {jobDiagnosticData.JobDetail.Key.Name}",
                OnJobVetoOperation => $"veto {jobDiagnosticData.JobDetail.Key.Name}",
                _ => jobDiagnosticData.JobDetail.ToString()
            };

            scope.Span.ResourceName = resourceNameValue;
            scope.Span.SetTag("job.key", jobDiagnosticData.JobDetail.Key.ToString());
        }
    }
}
