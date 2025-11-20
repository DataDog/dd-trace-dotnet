// <copyright file="BatchWriteItemAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.DynamoDb
{
    /// <summary>
    /// AWSSDK.DynamoDBv2 BatchWriteItemAsync CallTarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.DynamoDBv2",
        TypeName = "Amazon.DynamoDBv2.AmazonDynamoDBClient",
        MethodName = "BatchWriteItemAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.DynamoDBv2.Model.BatchWriteItemResponse]",
        ParameterTypeNames = ["Amazon.DynamoDBv2.Model.BatchWriteItemRequest", ClrNames.CancellationToken],
        MinimumVersion = "3.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = AwsDynamoDbCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class BatchWriteItemAsyncIntegration
    {
        private const string Operation = "BatchWriteItem";

        internal static CallTargetState OnMethodBegin<TTarget, TBatchWriteItemRequest>(TTarget instance, TBatchWriteItemRequest request, CancellationToken cancellationToken)
            where TBatchWriteItemRequest : IBatchRequest, IDuckType
        {
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = AwsDynamoDbCommon.CreateScope(Tracer.Instance, Operation, out var tags);
            AwsDynamoDbCommon.TagBatchRequest(request, tags, scope);

            return new CallTargetState(scope);
        }

        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception? exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
