using System;
using System.Linq;
using System.Net;
using System.Threading;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracing integration for MongoDB.Driver.Core.
    /// </summary>
    public static class MongoDbIntegration
    {
        internal const string OperationName = "mongodb.query";
        internal const string ServiceName = "mongodb";

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="wireProtocol">The IWireProtocol`1 instance we are replacing.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="cancellationTokenSource">A cancellation token source.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = "MongoDB.Driver.Core",
            TargetType = "MongoDB.Driver.Core.WireProtocol.IWireProtocol`1")]
        public static object Execute(object wireProtocol, object connection, object cancellationTokenSource)
        {
            // TResult MongoDB.Driver.Core.WireProtocol.IWireProtocol<TResult>.Execute(IConnection connection, CancellationToken cancellationToken)
            if (wireProtocol == null) { throw new ArgumentNullException(nameof(wireProtocol)); }

            string databaseName = null;
            string host = null;
            string port = null;

            if (wireProtocol.TryGetFieldValue("_databaseNamespace", out object databaseNamespace))
            {
                databaseNamespace?.TryGetPropertyValue("DatabaseName", out databaseName);
            }

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

            string operationName = null;
            string collectionName = null;
            string query = null;
            string resourceName = null;

            if (wireProtocol.TryGetFieldValue("_command", out object command) && command != null)
            {
                try
                {
                    // the name of the first element in the command BsonDocument will be the operation type (insert, delete, find, etc)
                    // and its value is the collection name
                    if (command.TryCallMethod("GetElement", 0, out object firstElement) && firstElement != null)
                    {
                        firstElement.TryGetPropertyValue("Name", out operationName);

                        if (firstElement.TryGetPropertyValue("Value", out object collectionNameObj) && collectionNameObj != null)
                        {
                            collectionName = (string)collectionNameObj;
                        }
                    }

                    // get the "query" element from the command BsonDocument, if it exists
                    if (command.TryCallMethod("Contains", "query", out bool found) && found)
                    {
                        if (command.TryCallMethod("GetElement", "query", out object queryElement) && queryElement != null)
                        {
                            if (queryElement.TryGetPropertyValue("Value", out object queryValue) && queryValue != null)
                            {
                                query = queryValue.ToString();
                            }
                        }
                    }
                }
                catch
                {
                    // TODO: logging
                }

                string[] parts = { operationName, databaseName, query };
                resourceName = string.Join(" ", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
            }

            Tracer tracer = Tracer.Instance;
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            var execute = DynamicMethodBuilder<Func<object, object, object, object>>
               .GetOrCreateMethodCallDelegate(
                    wireProtocol.GetType(),
                    "Execute");

            using (var scope = tracer.StartActive(OperationName, serviceName: serviceName))
            {
                scope.Span.Type = SpanTypes.MongoDB;
                scope.Span.ResourceName = resourceName;
                scope.Span.SetTag(Tags.DbName, databaseName);
                scope.Span.SetTag(Tags.MongoDbQuery, query);
                scope.Span.SetTag(Tags.MongoDbCollection, collectionName);
                scope.Span.SetTag(Tags.OutHost, host);
                scope.Span.SetTag(Tags.OutPort, port);

                try
                {
                    var cancellationToken = (cancellationTokenSource as CancellationTokenSource)?.Token ?? CancellationToken.None;
                    return execute(wireProtocol, connection, cancellationToken);
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }
    }
}
