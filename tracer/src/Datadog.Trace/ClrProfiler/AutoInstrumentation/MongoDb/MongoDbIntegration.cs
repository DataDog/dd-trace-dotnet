// <copyright file="MongoDbIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Net;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    /// <summary>
    /// Tracing integration for MongoDB.Driver.Core.
    /// </summary>
    internal static class MongoDbIntegration
    {
        internal const string IntegrationName = nameof(Configuration.IntegrationId.MongoDb);

        internal const string Major2 = "2";
        internal const string Major2Minor1 = "2.1";
        internal const string Major2Minor2 = "2.2"; // Synchronous methods added in 2.2
        internal const string MongoDbClientAssembly = "MongoDB.Driver.Core";

        private const string OperationName = "mongodb.query";
        private const string ServiceName = "mongodb";

        private const string IWireProtocolGeneric = "MongoDB.Driver.Core.WireProtocol.IWireProtocol`1";

        internal const IntegrationId IntegrationId = Configuration.IntegrationId.MongoDb;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MongoDbIntegration));

        internal static Scope CreateScope<TConnection>(object wireProtocol, TConnection connection)
            where TConnection : IConnection
        {
            var tracer = Tracer.Instance;

            if (!tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            if (GetActiveMongoDbScope(tracer) != null)
            {
                // There is already a parent MongoDb span (nested calls)
                return null;
            }

            string databaseName = null;
            string host = null;
            string port = null;

            if (wireProtocol.TryDuckCast<IWireProtocolWithDatabaseNamespaceStruct>(out var protocolWithDatabaseNamespace))
            {
                databaseName = protocolWithDatabaseNamespace.DatabaseNamespace.DatabaseName;
            }

            if (connection.EndPoint is IPEndPoint ipEndPoint)
            {
                host = ipEndPoint.Address.ToString();
                port = ipEndPoint.Port.ToString();
            }
            else if (connection.EndPoint is DnsEndPoint dnsEndPoint)
            {
                host = dnsEndPoint.Host;
                port = dnsEndPoint.Port.ToString();
            }

            string collectionName = null;
            string query = null;
            string resourceName = null;

            if (wireProtocol.TryDuckCast<IWireProtocolWithCommandStruct>(out var protocolWithCommand)
                && protocolWithCommand.Command != null)
            {
                // the name of the first element in the command BsonDocument will be the operation type (insert, delete, find, etc)
                // and its value is the collection name
                var firstElement = protocolWithCommand.Command.GetElement(0);
                string operationName = firstElement.Name;

                collectionName = firstElement.Value?.ToString();
                query = protocolWithCommand.Command.ToString();
                resourceName = $"{operationName ?? "operation"} {databaseName ?? "database"}";
            }

            string serviceName = tracer.Settings.GetServiceName(tracer, ServiceName);

            Scope scope = null;

            try
            {
                var tags = new MongoDbTags();
                scope = tracer.StartActiveInternal(OperationName, serviceName: serviceName, tags: tags);
                var span = scope.Span;
                span.Type = SpanTypes.MongoDb;
                span.ResourceName = resourceName;
                tags.DbName = databaseName;
                tags.Query = query;
                tags.Collection = collectionName;
                tags.Host = host;
                tags.Port = port;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;
        }

        private static Scope GetActiveMongoDbScope(Tracer tracer)
        {
            var scope = tracer.InternalActiveScope;

            var parent = scope?.Span;

            if (parent != null &&
                parent.Type == SpanTypes.MongoDb &&
                parent.GetTag(Tags.InstrumentationName) != null)
            {
                return scope;
            }

            return null;
        }
    }
}
