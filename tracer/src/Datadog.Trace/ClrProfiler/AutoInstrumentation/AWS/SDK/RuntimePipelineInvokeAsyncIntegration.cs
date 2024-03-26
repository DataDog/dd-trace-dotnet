// <copyright file="RuntimePipelineInvokeAsyncIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.ComponentModel;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.AWS.SDK
{
    /// <summary>
    /// AWSSDK.Core InvokeAsync calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = "AWSSDK.Core",
        TypeName = "Amazon.Runtime.Internal.RuntimePipeline",
        MethodName = "InvokeAsync",
        ReturnTypeName = ClrNames.GenericTaskWithGenericClassParameter,
        ParameterTypeNames = new[] { "Amazon.Runtime.IExecutionContext" },
        MinimumVersion = "3.0.0",
        MaximumVersion = "3.*.*",
        IntegrationName = AwsConstants.IntegrationName)]
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class RuntimePipelineInvokeAsyncIntegration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TExecutionContext">Type of the execution context object</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method</param>
        /// <param name="executionContext">The execution context for the AWS SDK operation</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, TExecutionContext>(TTarget instance, TExecutionContext executionContext)
            where TExecutionContext : IExecutionContext, IDuckType
        {
            if (executionContext.Instance is null)
            {
                return CallTargetState.GetDefault();
            }

            var scope = Tracer.Instance.InternalActiveScope;
            if (scope?.Span.Tags is AwsSdkTags tags)
            {
                tags.Region = executionContext.RequestContext?.ClientConfig?.RegionEndpoint?.SystemName;
            }

            return new CallTargetState(scope, state: executionContext);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response, in an async scenario will be T of Task of T</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">Response instance</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
            where TResponse : IAmazonWebServiceResponse
        {
            if (state.Scope?.Span.Tags is AwsSdkTags tags)
            {
                if (state.State is IExecutionContext { RequestContext.Request: { } request })
                {
                    var uri = request.Endpoint;
                    var absolutePath = uri?.AbsolutePath;
                    var path = request.ResourcePath switch
                    {
                        null => absolutePath,
                        string resourcePath when absolutePath == "/" => resourcePath,
                        string resourcePath => UriHelpers.Combine(absolutePath, resourcePath),
                    };

                    // The request object is populated later by the Marshaller,
                    // so we wait until the method end callback to read it
                    tags.HttpMethod = request.HttpMethod?.ToUpperInvariant();
                    tags.HttpUrl = $"{uri?.Scheme}{Uri.SchemeDelimiter}{uri?.Authority}{path}";
                }

                if (response.Instance is not null)
                {
                    tags.RequestId = response.ResponseMetadata?.RequestId;
                    state.Scope.Span.SetHttpStatusCode((int)response.HttpStatusCode, false, Tracer.Instance.Settings);
                }
            }

            // Do not dispose the scope (if present) when exiting.
            // It will be closed by the higher level client instrumentation
            return response;
        }
    }
}
