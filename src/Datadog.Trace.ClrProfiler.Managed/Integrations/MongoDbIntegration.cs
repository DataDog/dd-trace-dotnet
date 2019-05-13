using System;
using System.Net;
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

        private const string Major2 = "2";
        private const string Major2Minor1 = "2.1";
        private const string Major2Minor2 = "2.2"; // Synchronous methods added in 2.2
        private const string MongoDbClientAssembly = "MongoDB.Driver.Core";
        private const string IWireProtocol = "MongoDB.Driver.Core.WireProtocol.IWireProtocol";
        private const string IWireProtocolGeneric = "MongoDB.Driver.Core.WireProtocol.IWireProtocol`1";

        private static readonly ILog Log = LogProvider.GetLogger(typeof(MongoDbIntegration));

        private static readonly InterceptedMethodAccess<Func<object, object, CancellationToken, object>> ExecuteAccess = new InterceptedMethodAccess<Func<object, object, CancellationToken, object>>();
        private static readonly InterceptedMethodAccess<Func<object, object, CancellationToken, object>> ExecuteAsyncAccess = new InterceptedMethodAccess<Func<object, object, CancellationToken, object>>();
        private static readonly GenericAsyncTargetAccess AsyncTargetAccess = new GenericAsyncTargetAccess();

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="wireProtocol">The IWireProtocol`1 or IWireProtocol instance we are replacing.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="cancellationTokenSource">A cancellation token source.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = MongoDbClientAssembly,
            TargetType = IWireProtocol,
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2)]
        [InterceptMethod(
            TargetAssembly = MongoDbClientAssembly,
            TargetType = IWireProtocolGeneric,
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2)]
        public static object Execute(object wireProtocol, object connection, object cancellationTokenSource)
        {
            if (wireProtocol == null) { throw new ArgumentNullException(nameof(wireProtocol)); }

            const string methodName = nameof(Execute);
            Func<object, object, CancellationToken, object> execute;
            var wireProtocolType = wireProtocol.GetType();

            try
            {
                execute = ExecuteAccess.GetInterceptedMethod(
                    wireProtocolType,
                    returnType: null, // return type doesn't matter
                    methodName: methodName,
                    generics: Interception.NullTypeArray,
                    parameters: Interception.ParamsToTypes(connection, cancellationTokenSource));
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error calling {wireProtocolType.Name}.{methodName}(IConnection connection, CancellationToken cancellationToken)", ex);
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
        /// <param name="wireProtocol">The IWireProtocol instance we are replacing.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="cancellationTokenSource">A cancellation token source.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetMethod = nameof(ExecuteAsync),
            TargetAssembly = MongoDbClientAssembly,
            TargetType = IWireProtocol,
            TargetMinimumVersion = Major2Minor1,
            TargetMaximumVersion = Major2)]
        public static object ExecuteAsync(object wireProtocol, object connection, object cancellationTokenSource)
        {
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            return ExecuteAsyncInternalNonGeneric(wireProtocol, connection, cancellationToken);
        }

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="wireProtocol">The IWireProtocol`1 instance we are replacing.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="cancellationTokenSource">A cancellation token source.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetMethod = nameof(ExecuteAsync),
            TargetAssembly = MongoDbClientAssembly,
            TargetType = IWireProtocolGeneric,
            TargetMinimumVersion = Major2Minor1,
            TargetMaximumVersion = Major2)]
        public static object ExecuteAsyncGeneric(object wireProtocol, object connection, object cancellationTokenSource)
        {
            if (wireProtocol == null) { throw new ArgumentNullException(nameof(wireProtocol)); }

            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;

            var wireProtocolType = wireProtocol.GetType();
            var interfaces = wireProtocolType.GetInterfaces();
            Type typeWeInstrument = null;

            for (var i = 0; i < interfaces.Length; i++)
            {
                if ($"{interfaces[i].Namespace}.{interfaces[i].Name}" == IWireProtocolGeneric)
                {
                    typeWeInstrument = interfaces[i];
                    break;
                }
            }

            if (typeWeInstrument == null)
            {
                throw new ArgumentException($"Unable to find the instrumented interface: {IWireProtocolGeneric}");
            }

            var genericArgs = typeWeInstrument.GetGenericArguments();

            if (genericArgs.Length == 0)
            {
                throw new ArgumentException($"Expected generics to determine TaskResult from {wireProtocolType.AssemblyQualifiedName}");
            }

            return AsyncTargetAccess.InvokeGenericTaskDelegate(
                wireProtocolType,
                genericArgs[0],
                nameof(ExecuteAsyncInternalGeneric),
                typeof(MongoDbIntegration),
                wireProtocol,
                connection,
                cancellationToken);
        }

        private static async Task ExecuteAsyncInternalNonGeneric(object wireProtocol, object connection, CancellationToken cancellationToken)
        {
            if (wireProtocol == null) { throw new ArgumentNullException(nameof(wireProtocol)); }

            const string methodName = nameof(ExecuteAsync);
            Func<object, object, CancellationToken, object> executeAsync = null;
            var wireProtocolType = wireProtocol.GetType();

            try
            {
                executeAsync = ExecuteAsyncAccess.GetInterceptedMethod(
                    owningType: wireProtocolType,
                    returnType: typeof(Task),
                    methodName: methodName,
                    generics: Interception.NullTypeArray,
                    parameters: Interception.ParamsToTypes(connection, cancellationToken));
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error calling {wireProtocolType.Name}.{methodName}(IConnection connection, CancellationToken cancellationToken)", ex);
            }

            using (var scope = CreateScope(wireProtocol, connection))
            {
                try
                {
                    if (executeAsync == null)
                    {
                        throw new Exception();
                    }

                    var taskObject = executeAsync(wireProtocol, connection, cancellationToken);
                    var task = (Task)taskObject;
                    await task.ConfigureAwait(false);
                }
                catch (Exception ex) when (scope?.Span.SetExceptionForFilter(ex) ?? false)
                {
                    // unreachable code
                    throw;
                }
            }
        }

        private static async Task<T> ExecuteAsyncInternalGeneric<T>(
            object wireProtocol,
            object connection,
            CancellationToken cancellationToken)
        {
            const string methodName = nameof(ExecuteAsync);
            Func<object, object, CancellationToken, object> executeAsync;
            var wireProtocolType = wireProtocol.GetType();

            try
            {
                executeAsync = ExecuteAsyncAccess.GetInterceptedMethod(
                    owningType: wireProtocolType,
                    returnType: typeof(Task<T>),
                    methodName: methodName,
                    generics: Interception.NullTypeArray,
                    parameters: Interception.ParamsToTypes(connection, cancellationToken));
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error calling {wireProtocolType.Name}.{methodName}(IConnection connection, CancellationToken cancellationToken)", ex);
                throw;
            }

            using (var scope = CreateScope(wireProtocol, connection))
            {
                try
                {
                    var taskObject = executeAsync(wireProtocol, connection, cancellationToken);
                    var typedTask = (Task<T>)taskObject;
                    return await typedTask.ConfigureAwait(false);
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
