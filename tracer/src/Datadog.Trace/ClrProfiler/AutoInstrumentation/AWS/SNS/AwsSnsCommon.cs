// <copyright file="AwsSnsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS
{
    internal static class AwsSnsCommon
    {
        private const string DatadogAwsSnsServiceName = "aws-sns";
        private const string SnsOperationName = "sns.request";
        private const string SnsServiceName = "SNS";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsSnsCommon));

        internal const string IntegrationName = nameof(Configuration.IntegrationId.AwsSns);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.AwsSns;

        public static Scope CreateScope(Tracer tracer, string operation, out AwsSnsTags tags, ISpanContext parentContext = null)
        {
            tags = null;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) || !tracer.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope scope = null;

            try
            {
                tags = new AwsSnsTags();
                string serviceName = tracer.Settings.GetServiceName(tracer, DatadogAwsSnsServiceName);
                scope = tracer.StartActiveInternal(SnsOperationName, parent: parentContext, tags: tags, serviceName: serviceName);
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{SnsServiceName}.{operation}";

                tags.Service = SnsServiceName;
                tags.TopLevelServiceName = SnsServiceName;
                tags.Operation = operation;
                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            // always returns the scope, even if it's null because we couldn't create it,
            // or we couldn't populate it completely (some tags is better than no tags)
            return scope;
        }
    }
}
