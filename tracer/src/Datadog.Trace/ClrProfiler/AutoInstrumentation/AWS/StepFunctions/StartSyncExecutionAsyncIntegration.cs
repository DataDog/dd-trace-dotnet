// <copyright file="StartSyncExecutionAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Propagators;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.StepFunctions
{
    /// <summary>
    /// AWSSDK.StepFunctions StartSyncExecutionAsync CallTarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.StepFunctions",
        TypeName = "Amazon.StepFunctions.AmazonStepFunctionsClient",
        MethodName = "StartSyncExecutionAsync",
        ReturnTypeName = "Amazon.StepFunctions.Model.StartSyncExecutionResponse",
        ParameterTypeNames = new[] { "Amazon.StepFunctions.Model.StartSyncExecutionRequest", ClrNames.CancellationToken },
        MinimumVersion = "3.3.0",
        MaximumVersion = "4.*.*",
        IntegrationName = AwsStepFunctionsCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class StartSyncExecutionAsyncIntegration
    {
        private const string Operation = "StartSyncExecutionAsync";

        internal interface IStartExecutionRequest : IAwsStepFunctionsRequestWithStateMachineArn, IContainsInput
        {
        }

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TStartSyncExecutionRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the Step Functions operation</param>
        /// <param name="cancellationToken">CancellationToken value</param>
        /// <returns>CallTarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TStartSyncExecutionRequest>(TTarget instance, TStartSyncExecutionRequest request, CancellationToken cancellationToken)
            where TStartSyncExecutionRequest : IStartExecutionRequest, IDuckType
        {
            if (request?.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var tracer = Tracer.Instance;
            var scope = AwsStepFunctionsCommon.CreateScope(tracer, Operation, SpanKinds.Producer, out var tags);
            if (tags is not null && request.StateMachineArn is not null)
            {
                tags.StateMachineName = AwsStepFunctionsCommon.GetStateMachineName(request.StateMachineArn);
            }

            if (request.Input is not null && scope?.Span.Context is { } spanContext)
            {
                var context = new PropagationContext(spanContext, Baggage.Current);
                ContextPropagation.InjectContextIntoInput<TTarget, TStartSyncExecutionRequest>(tracer, request, context);
            }

            return new CallTargetState(scope, state: request);
        }

        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
        {
            state.Scope.DisposeWithException(exception);
            return response;
        }
    }
}
