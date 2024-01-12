// <copyright file="PublishAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SNS
{
    /// <summary>
    /// AWSSDK.SNS PublishAsync CallTarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.SimpleNotificationService",
        TypeName = "Amazon.SimpleNotificationService.AmazonSimpleNotificationServiceClient",
        MethodName = "PublishAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.SimpleNotificationService.Model.PublishResponse]",
        ParameterTypeNames = new[] { "Amazon.SimpleNotificationService.Model.PublishRequest", ClrNames.CancellationToken },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsSnsCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class PublishAsyncIntegration
    {
        private const string Operation = "Publish";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TPublishRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the SNS operation</param>
        /// <param name="cancellationToken">CancellationToken value</param>
        /// <returns>CallTarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TPublishRequest>(TTarget instance, TPublishRequest request, CancellationToken cancellationToken)
            where TPublishRequest : IPublishRequest, IDuckType
        {
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = AwsSnsCommon.CreateScope(Tracer.Instance, Operation, SpanKinds.Producer, out var tags);
            if (tags is not null && request.TopicArn is not null)
            {
                tags.TopicArn = request.TopicArn;
                tags.TopicName = AwsSnsCommon.GetTopicName(request.TopicArn);
            }

            if (scope?.Span.Context is { } context)
            {
                ContextPropagation.InjectHeadersIntoMessage<TTarget, TPublishRequest>(request, context);
            }

            return new CallTargetState(scope);
        }

        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
