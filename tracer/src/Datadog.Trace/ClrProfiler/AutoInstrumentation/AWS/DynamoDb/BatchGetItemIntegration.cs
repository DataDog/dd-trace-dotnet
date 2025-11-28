// <copyright file="BatchGetItemIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.DynamoDb
{
    /// <summary>
    /// AWSSDK.DynamoDBv2 BatchGetItem CallTarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.DynamoDBv2",
        TypeName = "Amazon.DynamoDBv2.AmazonDynamoDBClient",
        MethodName = "BatchGetItem",
        ReturnTypeName = "Amazon.DynamoDBv2.Model.BatchGetItemResponse",
        ParameterTypeNames = ["Amazon.DynamoDBv2.Model.BatchGetItemRequest"],
        MinimumVersion = "3.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = AwsDynamoDbCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class BatchGetItemIntegration
    {
        private const string Operation = "BatchGetItem";

        internal static CallTargetState OnMethodBegin<TTarget, TBatchGetItemRequest>(TTarget instance, TBatchGetItemRequest request)
            where TBatchGetItemRequest : IBatchRequest, IDuckType
        {
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = AwsDynamoDbCommon.CreateScope(Tracer.Instance, Operation, out var tags);
            AwsDynamoDbCommon.TagBatchRequest(request, tags, scope);

            return new CallTargetState(scope);
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
