using System;
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
            var cancellationToken = (cancellationTokenSource as CancellationTokenSource)?.Token ?? CancellationToken.None;
            Type wireProtocolType = wireProtocol.GetType();

            wireProtocol.TryGetFieldValue("_command", out object command);

            wireProtocol.TryGetFieldValue("_databaseNamespace", out object databaseNamespace);
            databaseNamespace.TryGetPropertyValue("DatabaseName", out string databaseName);

            string host = null;
            string port = null;

            if (connection.TryGetPropertyValue("EndPoint", out object endpoint))
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
            // string collectionName = null;
            string query = null;
            string resourceName = null;

            if (command != null)
            {
                try
                {
                    command.TryCallMethod("GetElement", 0, out object firstElement);
                    firstElement.TryGetPropertyValue("Name", out operationName);
                    // firstElement.TryGetPropertyValue("Value", out object collectionNameObj);
                    // collectionName = (string)collectionNameObj;

                    if (command.TryCallMethod("Contains", "query", out bool found) && found)
                    {
                        command.TryCallMethod("GetElement", "query", out object queryElement);
                        queryElement.TryGetPropertyValue("Value", out object queryValue);
                        query = queryValue.ToString();
                    }
                }
                catch
                {
                    // TODO: logging
                }

                string[] parts = { operationName, databaseName, query };
                resourceName = string.Join(" ", parts);
            }

            Tracer tracer = Tracer.Instance;
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            var execute = DynamicMethodBuilder<Func<object, object, object, object>>
               .GetOrCreateMethodCallDelegate(
                    wireProtocolType,
                    "Execute");

            using (var scope = tracer.StartActive(OperationName, serviceName: serviceName))
            {
                scope.Span.Type = SpanTypes.MongoDB;
                scope.Span.ResourceName = resourceName;
                scope.Span.SetTag(Tags.MongoDbQuery, query);
                scope.Span.SetTag(Tags.DbName, databaseName);
                // scope.Span.SetTag("mongodb.collection", collectionName);
                scope.Span.SetTag(Tags.OutHost, host);
                scope.Span.SetTag(Tags.OutPort, port);

                try
                {
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
