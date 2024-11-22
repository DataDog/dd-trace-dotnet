// <copyright file="PutRecordAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// AWSSDK.Kinesis PutRecordAsync CallTarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.Kinesis",
        TypeName = "Amazon.Kinesis.AmazonKinesisClient",
        MethodName = "PutRecordAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.Kinesis.Model.PutRecordResponse]",
        ParameterTypeNames = new[] { "Amazon.Kinesis.Model.PutRecordRequest", ClrNames.CancellationToken },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsKinesisCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class PutRecordAsyncIntegration
    {
        private const string Operation = "PutRecord";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<PutRecordAsyncIntegration>();

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TPutRecordRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the Kinesis operation</param>
        /// <param name="cancellationToken">CancellationToken value</param>
        /// <returns>CallTarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TPutRecordRequest>(TTarget instance, TPutRecordRequest request, CancellationToken cancellationToken)
            where TPutRecordRequest : IPutRecordRequest, IDuckType
        {
            Log.Warning("PutRecordAsync onmethodbegin 1");
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            Log.Warning("PutRecordAsync onmethodbegin 2");

            var scope = AwsKinesisCommon.CreateScope(Tracer.Instance, Operation, SpanKinds.Producer, null, out var tags);
            if (tags is not null)
            {
                tags.StreamName = request.StreamName;
            }

            Log.Warning("PutRecordAsync onmethodbegin 3");
            Log.Warning("PutRecordAsync onmethodbegin streamName {0}", request.StreamName);
            Log.Warning("PutRecordAsync onmethodbegin 4");

            if (scope?.Span.Context != null && !string.IsNullOrEmpty(request.StreamName))
            {
                Log.Warning("PutRecordAsync onmethodbegin 5");
                var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
                if (dataStreamsManager != null && dataStreamsManager.IsEnabled)
                {
                    Log.Warning("PutRecordAsync onmethodbegin 6");

                    var edgeTags = new[] { "direction:out", $"topic:{request.StreamName}", "type:kinesis" };
                    scope.Span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Produce, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
                }

                Log.Warning("PutRecordAsync onmethodbegin 7");
            }

            Log.Warning("PutRecordAsync onmethodbegin 8");

            var context = new PropagationContext(scope?.Span.Context, Baggage.Current);
            ContextPropagation.InjectTraceIntoData(request, context);
            Log.Warning("PutRecordAsync onmethodbegin 9");

            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">CallTarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception? exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
