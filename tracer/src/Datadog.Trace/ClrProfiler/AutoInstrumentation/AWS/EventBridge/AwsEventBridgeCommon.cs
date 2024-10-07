// <copyright file="AwsEventBridgeCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.EventBridge
{
    internal static class AwsEventBridgeCommon
    {
        private const string DatadogAwsEventBridgeServiceName = "aws-eventbridge";
        private const string EventBridgeRequestOperationName = "eventbridge.request";
        private const string EventBridgeServiceName = "EventBridge";
        private const string EventBridgeOperationName = "aws.eventbridge";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(AwsEventBridgeCommon));

        internal const string IntegrationName = nameof(IntegrationId.AwsEventBridge);
        private const IntegrationId IntegrationId = Configuration.IntegrationId.AwsEventBridge;

        public static Scope? CreateScope(Tracer tracer, string operation, string spanKind, out AwsEventBridgeTags? tags, ISpanContext? parentContext = null)
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
                tags = tracer.CurrentTraceSettings.Schema.Messaging.CreateAwsEventBridgeTags(spanKind);
                var serviceName = tracer.CurrentTraceSettings.GetServiceName(tracer, DatadogAwsEventBridgeServiceName);
                var operationName = GetOperationName(tracer);
                scope = tracer.StartActiveInternal(operationName, parent: parentContext, tags: tags, serviceName: serviceName);
                var span = scope.Span;

                span.Type = SpanTypes.Http;
                span.ResourceName = $"{EventBridgeServiceName}.{operation}";

                tags.Service = EventBridgeServiceName;
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

        // Finds the first entry that has a valid bus name, or null if not found.
        public static string? GetBusName(IEnumerable? entries)
        {
            if (entries is null)
            {
                return null;
            }

            foreach (var entry in entries)
            {
                var duckEntry = entry.DuckCast<IPutEventsRequestEntry>();
                if (duckEntry is not null && !string.IsNullOrEmpty(duckEntry.EventBusName))
                {
                    return duckEntry.EventBusName;
                }
            }

            return null;
        }

        internal static string GetOperationName(Tracer tracer)
        {
            return tracer.CurrentTraceSettings.Schema.Version == SchemaVersion.V0
                       ? EventBridgeRequestOperationName
                       : tracer.CurrentTraceSettings.Schema.Messaging.GetOutboundOperationName(EventBridgeOperationName);
        }
    }
}
