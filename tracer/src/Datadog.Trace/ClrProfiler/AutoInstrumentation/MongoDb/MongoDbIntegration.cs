// <copyright file="MongoDbIntegration.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.Net;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MongoDb
{
    /// <summary>
    /// Tracing integration for MongoDB.Driver.Core.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class MongoDbIntegration
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

        internal static Scope CreateScope(object wireProtocol, object connection)
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

            try
            {
                if (wireProtocol.TryGetFieldValue("_databaseNamespace", out object databaseNamespace))
                {
                    databaseNamespace?.TryGetPropertyValue("DatabaseName", out databaseName);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to access DatabaseName property.");
            }

            try
            {
                if (connection != null && connection.TryGetPropertyValue("EndPoint", out object endpoint))
                {
                    if (endpoint is IPEndPoint ipEndPoint)
                    {
                        host = ipEndPoint.Address.ToString();
                        port = ipEndPoint.Port.ToString();
                    }
                    else if (endpoint is DnsEndPoint dnsEndPoint)
                    {
                        host = dnsEndPoint.Host;
                        port = dnsEndPoint.Port.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to access EndPoint properties.");
            }

            string operationName = null;
            string collectionName = null;
            string query = null;
            string resourceName = null;

            try
            {
                if (wireProtocol.TryGetFieldValue("_command", out object command) && command != null)
                {
                    // the name of the first element in the command BsonDocument will be the operation type (insert, delete, find, etc)
                    // and its value is the collection name
                    if (command.TryCallMethod("GetElement", 0, out object firstElement) && firstElement != null)
                    {
                        firstElement.TryGetPropertyValue("Name", out operationName);

                        if (firstElement.TryGetPropertyValue("Value", out object collectionNameObj) && collectionNameObj != null)
                        {
                            collectionName = collectionNameObj.ToString();
                        }
                    }

                    query = command.ToString();

                    resourceName = $"{operationName ?? "operation"} {databaseName ?? "database"}";
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to access IWireProtocol.Command properties.");
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

        private static Type[] GetGenericsFromWireProtocol(Type wireProtocolType)
        {
            var interfaces = wireProtocolType.GetInterfaces();
            Type typeWeInstrument = null;

            for (var i = 0; i < interfaces.Length; i++)
            {
                if (string.Equals($"{interfaces[i].Namespace}.{interfaces[i].Name}", IWireProtocolGeneric))
                {
                    typeWeInstrument = interfaces[i];
                    break;
                }
            }

            if (typeWeInstrument == null)
            {
                // We're likely in a non-generic context
                return null;
            }

            var genericArgs = typeWeInstrument.GetGenericArguments();

            if (genericArgs.Length == 0)
            {
                throw new ArgumentException($"Expected generics to determine TaskResult from {wireProtocolType.AssemblyQualifiedName}");
            }

            return genericArgs;
        }
    }
}
