// <copyright file="AwsStepFunctionsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.StepFunctions
{
    internal static class AwsStepFunctionsCommon
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.AwsStepFunctions);
        internal const IntegrationId IntegrationId = Configuration.IntegrationId.AwsStepFunctions;

        private const string DatadogAwsStepFunctionsServiceName = "aws-stepfunctions";
        private const string StepFunctionsServiceName = "StepFunctions";
        private const string StepFunctionsOperationName = "aws.stepfunctions";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsStepFunctionsCommon));

        public static Scope? CreateScope(Tracer tracer, string operation, string spanKind, out AwsSdkTags? tags, ISpanContext? parentContext = null)
        {
            tags = null;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) || !tracer.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope? scope = null;

            try
            {
                var serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, DatadogAwsStepFunctionsServiceName);
                var operationName = GetOperationName(tracer, spanKind);
                scope = tracer.StartActiveInternal(operationName, parent: parentContext, tags: tags, serviceName: serviceName);
                var span = scope.Span;
                tags = span.Tags as AwsSdkTags;

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{StepFunctionsServiceName}.{operation}";

                if (tags != null)
                {
                    tags.Service = StepFunctionsServiceName;
                    tags.Operation = operation;
                    tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                }

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

        internal static string GetOperationName(Tracer tracer, string spanKind)
        {
            return StepFunctionsOperationName;
        }
    }
}
