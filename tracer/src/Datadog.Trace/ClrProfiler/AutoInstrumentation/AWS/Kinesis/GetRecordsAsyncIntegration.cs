// <copyright file="GetRecordsAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

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
        MaximumVersion = "4.*.*",
        IntegrationName = AwsKinesisCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class GetRecordsAsyncIntegration
    {
        private const string Operation = "GetRecords";

        internal static CallTargetState OnMethodBegin<TTarget, TGetRecordsRequest>(TTarget instance, TGetRecordsRequest request, CancellationToken cancellationToken)
            where TGetRecordsRequest : IGetRecordsRequest, IDuckType
        {
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = AwsKinesisCommon.CreateScope(Tracer.Instance, Operation, SpanKinds.Consumer, null, out var tags);

            string? streamName = AwsKinesisCommon.StreamNameFromARN(request.StreamARN);
            if (tags is not null && streamName is not null)
            {
                tags.StreamName = streamName;
            }

            return new CallTargetState(scope, streamName);
        }

        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception? exception, in CallTargetState state)
            where TResponse : IGetRecordsResponse, IDuckType
        {
            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
