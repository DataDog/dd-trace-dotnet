using System;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Helpers;
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

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="wireProtocol">The IWireProtocol instance we are replacing.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="cancellationTokenSource">A cancellation token source.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = MongoDbClientAssembly,
            TargetType = IWireProtocol,
            TargetSignatureTypes = new[] { ClrNames.Void, "MongoDB.Driver.Core.Connections.IConnection", ClrNames.CancellationToken },
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2)]
        public static object Execute(object wireProtocol, object connection, object cancellationTokenSource, int opCode, int mdToken)
        {
            if (wireProtocol == null) { throw new ArgumentNullException(nameof(wireProtocol)); }

            const string methodName = nameof(Execute);
            Func<object, object, CancellationToken, object> execute;
            var wireProtocolType = wireProtocol.GetType();

            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;

            try
            {
                execute =
                    MethodBuilder<Func<object, object, CancellationToken, object>>
                       .Start(Assembly.GetCallingAssembly(), mdToken, opCode, methodName)
                       .WithConcreteType(wireProtocolType)
                       .WithParameters(connection, cancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error retrieving {wireProtocolType.Name}.{methodName}(IConnection connection, CancellationToken cancellationToken)", ex);
                throw;
            }

            using (var scope = CreateScope(wireProtocol, connection))
            {
                try
                {
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
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetAssembly = MongoDbClientAssembly,
            TargetType = IWireProtocolGeneric,
            TargetSignatureTypes = new[] { "T", "MongoDB.Driver.Core.Connections.IConnection", ClrNames.CancellationToken },
            TargetMethod = nameof(Execute),
            TargetMinimumVersion = Major2Minor2,
            TargetMaximumVersion = Major2)]
        public static object ExecuteGeneric(object wireProtocol, object connection, object cancellationTokenSource, int opCode, int mdToken)
        {
            if (wireProtocol == null) { throw new ArgumentNullException(nameof(wireProtocol)); }

            const string methodName = nameof(Execute);
            Func<object, object, CancellationToken, object> execute;
            var wireProtocolType = wireProtocol.GetType();

            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            var genericArgs = GetGenericsFromWireProtocol(wireProtocolType);

            try
            {
                execute =
                    MethodBuilder<Func<object, object, CancellationToken, object>>
                       .Start(Assembly.GetCallingAssembly(), mdToken, opCode, methodName)
                       .WithConcreteType(wireProtocolType)
                       .WithDeclaringTypeGenerics(genericArgs)
                       .WithParameters(connection, cancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error retrieving {wireProtocolType.Name}.{methodName}(IConnection connection, CancellationToken cancellationToken)", ex);
                throw;
            }

            using (var scope = CreateScope(wireProtocol, connection))
            {
                try
                {
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
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetMethod = nameof(ExecuteAsync),
            TargetAssembly = MongoDbClientAssembly,
            TargetType = IWireProtocol,
            TargetSignatureTypes = new[] { ClrNames.Task, "MongoDB.Driver.Core.Connections.IConnection", ClrNames.CancellationToken },
            TargetMinimumVersion = Major2Minor1,
            TargetMaximumVersion = Major2)]
        public static object ExecuteAsync(object wireProtocol, object connection, object cancellationTokenSource, int opCode, int mdToken)
        {
            if (wireProtocol == null) { throw new ArgumentNullException(nameof(wireProtocol)); }

            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;

            const string methodName = nameof(ExecuteAsync);
            var wireProtocolType = wireProtocol.GetType();

            Func<object, object, CancellationToken, object> executeAsync;

            try
            {
                executeAsync =
                    MethodBuilder<Func<object, object, CancellationToken, object>>
                       .Start(Assembly.GetCallingAssembly(), mdToken, opCode, methodName)
                       .WithConcreteType(wireProtocolType)
                       .WithParameters(connection, cancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error calling {wireProtocolType.Name}.{methodName}(IConnection connection, CancellationToken cancellationToken)", ex);
                throw;
            }

            return ExecuteAsyncInternalNonGeneric(wireProtocol, connection, cancellationToken, executeAsync);
        }

        /// <summary>
        /// Wrap the original method by adding instrumentation code around it.
        /// </summary>
        /// <param name="wireProtocol">The IWireProtocol`1 instance we are replacing.</param>
        /// <param name="connection">The connection.</param>
        /// <param name="cancellationTokenSource">A cancellation token source.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <returns>The original method's return value.</returns>
        [InterceptMethod(
            TargetMethod = nameof(ExecuteAsync),
            TargetAssembly = MongoDbClientAssembly,
            TargetType = IWireProtocolGeneric,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<T>", "MongoDB.Driver.Core.Connections.IConnection", ClrNames.CancellationToken },
            TargetMinimumVersion = Major2Minor1,
            TargetMaximumVersion = Major2)]
        public static object ExecuteAsyncGeneric(object wireProtocol, object connection, object cancellationTokenSource, int opCode, int mdToken)
        {
            // The generic type for this method comes from the declaring type of wireProtocol
            if (wireProtocol == null) { throw new ArgumentNullException(nameof(wireProtocol)); }

            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;

            var wireProtocolType = wireProtocol.GetType();
            var genericArgs = GetGenericsFromWireProtocol(wireProtocolType);

            const string methodName = nameof(ExecuteAsync);
            Func<object, object, CancellationToken, object> executeAsync;

            try
            {
                executeAsync =
                    MethodBuilder<Func<object, object, CancellationToken, object>>
                       .Start(Assembly.GetCallingAssembly(), mdToken, opCode, methodName)
                       .WithConcreteType(wireProtocolType)
                       .WithDeclaringTypeGenerics(genericArgs)
                       .WithParameters(connection, cancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                // profiled app will not continue working as expected without this method
                Log.ErrorException($"Error resolving {wireProtocolType.Name}.{methodName}(IConnection connection, CancellationToken cancellationToken)", ex);
                throw;
            }

            return AsyncHelper.InvokeGenericTaskDelegate(
                wireProtocolType,
                genericArgs[0],
                nameof(ExecuteAsyncInternalGeneric),
                typeof(MongoDbIntegration),
                wireProtocol,
                connection,
                cancellationToken,
                executeAsync);
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

        private static async Task ExecuteAsyncInternalNonGeneric(
            object wireProtocol,
            object connection,
            CancellationToken cancellationToken,
            Func<object, object, CancellationToken, object> originalMethod)
        {
            using (var scope = CreateScope(wireProtocol, connection))
            {
                try
                {
                    if (originalMethod == null)
                    {
                        throw new ArgumentNullException(nameof(originalMethod));
                    }

                    var taskObject = originalMethod(wireProtocol, connection, cancellationToken);
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
            CancellationToken cancellationToken,
            Func<object, object, CancellationToken, object> originalMethod)
        {
            using (var scope = CreateScope(wireProtocol, connection))
            {
                try
                {
                    var taskObject = originalMethod(wireProtocol, connection, cancellationToken);
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
