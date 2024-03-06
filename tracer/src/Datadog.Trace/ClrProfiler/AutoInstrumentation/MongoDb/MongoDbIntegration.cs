// <copyright file="MongoDbIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Net;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb.BsonSerialization;
using Datadog.Trace.Configuration;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Logging;

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

        private const string DatabaseType = "mongodb";

        internal const IntegrationId IntegrationId = Configuration.IntegrationId.MongoDb;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(MongoDbIntegration));

        internal static Scope? CreateScope<TConnection>(object? wireProtocol, TConnection connection)
            where TConnection : IConnection
        {
            var tracer = Tracer.Instance;

            if (wireProtocol is null || !tracer.Settings.IsIntegrationEnabled(IntegrationId))
            {
                // integration disabled, don't create a scope, skip this trace
                return null;
            }

            if (GetActiveMongoDbScope(tracer) != null)
            {
                // There is already a parent MongoDb span (nested calls)
                return null;
            }

            var databaseName = GetDatabaseName(wireProtocol);

            if (!TryGetQueryDetails(wireProtocol, databaseName, out string? resourceName, out string? collectionName, out string? query))
            {
                // not a query we want to trace
                return null;
            }

            TryGetHostAndPort(connection, out var host, out var port);

            var operationName = tracer.CurrentTraceSettings.Schema.Database.GetOperationName(DatabaseType);
            var serviceName = tracer.CurrentTraceSettings.Schema.Database.GetServiceName(DatabaseType);
            var tags = tracer.CurrentTraceSettings.Schema.Database.CreateMongoDbTags();

            Scope? scope = null;

            try
            {
                scope = tracer.StartActiveInternal(operationName, serviceName: serviceName, tags: tags);
                var span = scope.Span;
                span.Type = SpanTypes.MongoDb;
                span.ResourceName = resourceName;
                tags.DbName = databaseName;
                tags.Query = query;
                tags.Collection = collectionName;
                tags.Host = host;
                tags.Port = port;

                tags.SetAnalyticsSampleRate(IntegrationId, tracer.Settings, enabledWithGlobalSetting: false);
                tracer.CurrentTraceSettings.Schema.RemapPeerService(tags);
                tracer.TracerManager.Telemetry.IntegrationGeneratedSpan(IntegrationId);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error creating or populating scope.");
            }

            return scope;

            static string? GetDatabaseName(object wireProtocol)
            {
                if (wireProtocol.TryDuckCast<IWireProtocolWithDatabaseNamespaceStruct>(out var protocolWithDatabaseNamespace)
                 && protocolWithDatabaseNamespace.DatabaseNamespace is not null
                 && protocolWithDatabaseNamespace.DatabaseNamespace.TryDuckCast<DatabaseNamespaceStruct>(out var databaseNamespace))
                {
                    return databaseNamespace.DatabaseName;
                }

                return null;
            }

            static void TryGetHostAndPort(TConnection connection, out string? host, out string? port)
            {
                host = null;
                port = null;

                if (connection.Instance is not null)
                {
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
                }
            }
        }

        private static bool TryGetQueryDetails(object wireProtocol, string? databaseName, out string? resourceName, out string? collectionName, out string? query)
        {
            collectionName = null;
            query = null;
            resourceName = null;

            if (wireProtocol.TryDuckCast<IWireProtocolWithCommandStruct>(out var protocolWithCommand)
             && protocolWithCommand.Command != null
             && protocolWithCommand.Command.TryDuckCast<IBsonDocumentProxy>(out var bsonDocument))
            {
                try
                {
                    // the name of the first element in the command BsonDocument will be the operation type (insert, delete, find, etc)
                    // and its value is the collection name
                    var firstElement = bsonDocument.GetElement(0);
                    var mongoOperationName = firstElement.Name;

                    if (mongoOperationName is "isMaster" or "hello")
                    {
                        return false;
                    }

                    resourceName = $"{mongoOperationName ?? "operation"} {databaseName ?? "database"}";
                    collectionName = firstElement.Value?.ToString();
                    query = BsonSerializationHelper.ToShortString(protocolWithCommand.Command);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Unable to access IWireProtocol.Command properties.");
                }
            }

            return true;
        }

        private static Scope? GetActiveMongoDbScope(Tracer tracer)
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
