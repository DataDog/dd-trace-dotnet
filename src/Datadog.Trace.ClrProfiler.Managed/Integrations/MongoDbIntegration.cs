using System;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    /// Tracing integration for MongoDB.Driver.Core.
    /// </summary>
    public static class MongoDbIntegration
    {
        private const string IntegrationName = "MongoDb";
        private const string OperationName = "mongodb.query";
        private const string ServiceName = "mongodb";
        private const string Major2Minor2 = "2.2";
        private const string Major2 = "2";

        private static readonly InterceptedMethodAccess<Action<object, object, object>> ExecuteAccess = new InterceptedMethodAccess<Action<object, object, object>>();

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
            TargetType = "MongoDB.Driver.Core.WireProtocol.IWireProtocol",
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2)]
        public static object ExecuteIWireProtocol(object wireProtocol, object connection, object cancellationTokenSource)
        {
            return Execute(
                "MongoDB.Driver.Core.WireProtocol.IWireProtocol",
                wireProtocol,
                connection,
                cancellationTokenSource);
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
            TargetType = "MongoDB.Driver.Core.WireProtocol.IWireProtocol`1",
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2)]
        public static object ExecuteIWireProtocol1(object wireProtocol, object connection, object cancellationTokenSource)
        {
            return Execute(
                "MongoDB.Driver.Core.WireProtocol.IWireProtocol`1",
                wireProtocol,
                connection,
                cancellationTokenSource);
        }

        private static object Execute(
            string owningType,
            object wireProtocol,
            object connection,
            object cancellationTokenSource)
        {
            if (wireProtocol == null) { throw new ArgumentNullException(nameof(wireProtocol)); }

            Func<object, object, object, object> execute;

            try
            {
                execute = ExecuteAccess.GetInterceptedMethod(
                    assembly: Assembly.GetCallingAssembly(),
                    owningType: owningType,
                    methodName: nameof(Execute),
                    generics: Interception.NoArguments,
                    parameters: Interception.TypeArray(wireProtocol, connection, cancellationTokenSource));
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this rethrow method
                Log.ErrorException($"Error calling {owningType}.{nameof(Execute)}(object wireProtocol, object connection, object cancellationTokenSource)", ex);
                throw;
            }

            using (var scope = CreateScope(wireProtocol, connection))
            {
                try
                {
                    var tokenSource = cancellationTokenSource as CancellationTokenSource;
                    var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
                    return execute(wireProtocol, connection, cancellationToken);
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
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
            TargetType = "MongoDB.Driver.Core.WireProtocol.IWireProtocol",
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2)]
        [InterceptMethod(
            TargetAssembly = "MongoDB.Driver.Core",
            TargetType = "MongoDB.Driver.Core.WireProtocol.IWireProtocol`1",
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2)]
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

            var executeAsync = Emit.DynamicMethodBuilder<Func<object, object, CancellationToken, Task<object>>>
               .GetOrCreateMethodCallDelegate(
                    wireProtocol.GetType(),
                    "ExecuteAsync");

            using (var scope = CreateScope(wireProtocol, connection))
            {
                try
                {
                    return await executeAsync(wireProtocol, connection, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        private static Scope CreateScope(object wireProtocol, object connection)
        {
            if (!Tracer.Instance.Settings.IsIntegrationEnabled(IntegrationName))
            {
                // integration disabled, don't create a scope, skip this trace
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
                Log.WarnException("Unable to access DatabaseName property.", ex);
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
                Log.WarnException("Unable to access EndPoint properties.", ex);
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

                    resourceName = $"{operationName ?? "operation"} {databaseName ?? "database"} {query ?? "query"}";
                }
            }
            catch (Exception ex)
            {
                Log.WarnException("Unable to access IWireProtocol.Command properties.", ex);
            }

            Tracer tracer = Tracer.Instance;
            string serviceName = string.Join("-", tracer.DefaultServiceName, ServiceName);

            Scope scope = null;

            try
            {
                scope = tracer.StartActive(OperationName, serviceName: serviceName);
                var span = scope.Span;
                span.Type = SpanTypes.MongoDb;
                span.ResourceName = resourceName;
                span.SetTag(Tags.DbName, databaseName);
                span.SetTag(Tags.MongoDbQuery, query);
                span.SetTag(Tags.MongoDbCollection, collectionName);
                span.SetTag(Tags.OutHost, host);
                span.SetTag(Tags.OutPort, port);

                // set analytics sample rate if enabled
                var analyticsSampleRate = tracer.Settings.GetIntegrationAnalyticsSampleRate(IntegrationName, enabledWithGlobalSetting: false);
                span.SetMetric(Tags.Analytics, analyticsSampleRate);
            }
            catch (Exception ex)
            {
                Log.ErrorException("Error creating or populating scope.", ex);
            }

            return scope;
        }
    }
}
