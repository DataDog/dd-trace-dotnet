// <copyright file="StartExecutionAsyncIntegration.cs" company="Datadog">
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
    /// AWSSDK.StepFunctions StartExecutionAsync CallTarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.StepFunctions",
        TypeName = "Amazon.StepFunctions.AmazonStepFunctionsClient",
        MethodName = "StartExecutionAsync",
        ReturnTypeName = "Amazon.StepFunctions.Model.StartExecutionResponse",
        ParameterTypeNames = new[] { "Amazon.StepFunctions.Model.StartExecutionRequest", ClrNames.CancellationToken },
        MinimumVersion = "3.3.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsStepFunctionsCommon.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class StartExecutionAsyncIntegration
    {
        private const string Operation = "StartExecutionAsync";

        internal interface IStartExecutionRequest : IAwsStepFunctionsRequestWithStateMachineArn, IContainsInput
        {
        }

        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TStartExecutionRequest">Type of the request object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="request">The request for the Step Functions operation</param>
        /// <param name="cancellationToken">CancellationToken value</param>
        /// <returns>CallTarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TStartExecutionRequest>(TTarget instance, TStartExecutionRequest request, CancellationToken cancellationToken)
            where TStartExecutionRequest : IStartExecutionRequest, IDuckType
        {
            if (request is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = AwsStepFunctionsCommon.CreateScope(Tracer.Instance, Operation, SpanKinds.Producer, out var tags);
            if (tags is not null && request.StateMachineArn is not null)
            {
                tags.StateMachineName = AwsStepFunctionsCommon.GetStateMachineName(request.StateMachineArn);
            }

            if (request.Input is not null && scope?.Span.Context is { } spanContext)
            {
                var context = new PropagationContext(spanContext, Baggage.Current);
                ContextPropagation.InjectContextIntoInput<TTarget, TStartExecutionRequest>(request, context);
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
