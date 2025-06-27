// <copyright file="PutRecordsAsyncV3_7Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// AWSSDK.Kinesis PutRecordsAsync CallTarget instrumentation for SDK versions 3.7.0+
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.Kinesis",
        TypeName = "Amazon.Kinesis.AmazonKinesisClient",
        MethodName = "PutRecordsAsync",
        ReturnTypeName = "System.Threading.Tasks.Task`1[Amazon.Kinesis.Model.PutRecordsResponse]",
        ParameterTypeNames = new[] { "Amazon.Kinesis.Model.PutRecordsRequest", ClrNames.CancellationToken },
        MinimumVersion = "3.7.0",
        MaximumVersion = "4.*.*",
        IntegrationName = AwsKinesisCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class PutRecordsAsyncV3_7Integration
    {
        private const string Operation = "PutRecords";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TPutRecordsRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the Kinesis operation</param>
        /// <param name="cancellationToken">CancellationToken value</param>
        /// <returns>CallTarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TPutRecordsRequest>(TTarget instance, TPutRecordsRequest request, CancellationToken cancellationToken)
            where TPutRecordsRequest : IPutRecordsRequestV3_7, IDuckType
        {
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = AwsKinesisCommon.CreateScope(Tracer.Instance, Operation, SpanKinds.Producer, null, out var tags);
            var streamName = AwsKinesisCommon.GetStreamName(request);
            if (tags is not null)
            {
                tags.StreamName = streamName;
            }

            ContextPropagation.InjectTraceIntoRecords(request, scope, streamName);

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
