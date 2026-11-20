// <copyright file="SimpleSubscriptionHandlerHandleMessageIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

#nullable enable

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Google.CloudPubSub
{
    /// <summary>
    /// Google.Cloud.PubSub.V1.SimpleSubscriptionHandler.HandleMessage calltarget instrumentation.
    /// Every message pulled by a SubscriberClient created with StartAsync(Func{...}) flows through here,
    /// right before the user callback runs.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = GooglePubSubConstants.AssemblyName,
        TypeName = GooglePubSubConstants.SimpleSubscriptionHandlerTypeName,
        MethodName = "HandleMessage",
        ReturnTypeName = GooglePubSubConstants.TaskReplyTypeName,
        ParameterTypeNames = new[] { GooglePubSubConstants.PubsubMessageTypeName, ClrNames.CancellationToken },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = GooglePubSubConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class SimpleSubscriptionHandlerHandleMessageIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SimpleSubscriptionHandlerHandleMessageIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message, CancellationToken cancellationToken)
            where TMessage : IPubsubMessage, IDuckType
        {
            var tracer = Tracer.Instance;

            PropagationContext extractedContext = default;
            PathwayContext? pathwayContext = null;

            var attributes = message.Instance is null ? null : message.Attributes;
            if (attributes is not null)
            {
                var headers = new PubsubHeadersCollectionAdapter(attributes);

                try
                {
                    extractedContext = tracer.TracerManager.SpanContextPropagator.Extract(headers);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting propagated headers from a Google Cloud Pub/Sub message");
                }

                try
                {
                    pathwayContext = tracer.TracerManager.DataStreamsManager.ExtractPathwayContextAsBase64String(headers);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error extracting PathwayContext from a Google Cloud Pub/Sub message");
                }
            }

            var subscriptionId = GooglePubSubCommon.GetSubscription(instance);

            var scope = GooglePubSubCommon.CreateConsumerSpan(tracer, subscriptionId, extractedContext);
            if (scope is null)
            {
                return CallTargetState.GetDefault();
            }

            try
            {
                var span = scope.Span;
                var tags = (Tagging.GoogleCloudPubSubTags)span.Tags;

                if (!string.IsNullOrEmpty(message.MessageId))
                {
                    span.SetTag(Tags.MessagingMessageId, message.MessageId!);
                }

                var queueTimeMs = GooglePubSubCommon.GetMessageQueueTimeMs(span, message);
                if (queueTimeMs is not null)
                {
                    tags.MessageQueueTimeMs = queueTimeMs;
                }

                var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
                if (dataStreamsManager.IsEnabled)
                {
                    var edgeTags = dataStreamsManager.GetOrCreateEdgeTags(
                        new PubSubEdgeTagCacheKey(subscriptionId ?? string.Empty, IsConsume: true),
                        static k => GooglePubSubCommon.BuildConsumeEdgeTags(k.EntityName));
                    var msgSize = dataStreamsManager.IsInDefaultState ? 0 : GooglePubSubCommon.GetMessageSize(message);
                    span.SetDataStreamsCheckpoint(
                        dataStreamsManager,
                        CheckpointKind.Consume,
                        edgeTags,
                        msgSize,
                        (long)(queueTimeMs ?? 0),
                        pathwayContext);
                }
            }
            catch (Exception ex)
            {
                // do not fail the handler because of the tracer
                Log.Error(ex, "Error instrumenting a Google Cloud Pub/Sub message");
            }

            return new CallTargetState(scope);
        }

        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception? exception, in CallTargetState state)
        {
            state.Scope?.DisposeWithException(exception);
            return response;
        }
    }
}
