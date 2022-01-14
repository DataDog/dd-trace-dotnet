// <copyright file="Transport_RequestAsync_Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Threading;
using Datadog.Trace.ClrProfiler.CallTarget;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V7
{
    /// <summary>
    /// Elasticsearch.Net.RequestPipeline.CallElasticsearchAsync&lt;T&gt; calltarget instrumentation
    /// </summary>
    [InstrumentMethod(
        AssemblyName = ElasticsearchV7Constants.ElasticsearchAssemblyName,
        TypeName = ElasticsearchV7Constants.TransportTypeName,
        MethodName = "RequestAsync",
        ReturnTypeName = ClrNames.GenericParameterTask,
        ParameterTypeNames = new[] { "Elasticsearch.Net.HttpMethod", ClrNames.String, ClrNames.CancellationToken, "Elasticsearch.Net.PostData", "Elasticsearch.Net.IRequestParameters" },
        MinimumVersion = ElasticsearchV7Constants.Version7,
        MaximumVersion = ElasticsearchV7Constants.Version7,
        IntegrationName = ElasticsearchV7Constants.IntegrationName)]
    // ReSharper disable once InconsistentNaming
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public class Transport_RequestAsync_Integration
    {
        /// <summary>
        /// OnMethodBegin callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="THttpMethod">The type of the HttpMethod parameter</typeparam>
        /// <typeparam name="TPostData">The type of the PostData parameter</typeparam>
        /// <typeparam name="TRequestParameters">The type of the request parameters</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="method">The HTTP method of the request</param>
        /// <param name="path">The path of the request</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <param name="postData">The payload of the request</param>
        /// <param name="requestParameters">The parameters of the request</param>
        /// <returns>Calltarget state value</returns>
        internal static CallTargetState OnMethodBegin<TTarget, THttpMethod, TPostData, TRequestParameters>(TTarget instance, THttpMethod method, string path, CancellationToken cancellationToken, TPostData postData, TRequestParameters requestParameters)
        {
            var scope = ElasticsearchNetCommon.CreateScope(Tracer.Instance, ElasticsearchV7Constants.IntegrationId, path, method.ToString(), requestParameters, out var tags);

            return new CallTargetState(scope, tags);
        }

        /// <summary>
        /// OnAsyncMethodEnd callback
        /// </summary>
        /// <typeparam name="TTarget">Type of the target</typeparam>
        /// <typeparam name="TResponse">Type of the response</typeparam>
        /// <param name="instance">Instance value, aka `this` of the instrumented method.</param>
        /// <param name="response">The response</param>
        /// <param name="exception">Exception instance in case the original code threw an exception.</param>
        /// <param name="state">Calltarget state value</param>
        /// <returns>A response value, in an async scenario will be T of Task of T</returns>
        internal static TResponse OnAsyncMethodEnd<TTarget, TResponse>(TTarget instance, TResponse response, Exception exception, in CallTargetState state)
            where TResponse : IElasticsearchResponse
        {
            var uri = response?.ApiCall?.Uri?.ToString();

            if (uri != null)
            {
                var tags = (ElasticsearchTags)state.State;

                if (tags != null)
                {
                    tags.Url = uri;
                }
            }

            state.Scope.DisposeWithException(exception);

            return response;
        }
    }
}
