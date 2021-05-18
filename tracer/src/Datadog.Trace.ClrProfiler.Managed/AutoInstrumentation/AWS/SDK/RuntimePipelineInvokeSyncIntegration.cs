// <copyright file="RuntimePipelineInvokeSyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// AWSSDK.Core InvokeSync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.Core",
        TypeName = "Amazon.Runtime.Internal.RuntimePipeline",
        MethodName = "InvokeSync",
        ReturnTypeName = "Amazon.Runtime.IResponseContext",
        ParameterTypeNames = new[] { "Amazon.Runtime.IExecutionContext" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsConstants.IntegrationName)]
    public class RuntimePipelineInvokeSyncIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TExecutionContext">Type of the execution context object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="executionContext">The execution context for the AWS SDK operation</param>
        /// <returns>Calltarget state value</returns>
        public static CallTargetState OnMethodBegin<TTarget, TExecutionContext>(TTarget instance, TExecutionContext executionContext)
            where TExecutionContext : IExecutionContext, IDuckType
        {
            if (executionContext.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = Tracer.Instance.ActiveScope;
            if (scope?.Span.Tags is AwsSdkTags tags)
            {
                tags.Region = executionContext.RequestContext.ClientConfig.RegionEndpoint?.SystemName;
            }

            return new CallTargetState(scope, state: executionContext);
        }

        /// <summary>
        /// OnMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponseContext">Type of the response contex</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="responseContext">Response context instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        public static CallTargetReturn<TResponseContext> OnMethodEnd<TTarget, TResponseContext>(TTarget instance, TResponseContext responseContext, Exception exception, CallTargetState state)
            where TResponseContext : IResponseContext
        {
            if (state.Scope?.Span.Tags is AwsSdkTags tags)
            {
                if (state.State is IExecutionContext executionContext)
                {
                    // The request object is populated later by the Marshaller,
                    // so we wait until the method end callback to read it
                    tags.HttpMethod = executionContext.RequestContext.Request.HttpMethod.ToUpperInvariant();
                }

                tags.RequestId = responseContext.Response.ResponseMetadata.RequestId;
                state.Scope.Span.SetHttpStatusCode((int)responseContext.Response.HttpStatusCode, isServer: false);
            }

            // Do not dispose the scope (if present) when exiting.
            // It will be closed by the higher level client instrumentation
            return new CallTargetReturn<TResponseContext>(responseContext);
        }
    }
}
