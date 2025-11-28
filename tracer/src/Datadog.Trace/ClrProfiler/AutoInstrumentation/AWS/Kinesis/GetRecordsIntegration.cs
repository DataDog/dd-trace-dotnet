// <copyright file="GetRecordsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// AWSSDK.Kinesis GetRecords CallTarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.Kinesis",
        TypeName = "Amazon.Kinesis.AmazonKinesisClient",
        MethodName = "GetRecords",
        ReturnTypeName = "Amazon.Kinesis.Model.GetRecordsResponse",
        ParameterTypeNames = new[] { "Amazon.Kinesis.Model.GetRecordsRequest" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = AwsKinesisCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class GetRecordsIntegration
    {
        private const string Operation = "GetRecords";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TGetRecordsRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the Kinesis operation</param>
        /// <returns>CallTarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TGetRecordsRequest>(TTarget instance, TGetRecordsRequest request)
            where TGetRecordsRequest : IGetRecordsRequest, IDuckType
        {
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = AwsKinesisCommon.CreateScope(Tracer.Instance, Operation, SpanKinds.Producer, null, out var tags);

            string? streamName = AwsKinesisCommon.StreamNameFromARN(request.StreamARN);
            if (tags is not null && streamName is not null)
            {
                tags.StreamName = streamName;
            }

            return new CallTargetState(scope);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the return value</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">Task of HttpResponse message instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static CallTargetReturn<TResponse> OnMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception? exception, in CallTargetState state)
            where TResponse : IGetRecordsResponse, IDuckType
        {
            if (response.Instance != null && response.Records is { Count: > 0 } && state is { State: not null, Scope.Span: { } span })
            {
                var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
                if (dataStreamsManager is { IsEnabled: true })
                {
                    var edgeTags = new[] { "direction:in", $"topic:{(string)state.State}", "type:kinesis" };
                    foreach (var o in response.Records)
                    {
                        var record = o.DuckCast<IRecord>();
                        if (record == null)
                        {
                            continue; // should not happen
                        }

                        span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Consume, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
                    }
                }
            }

            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TResponse>(response);
        }
    }
}
