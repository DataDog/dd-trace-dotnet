// <copyright file="PublisherClientPublishAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
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
    /// Google.Cloud.PubSub.V1.PublisherClientImpl.PublishAsync calltarget instrumentation.
    /// All PublishAsync overloads of PublisherClient funnel into this method with a single PubsubMessage.
    /// </summary>
    [InstrumentMethod(
        AssemblyName = GooglePubSubConstants.AssemblyName,
        TypeName = GooglePubSubConstants.PublisherClientImplTypeName,
        MethodName = "PublishAsync",
        ReturnTypeName = GooglePubSubConstants.TaskStringTypeName,
        ParameterTypeNames = new[] { GooglePubSubConstants.PubsubMessageTypeName },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = GooglePubSubConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class PublisherClientPublishAsyncIntegration
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(PublisherClientPublishAsyncIntegration));

        internal static CallTargetState OnMethodBegin<TTarget, TMessage>(TTarget instance, TMessage message)
            where TTarget : IPublisherClient, IDuckType
            where TMessage : IPubsubMessage, IDuckType
        {
            var tracer = Tracer.Instance;

            var scope = GooglePubSubCommon.CreateProducerSpan(tracer, instance.TopicName?.TopicId);
            if (scope is null)
            {
                return CallTargetState.GetDefault();
            }

            try
            {
                var dataStreamsManager = tracer.TracerManager.DataStreamsManager;
                var span = scope.Span;
                var topicId = instance.TopicName?.TopicId;
                var attributes = message.Instance is null ? null : message.Attributes;

                if (attributes is not null)
                {
                    if (!string.IsNullOrEmpty(message.MessageId))
                    {
                        span.SetTag(Tags.MessagingMessageId, message.MessageId!);
                    }

                    var headers = new PubsubHeadersCollectionAdapter(attributes);
                    var propagationContext = new PropagationContext(span.Context, Baggage.Current);
                    tracer.TracerManager.SpanContextPropagator.Inject(propagationContext, headers);

                    if (dataStreamsManager.IsEnabled)
                    {
                        var edgeTags = dataStreamsManager.GetOrCreateEdgeTags(
                            new PubSubEdgeTagCacheKey(topicId ?? string.Empty, IsConsume: false),
                            static k => GooglePubSubCommon.BuildProduceEdgeTags(k.EntityName));
                        var msgSize = dataStreamsManager.IsInDefaultState ? 0 : GooglePubSubCommon.GetMessageSize(message);
                        span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, msgSize, 0);
                        dataStreamsManager.InjectPathwayContextAsBase64String(span.Context.PathwayContext, headers);
                    }
                }
            }
            catch (Exception ex)
            {
                // do not fail the publish because of the tracer
                Log.Error(ex, "Error instrumenting Google Cloud Pub/Sub publish");
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
