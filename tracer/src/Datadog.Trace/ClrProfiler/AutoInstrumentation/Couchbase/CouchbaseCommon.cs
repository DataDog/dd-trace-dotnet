// <copyright file="CouchbaseCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Couchbase
{
    internal static class CouchbaseCommon
    {
        internal const string CouchbaseClientAssemblyName = "Couchbase.NetClient";
        internal const string CouchbaseOperationTypeName = "Couchbase.IO.Operations.IOperation";
        internal const string CouchbaseConnectionTypeName = "Couchbase.IO.IConnection";
        internal const string CouchbaseOperationV3TypeName = "Couchbase.Core.IO.Operations.IOperation";
        internal const string CouchbaseConnectionV3TypeName = "Couchbase.Core.IO.Connections.IConnection";
        internal const string CouchbaseGenericOperationTypeName = "Couchbase.IO.Operations.IOperation`1[!!0]";
        internal const string CouchbaseOperationResultTypeName = "Couchbase.IOperationResult<T>";
        internal const string MinVersion2 = "2.2.8";
        internal const string MaxVersion2 = "2.7.25";
        internal const string MinVersion3 = "3";
        internal const string MaxVersion3 = "3";
        internal const string IntegrationName = nameof(IntegrationIds.Couchbase);

        private const string OperationName = "couchbase.query";
        private const string ServiceName = "couchbase";
        private static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CouchbaseCommon));

        internal static CallTargetState CommonOnMethodBeginV3<TOperation>(TOperation tOperation)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId) || tOperation == null)
            {
                // integration disabled, don't create a scope, skip this trace
                return CallTargetState.GetDefault();
            }

            var operation = tOperation.DuckCast<OperationStructV3>();

            var tags = new CouchbaseTags()
            {
                OperationCode = operation.OpCode.ToString(),
                Bucket = operation.BucketName,
                Key = operation.Key,
            };

            return CommonOnMethodBegin(tags);
        }

        internal static CallTargetState CommonOnMethodBegin<TOperation>(TOperation tOperation)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId) || tOperation == null)
            {
                // integration disabled, don't create a scope, skip this trace
                return CallTargetState.GetDefault();
            }

            var operation = tOperation.DuckCast<OperationStruct>();

            var host = operation.CurrentHost?.Address?.ToString();
            var port = operation.CurrentHost?.Port.ToString();
            var code = operation.OperationCode.ToString();

            var tags = new CouchbaseTags()
            {
                OperationCode = code,
                Bucket = operation.BucketName,
                Key = operation.Key,
                Host = host,
                Port = port
            };

            return CommonOnMethodBegin(tags);
        }

        private static CallTargetState CommonOnMethodBegin(CouchbaseTags tags)
        {
            try
            {
                var tracer = Tracer.Instance;
                var serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);
                var scope = tracer.StartActiveWithTags(OperationName, serviceName: serviceName, tags: tags);
                scope.Span.Type = SpanTypes.Db;
                scope.Span.ResourceName = $"{tags.OperationCode}";
                return new CallTargetState(scope);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
                return CallTargetState.GetDefault();
            }
        }

        internal static CallTargetReturn<TOperationResult> CommonOnMethodEndSync<TOperationResult>(TOperationResult tResult, Exception exception, CallTargetState state)
        {
            return new CallTargetReturn<TOperationResult>(CommonOnMethodEnd(tResult, exception, state));
        }

        internal static TOperationResult CommonOnMethodEnd<TOperationResult>(TOperationResult tResult, Exception exception, CallTargetState state)
        {
            if (state.Scope == null || tResult == null)
            {
                state.Scope?.DisposeWithException(exception);
                return tResult;
            }

            var result = tResult.DuckCast<ResultStruct>();
            var span = state.Scope.Span;

            if (!result.Success)
            {
                span.Error = true;
                if (!string.IsNullOrEmpty(result.Message))
                {
                    span.SetTag(Tags.ErrorMsg, result.Message);
                }
            }

            state.Scope.DisposeWithException(exception ?? result.Exception);
            return tResult;
        }
    }
}
