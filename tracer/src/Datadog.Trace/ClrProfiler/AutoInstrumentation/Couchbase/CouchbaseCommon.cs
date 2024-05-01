// <copyright file="CouchbaseCommon.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Datadog.Trace.ClrProfiler.CallTarget;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Tagging;
using Datadog.Trace.Util;

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
        internal const string MaxVersion2 = "2";
        internal const string MinVersion3 = "3";
        internal const string MaxVersion3 = "3";
        internal const string IntegrationName = nameof(Configuration.IntegrationId.Couchbase);

        private const string DatabaseType = "couchbase";
        private const IntegrationId IntegrationId = Configuration.IntegrationId.Couchbase;
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CouchbaseCommon));
        private static readonly ConditionalWeakTable<object, string> ClientSourceToNormalizedSeedNodesMap = new();

        internal static CallTargetState CommonOnMethodBeginV3<TOperation>(TOperation tOperation, IClusterNode clusterNode)
        {
            var tracer = Tracer.Instance;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) || tOperation == null)
            {
                // integration disabled, don't create a scope, skip this trace
                return CallTargetState.GetDefault();
            }

            var normalizedSeedNodes = GetNormalizedSeedNodesFromConnectionString(clusterNode.Context.ClusterOptions.ConnectionStringValue);
            var operation = tOperation.DuckCast<OperationStructV3>();

            var tags = tracer.CurrentTraceSettings.Schema.Database.CreateCouchbaseTags();
            tags.OperationCode = operation.OpCode.ToString();
            tags.Bucket = operation.BucketName;
            tags.Key = operation.Key;
            tags.SeedNodes = normalizedSeedNodes;
            tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);

            return CommonOnMethodBegin(tracer, tags);
        }

        internal static CallTargetState CommonOnMethodBegin<TOperation>(TOperation tOperation, string normalizedSeedNodes)
        {
            var tracer = Tracer.Instance;
            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId) || tOperation == null)
            {
                // integration disabled, don't create a scope, skip this trace
                return CallTargetState.GetDefault();
            }

            var operation = tOperation.DuckCast<OperationStruct>();

            var host = operation.CurrentHost?.Address?.ToString();
            var port = operation.CurrentHost?.Port.ToString();
            var code = operation.OperationCode.ToString();

            var tags = tracer.CurrentTraceSettings.Schema.Database.CreateCouchbaseTags();
            tags.OperationCode = code;
            tags.Key = operation.Key;
            tags.Host = host;
            tags.Port = port;
            tags.SeedNodes = normalizedSeedNodes;

            return CommonOnMethodBegin(tracer, tags);
        }

        private static CallTargetState CommonOnMethodBegin(Tracer tracer, CouchbaseTags tags)
        {
            try
            {
                string operationName = tracer.CurrentTraceSettings.Schema.Database.GetOperationName(DatabaseType);
                string serviceName = tracer.CurrentTraceSettings.Schema.Database.GetServiceName(DatabaseType);

                var scope = tracer.StartActiveInternal(operationName, serviceName: serviceName, tags: tags);
                scope.Span.Type = SpanTypes.Db;
                scope.Span.ResourceName = tags.OperationCode;
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);
                return new CallTargetState(scope);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
                return CallTargetState.GetDefault();
            }
        }

        internal static CallTargetReturn<TOperationResult> CommonOnMethodEndSync<TOperationResult>(TOperationResult tResult, Exception exception, in CallTargetState state)
        {
            return new CallTargetReturn<TOperationResult>(CommonOnMethodEnd(tResult, exception, in state));
        }

        internal static TOperationResult CommonOnMethodEnd<TOperationResult>(TOperationResult tResult, Exception exception, in CallTargetState state)
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

        internal static string GetNormalizedSeedNodesFromClientConfiguration(IClientConfiguration clientConfiguration)
        {
            if (ClientSourceToNormalizedSeedNodesMap.TryGetValue(clientConfiguration.Instance, out var normalizedSeedNodes))
            {
                return normalizedSeedNodes;
            }

            // Construct the normalized value from the list of hosts (each host will be {Host} or {Host}:{Port})
            var sb = StringBuilderCache.Acquire(0);
            IList<Uri> servers = clientConfiguration.Servers;

            for (int i = 0; i < servers.Count; i++)
            {
                if (i != 0)
                {
                    sb.Append(',');
                }

                var uri = servers[i];
                sb.Append($"{uri.Host}:{uri.Port}");
            }

            normalizedSeedNodes = StringBuilderCache.GetStringAndRelease(sb);

#if NETCOREAPP3_1_OR_GREATER
            ClientSourceToNormalizedSeedNodesMap.AddOrUpdate(clientConfiguration.Instance!, normalizedSeedNodes);
#else
            ClientSourceToNormalizedSeedNodesMap.GetValue(clientConfiguration.Instance!, x => normalizedSeedNodes);
#endif

            return normalizedSeedNodes;
        }

        internal static string GetNormalizedSeedNodesFromConnectionString(IConnectionString connectionStringValue)
        {
            if (ClientSourceToNormalizedSeedNodesMap.TryGetValue(connectionStringValue.Instance, out var normalizedSeedNodes))
            {
                return normalizedSeedNodes;
            }

            // Construct the normalized value from the list of hosts (each host will be {Host} or {Host}:{Port})
            var sb = StringBuilderCache.Acquire(0);
            var firstIteration = true;
            foreach (var hostObj in connectionStringValue.Hosts)
            {
                if (!firstIteration)
                {
                    sb.Append(',');
                }

                sb.Append(hostObj.ToString());
                firstIteration = false;
            }

            normalizedSeedNodes = StringBuilderCache.GetStringAndRelease(sb);

#if NETCOREAPP3_1_OR_GREATER
            ClientSourceToNormalizedSeedNodesMap.AddOrUpdate(connectionStringValue.Instance!, normalizedSeedNodes);
#else
            ClientSourceToNormalizedSeedNodesMap.GetValue(connectionStringValue.Instance!, x => normalizedSeedNodes);
#endif

            return normalizedSeedNodes;
        }
    }
}
