// <copyright file="ElasticsearchNet5Integration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Elasticsearch.V5;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Traces an Elasticsearch pipeline
    /// </summary>
    public static class ElasticsearchNet5Integration
    {
        private const string Version5 = "5";
        private const string ElasticsearchAssembly = "Elasticsearch.Net";
        private const string RequestPipelineInterfaceTypeName = "Elasticsearch.Net.IRequestPipeline";

        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(nameof(IntegrationIds.ElasticsearchNet5));
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(ElasticsearchNet5Integration));
        private static readonly Type ElasticsearchResponseType = Type.GetType("Elasticsearch.Net.ElasticsearchResponse`1, Elasticsearch.Net", throwOnError: false);

        /// <summary>
        /// Traces a synchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="TResponse">The type of the response</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original result</returns>
        [InterceptMethod(
            CallerAssembly = ElasticsearchAssembly,
            TargetAssembly = ElasticsearchAssembly,
            TargetType = RequestPipelineInterfaceTypeName,
            TargetSignatureTypes = new[] { "Elasticsearch.Net.ElasticsearchResponse`1<T>", "Elasticsearch.Net.RequestData" },
            TargetMinimumVersion = Version5,
            TargetMaximumVersion = Version5)]
        public static object CallElasticsearch<TResponse>(
            object pipeline,
            object requestData,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            const string methodName = nameof(CallElasticsearch);
            Func<object, object, object> callElasticSearch;
            var pipelineType = pipeline.GetType();
            var genericArgument = typeof(TResponse);

            try
            {
                callElasticSearch =
                    MethodBuilder<Func<object, object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(pipelineType)
                       .WithMethodGenerics(genericArgument)
                       .WithNamespaceAndNameFilters("Elasticsearch.Net.ElasticsearchResponse`1", "Elasticsearch.Net.RequestData")
                       .WithParameters(requestData)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: RequestPipelineInterfaceTypeName,
                    methodName: methodName,
                    instanceType: pipeline.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = ElasticsearchNetCommon.CreateScope(Tracer.Instance, IntegrationId, pipeline.DuckCast<RequestPipelineStruct>(), new RequestDataV5(requestData)))
            {
                try
                {
                    return callElasticSearch(pipeline, requestData);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Traces an asynchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="TResponse">Type type of the response</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <param name="boxedCancellationToken">A cancellation token</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The original result</returns>
        [InterceptMethod(
            CallerAssembly = ElasticsearchAssembly,
            TargetAssembly = ElasticsearchAssembly,
            TargetType = RequestPipelineInterfaceTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<Elasticsearch.Net.ElasticsearchResponse`1<T>>", "Elasticsearch.Net.RequestData", ClrNames.CancellationToken },
            TargetMinimumVersion = Version5,
            TargetMaximumVersion = Version5)]
        public static object CallElasticsearchAsync<TResponse>(
            object pipeline,
            object requestData,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            var cancellationToken = (CancellationToken)boxedCancellationToken;

            var genericArgument = typeof(TResponse);
            var genericResponseType = ElasticsearchResponseType.MakeGenericType(genericArgument);

            Func<object, object, CancellationToken, object> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<object, object, CancellationToken, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, nameof(CallElasticsearchAsync))
                       .WithConcreteType(pipeline.GetType())
                       .WithMethodGenerics(genericArgument)
                       .WithParameters(requestData, cancellationToken)
                       .WithNamespaceAndNameFilters("System.Threading.Tasks.Task`1<Elasticsearch.Net.ElasticsearchResponse`1>", "Elasticsearch.Net.RequestData", ClrNames.CancellationToken)
                       .ForceMethodDefinitionResolution()
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: RequestPipelineInterfaceTypeName,
                    methodName: nameof(CallElasticsearchAsync),
                    instanceType: pipeline.GetType().AssemblyQualifiedName);
                throw;
            }

            return AsyncHelper.InvokeGenericTaskDelegate(
                owningType: ElasticsearchNetCommon.RequestPipelineType,
                taskResultType: genericResponseType,
                nameOfIntegrationMethod: nameof(CallElasticsearchAsyncInternal),
                integrationType: typeof(ElasticsearchNet5Integration),
                pipeline,
                requestData,
                cancellationToken,
                instrumentedMethod);
        }

        /// <summary>
        /// Traces an asynchronous call to Elasticsearch.
        /// </summary>
        /// <typeparam name="T">Type type of the Task</typeparam>
        /// <param name="pipeline">The pipeline for the original method</param>
        /// <param name="requestData">The request data</param>
        /// <param name="cancellationToken">A cancellation token</param>
        /// <param name="originalMethod">A delegate for the method we are instrumenting</param>
        /// <returns>The original result</returns>
        private static async Task<T> CallElasticsearchAsyncInternal<T>(
            object pipeline,
            object requestData,
            CancellationToken cancellationToken,
            Func<object, object, CancellationToken, object> originalMethod)
        {
            using (var scope = ElasticsearchNetCommon.CreateScope(Tracer.Instance, IntegrationId, pipeline.DuckCast<RequestPipelineStruct>(), new RequestDataV5(requestData)))
            {
                try
                {
                    var task = (Task<T>)originalMethod(pipeline, requestData, cancellationToken);
                    return await task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }
    }
}
