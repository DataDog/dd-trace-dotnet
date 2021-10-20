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
        internal const string CouchbaseGenericOperationTypeName = "Couchbase.IO.Operations.IOperation`1[!!0]";
        internal const string CouchbaseOperationResultTypeName = "Couchbase.IOperationResult<T>";
        internal const string MinVersion = "2.2.8";
        internal const string MaxVersion = "2.7.25";

        internal const string OperationName = "couchbase.query";
        internal const string ServiceName = "couchbase";

        internal const string IntegrationName = nameof(IntegrationIds.Couchbase);
        internal static readonly IntegrationInfo IntegrationId = IntegrationRegistry.GetIntegrationInfo(IntegrationName);
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CouchbaseCommon));

        internal static CallTargetState CommonOnMethodBegin<TOperation>(TOperation tOperation)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return CallTargetState.GetDefault();
            }

            Scope scope = null;

            try
            {
                if (tOperation != null && tOperation.TryDuckCast(out IOperation operation))
                {
                    var host = operation.CurrentHost?.Address.ToString();
                    var port = operation.CurrentHost?.Port.ToString();
                    var code = operation.OperationCode.ToString();

                    var tags = new CouchbaseTags()
                    {
                        OperationCode = code,
                        Key = operation.Key,
                        Host = host,
                        Port = port
                    };

                    scope = Tracer.Instance.StartActiveWithTags(OperationName, serviceName: ServiceName, tags: tags);
                    scope.Span.Type = SpanTypes.Couchbase;
                    scope.Span.ResourceName = $"{code} {operation.Key}";
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
                return CallTargetState.GetDefault();
            }

            if (scope == null)
            {
                return CallTargetState.GetDefault();
            }

            return new CallTargetState(scope);
        }

        internal static CallTargetReturn<TOperationResult> CommonOnMethodEndSync<TOperationResult>(TOperationResult tResult, Exception exception, CallTargetState state)
        {
            return new CallTargetReturn<TOperationResult>(CommonOnMethodEnd(tResult, exception, state));
        }

        internal static TOperationResult CommonOnMethodEnd<TOperationResult>(TOperationResult tResult, Exception exception, CallTargetState state)
        {
            if (state.Scope == null)
            {
                return tResult;
            }

            if (tResult != null && tResult.TryDuckCast(out IResult result))
            {
                var span = state.Scope.Span;
                if (!result.Success)
                {
                    span.Error = true;
                    if (!string.IsNullOrEmpty(result.Message))
                    {
                        span.SetTag(Tags.ErrorMsg, result.Message);
                    }

                    // TBC if this a pattern we usually follow
                    if (result.Exception != null && exception == null)
                    {
                        state.Scope?.DisposeWithException(result.Exception);
                    }
                }
            }
            else
            {
                Log.Information("Duck Cast or interface resp");
                }

            state.Scope?.DisposeWithException(exception);
            return tResult;
        }
    }
}
