// <copyright file="UpdateItemIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Shared;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.DynamoDb
{
    /// <summary>
    /// AWSSDK.DynamoDBv2 UpdateItem CallTarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.DynamoDBv2",
        TypeName = "Amazon.DynamoDBv2.AmazonDynamoDBClient",
        MethodName = "UpdateItem",
        ReturnTypeName = "Amazon.DynamoDBv2.Model.UpdateItemResponse",
        ParameterTypeNames = ["Amazon.DynamoDBv2.Model.UpdateItemRequest"],
        MinimumVersion = "3.0.0",
        MaximumVersion = "4.*.*",
        IntegrationName = AwsDynamoDbCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class UpdateItemIntegration
    {
        private const string Operation = "UpdateItem";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<UpdateItemIntegration>();

        internal static CallTargetState OnMethodBegin<TTarget, TUpdateItemRequest>(TTarget instance, TUpdateItemRequest request)
            where TUpdateItemRequest : IAmazonDynamoDbRequestWithKnownKeys, IDuckType
        {
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = AwsDynamoDbCommon.CreateScope(Tracer.Instance, Operation, out var tags);
            AwsDynamoDbCommon.TagTableNameAndResourceName(request.TableName, tags, scope);

            if (!Tracer.Instance.Settings.SpanPointersEnabled || scope is null)
            {
                return new CallTargetState(scope);
            }

            var tableName = request.TableName;
            try
            {
                var keys = request.Keys.DuckCast<IDynamoDbKeysObject>();
                // SpanPointers.AddDynamoDbSpanPointer(scope.Span, tableName, keys);
            }
            catch (Exception exception)
            {
                Log.Debug(exception, "Unable to add span pointer");
            }

            return new CallTargetState(scope);
        }

        internal static CallTargetReturn<TReturn> OnMethodEnd<TTarget, TReturn>(TTarget instance, TReturn returnValue, Exception? exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return new CallTargetReturn<TReturn>(returnValue);
        }
    }
}
