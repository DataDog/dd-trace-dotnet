// <copyright file="PutRecordsIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.Kinesis
{
    /// <summary>
    /// AWSSDK.Kinesis PutRecords CallTarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.Kinesis",
        TypeName = "Amazon.Kinesis.AmazonKinesisClient",
        MethodName = "PutRecords",
        ReturnTypeName = "Amazon.Kinesis.Model.PutRecordsResponse",
        ParameterTypeNames = new[] { "Amazon.Kinesis.Model.PutRecordsRequest" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsKinesisCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class PutRecordsIntegration
    {
        private const string Operation = "PutRecords";

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TPutRecordsRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the Kinesis operation</param>
        /// <returns>CallTarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TPutRecordsRequest>(TTarget instance, TPutRecordsRequest request)
            where TPutRecordsRequest : IPutRecordsRequest, IDuckType
        {
            if (request.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = AwsKinesisCommon.CreateScope(Tracer.Instance, Operation, SpanKinds.Producer, null, out AwsKinesisTags tags);
            tags.StreamName = request.StreamName;

            if (scope?.Span.Context != null)
            {
                ContextPropagation.InjectTraceIntoRecords<TPutRecordsRequest>(request, scope.Span.Context);
            }

            return new CallTargetState(scope);
        }
    }
}
