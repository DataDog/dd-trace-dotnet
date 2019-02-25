using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracing integration for MongoDB.Driver.Core.
    /// </summary>
    public static class MongoDbIntegration
    {
        internal const string OperationName = "mongodb.query";
        internal const string ServiceName = "mongodb";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(MongoDbIntegration));

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="wireProtocol">The IWireProtocol`1 instance we are replacing.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="cancellationTokenSource">A cancellation token source.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = "MongoDB.Driver.Core",
            TargetType = "MongoDB.Driver.Core.WireProtocol.IWireProtocol")]
        [InterceptMethod(
            TargetAssembly = "MongoDB.Driver.Core",
            TargetType = "MongoDB.Driver.Core.WireProtocol.IWireProtocol`1")]
        public static object Execute(object wireProtocol, object connection, object cancellationTokenSource)
        {
            // TResult MongoDB.Driver.Core.WireProtocol.IWireProtocol<TResult>.Execute(IConnection connection, CancellationToken cancellationToken)
            if (wireProtocol == null) { throw new ArgumentNullException(nameof(wireProtocol)); }

            var execute = DynamicMethodBuilder<Func<object, object, CancellationToken, object>>
               .GetOrCreateMethodCallDelegate(
                    wireProtocol.GetType(),
                    "Execute");

            using (var scope = CreateScope(wireProtocol, connection))
            {
                try
                {
                    var tokenSource = cancellationTokenSource as CancellationTokenSource;
                    var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
                    return execute(wireProtocol, connection, cancellationToken);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="wireProtocol">The IWireProtocol`1 instance we are replacing.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="cancellationTokenSource">A cancellation token source.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = "MongoDB.Driver.Core",
            TargetType = "MongoDB.Driver.Core.WireProtocol.IWireProtocol")]
        [InterceptMethod(
            TargetAssembly = "MongoDB.Driver.Core",
            TargetType = "MongoDB.Driver.Core.WireProtocol.IWireProtocol`1")]
        public static object ExecuteAsync(object wireProtocol, object connection, object cancellationTokenSource)
        {
            // TResult MongoDB.Driver.Core.WireProtocol.IWireProtocol<TResult>.Execute(IConnection connection, CancellationToken cancellationToken)
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            return ExecuteAsyncInternal(wireProtocol, connection, cancellationToken);
        }

        private static async Task<object> ExecuteAsyncInternal(object wireProtocol, object connection, CancellationToken cancellationToken)
        {
            if (wireProtocol == null) { throw new ArgumentNullException(nameof(wireProtocol)); }

            var executeAsync = DynamicMethodBuilder<Func<object, object, CancellationToken, Task<object>>>
               .GetOrCreateMethodCallDelegate(
                    wireProtocol.GetType(),
                    "ExecuteAsync");

            using (var scope = CreateScope(wireProtocol, connection))
            {
                try
                {
                    return await executeAsync(wireProtocol, connection, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scope.Span.SetException(ex);
                    throw;
                }
            }
        }

        private static Scope CreateScope(object wireProtocol, object connection)
        {
            string databaseName = null;
            string host = null;
            string port = null;

            try
            {
                databaseName = wireProtocol.GetField("_databaseNamespace").GetProperty<string>("DatabaseName").GetValueOrDefault();
            }
            catch (Exception ex)
            {
                Log.WarnException("Unable to access DatabaseName property.", ex);
            }

            try
            {
                var endpoint = connection?.GetProperty("EndPoint").GetValueOrDefault();

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
            catch (Exception ex)
            {
                Log.WarnException("Unable to access EndPoint properties.", ex);
            }

            string operationName = null;
            string collectionName = null;
            string query = null;
            string resourceName = null;

            try
            {
                var command = wireProtocol.GetField("_command");

                // the name of the first element in the command BsonDocument will be the operation type (insert, delete, find, etc)
                // and its value is the collection name
                var firstElement = command.CallMethod("GetElement", 0);
                operationName = firstElement.GetProperty<string>("Name").GetValueOrDefault();
                collectionName = firstElement.GetProperty("Value").GetValueOrDefault()?.ToString();

                if (operationName == "buildInfo" || operationName == "isMaster" || operationName == "getLastError")
                {
                    // don't create a scope for these internal (non-application) queries
                    // made automatically by the client
                    return null;
                }

                // get the "query" element from the command BsonDocument, if it exists
                bool found = command.CallMethod<string, bool>("Contains", "query").GetValueOrDefault();

                if (found)
                {
                    query = command.CallMethod("GetElement", "query").GetProperty("Value").GetValueOrDefault()?.ToString();
                }

                resourceName = $"{operationName ?? "operation"} {databaseName ?? "database"} {query ?? "query"}";
            }
            catch (Exception ex)
            {
                Log.WarnException("Unable to access IWireProtocol.Command properties.", ex);
            }

            Tracer tracer = Tracer.Instance;
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            var scope = tracer.StartActive(OperationName, serviceName: serviceName);
            scope.Span.Type = SpanTypes.MongoDB;
            scope.Span.ResourceName = resourceName;
            scope.Span.SetTag(Tags.DbName, databaseName);
            scope.Span.SetTag(Tags.MongoDbQuery, query);
            scope.Span.SetTag(Tags.MongoDbCollection, collectionName);
            scope.Span.SetTag(Tags.OutHost, host);
            scope.Span.SetTag(Tags.OutPort, port);
            return scope;
        }
    }
}
