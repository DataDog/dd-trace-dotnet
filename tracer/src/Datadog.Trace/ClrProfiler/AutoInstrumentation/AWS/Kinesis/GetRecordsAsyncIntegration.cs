// <copyright file="GetRecordsAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DataStreamsMonitoring;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// AWSSDK.Kinesis GetRecordsAsync CallTarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.Kinesis",
        TypeName = "Amazon.Kinesis.AmazonKinesisClient",
        MethodName = "GetRecordsAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.Kinesis.Model.GetRecordsResponse]",
        ParameterTypeNames = new[] { "Amazon.Kinesis.Model.GetRecordsRequest", ClrNames.CancellationToken },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsKinesisCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class GetRecordsAsyncIntegration
    {
        private const string Operation = "GetRecords";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<GetRecordsAsyncIntegration>();

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TGetRecordsRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the Kinesis operation</param>
        /// <param name="cancellationToken">CancellationToken value</param>
        /// <returns>CallTarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TGetRecordsRequest>(TTarget instance, TGetRecordsRequest request, CancellationToken cancellationToken)
            where TGetRecordsRequest : IGetRecordsRequest, IDuckType
        {
            Log.Warning("GetRecordsAsync onmethodbegin 1");
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            Log.Warning("GetRecordsAsync onmethodbegin 2");

            var scope = AwsKinesisCommon.CreateScope(Tracer.Instance, Operation, SpanKinds.Producer, null, out var tags);

            string? streamName = null;
            var arnComponents = request.StreamARN.Split('/');
            if (arnComponents.Length == 2)
            {
                streamName = arnComponents[1];
            }

            if (tags is not null && streamName is not null)
            {
                tags.StreamName = streamName;
            }

            Log.Warning("GetRecordsAsync onmethodbegin 3");

            return new CallTargetState(scope, streamName);
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
            where TResponse : IGetRecordsResponse, IDuckType
        {
            Log.Warning("GetRecordsAsync onasyncmethodend 1");

            if (response.Instance != null)
            {
                Log.Warning("GetRecordsAsync has instance");
                if (response.Records is { Count: > 0 })
                {
                    Log.Warning<int>("GetRecordsAsync has count {Count}", response.Records.Count);
                    if (state is { State: not null })
                    {
                        Log.Warning("GetRecordsAsync state not null");
                    }
                }
                else
                {
                    Log.Warning("GetRecordsAsync has zero count");
                }
            }

            if (response.Instance != null && response.Records is { Count: > 0 } && state is { State: not null, Scope.Span: { } span })
            {
                Log.Warning("GetRecordsAsync onasyncmethodend 2");
                var dataStreamsManager = Tracer.Instance.TracerManager.DataStreamsManager;
                if (dataStreamsManager is { IsEnabled: true })
                {
                    Log.Warning("GetRecordsAsync onasyncmethodend 3");
                    var edgeTags = new[] { "direction:in", $"topic:{(string)state.State}", "type:kinesis" };
                    foreach (var o in response.Records)
                    {
                        Log.Warning("GetRecordsAsync onasyncmethodend 4");
                        var record = o.DuckCast<IRecord>();
                        if (record == null)
                        {
                            Log.Warning("GetRecordsAsync onasyncmethodend 5");
                            continue; // should not happen
                        }

                        Log.Warning("GetRecordsAsync onasyncmethodend 6");
                        span.SetDataStreamsCheckpoint(dataStreamsManager, CheckpointKind.Consume, edgeTags, payloadSizeBytes: 0, timeInQueueMs: 0);
                        Log.Warning("GetRecordsAsync onasyncmethodend 7");
                    }
                }
            }

            Log.Warning("GetRecordsAsync onasyncmethodend 8");
            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
