// <copyright file="AwsStepFunctionsCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Schema;
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
        private const string StepFunctionsRequestOperationName = "stepfunctions.request";
        private const string StateMachineResourceDelimiter = ":stateMachine:";
        private static readonly char[] StateMachineNameTerminators = { ':', '/' };
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsStepFunctionsCommon));

        public static Scope? CreateScope(Tracer tracer, string operation, string spanKind, out AwsStepFunctionsTags? tags, ISpanContext? parentContext = null)
        {
            tags = null;
            var perTraceSettings = tracer.CurrentTraceSettings;
            if (!perTraceSettings.Settings.IsIntegrationEnabled(IntegrationId) || !perTraceSettings.Settings.IsIntegrationEnabled(AwsConstants.IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            Scope? scope = null;

            try
            {
                tags = perTraceSettings.Schema.Messaging.CreateAwsStepFunctionsTags(spanKind);
                var (serviceName, serviceNameSource) = perTraceSettings.GetServiceNameMetadata(DatadogAwsStepFunctionsServiceName);
                var operationName = GetOperationName(tracer, spanKind);
                scope = tracer.StartActiveInternal(operationName, parent: parentContext, tags: tags, serviceName: serviceName, serviceNameSource: serviceNameSource);
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{StepFunctionsServiceName}.{operation}";

                tags.Service = StepFunctionsServiceName;
                tags.Operation = operation;
                tags.SetAnalyticsSampleRate(IntegrationId, perTraceSettings.Settings, enabledWithGlobalSetting: false);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        /// <summary>
        /// Extracts the state machine name from the state machine ARN. The ARN may be unqualified, or
        /// qualified with a version, an alias, or a Distributed Map state label:
        /// `arn:aws:states:region:account:stateMachine:name`,
        /// `arn:aws:states:region:account:stateMachine:name:version`,
        /// `arn:aws:states:region:account:stateMachine:name:alias`, or
        /// `arn:aws:states:region:account:stateMachine:name/mapStateLabel`.
        /// Returns null when the ARN is null or no name can be found.
        /// </summary>
        public static string? GetStateMachineName(string? stateMachineArn)
        {
            if (stateMachineArn is null)
            {
                return stateMachineArn;
            }

            var resourceIndex = stateMachineArn.IndexOf(StateMachineResourceDelimiter, StringComparison.Ordinal);
            if (resourceIndex >= 0)
            {
                // The name follows the `stateMachine` resource type and may itself be followed by a
                // `:version`, `:alias`, or `/mapStateLabel` qualifier. State machine names cannot contain
                // ':' or '/', so the first of either character terminates the name.
                var nameStartIndex = resourceIndex + StateMachineResourceDelimiter.Length;
                var qualifierIndex = stateMachineArn.IndexOfAny(StateMachineNameTerminators, nameStartIndex);

                var name = qualifierIndex < 0
                               ? stateMachineArn.Substring(nameStartIndex)
                               : stateMachineArn.Substring(nameStartIndex, qualifierIndex - nameStartIndex);
                return NullIfEmpty(name);
            }

            // Not a recognized state machine ARN, so fall back to the trailing segment.
            var lastSeparationIndex = stateMachineArn.LastIndexOf(':') + 1;
            return NullIfEmpty(stateMachineArn.Substring(lastSeparationIndex));
        }

        private static string? NullIfEmpty(string value) => value.Length == 0 ? null : value;

        internal static string GetOperationName(Tracer tracer, string spanKind)
        {
            if (tracer.CurrentTraceSettings.Schema.Version == SchemaVersion.V0)
            {
                return StepFunctionsRequestOperationName;
            }

            return spanKind switch
            {
                SpanKinds.Consumer => tracer.CurrentTraceSettings.Schema.Messaging.GetInboundOperationName(MessagingSchema.OperationType.AwsStepFunctions),
                SpanKinds.Producer => tracer.CurrentTraceSettings.Schema.Messaging.GetOutboundOperationName(MessagingSchema.OperationType.AwsStepFunctions),
                _ => "aws.stepfunctions.request"
            };
        }
    }
}
