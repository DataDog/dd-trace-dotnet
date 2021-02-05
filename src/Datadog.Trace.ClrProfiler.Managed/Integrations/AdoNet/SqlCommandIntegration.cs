using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.ClrProfiler.Helpers;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.AdoNet
{
    /// <summary>
    /// Instrumentation wrappers for <c>System.Data.SqlClient.SqlCommand</c>
    /// and <c>Microsoft.Data.SqlClient.SqlCommand</c>.
    /// </summary>
    public static class SqlCommandIntegration
    {
        private const string Major1 = "1";
        private const string Major2 = "2";
        private const string Major4 = "4";

        private const string SqlCommandTypeName = "SqlCommand";
        private const string SqlDataReaderTypeName = "SqlDataReader";

        private const string SystemSqlClientAssemblyName = "System.Data.SqlClient";
        private const string SystemSqlClientNamespace = SystemSqlClientAssemblyName;
        private const string SystemSqlCommandTypeName = SystemSqlClientNamespace + "." + SqlCommandTypeName;
        private const string SystemSqlDataReaderTypeName = SystemSqlClientNamespace + "." + SqlDataReaderTypeName;

        private const string MicrosoftSqlClientAssemblyName = "Microsoft.Data.SqlClient";
        private const string MicrosoftSqlClientNamespace = MicrosoftSqlClientAssemblyName;
        private const string MicrosoftSqlCommandTypeName = MicrosoftSqlClientNamespace + "." + SqlCommandTypeName;
        private const string MicrosoftSqlDataReaderTypeName = MicrosoftSqlClientNamespace + "." + SqlDataReaderTypeName;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(SqlCommandIntegration));

        /// <summary>
        /// Instrumentation wrapper for System.Data.SqlCommand.ExecuteReader().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData },
            TargetType = SystemSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetSignatureTypes = new[] { SystemSqlDataReaderTypeName },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { SystemSqlClientAssemblyName },
            TargetType = SystemSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetSignatureTypes = new[] { SystemSqlDataReaderTypeName },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object SystemSqlClientExecuteReader(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteReader(command, opCode, mdToken, moduleVersionPtr, SystemSqlClientNamespace, SystemSqlDataReaderTypeName);
        }

        /// <summary>
        /// Instrumentation wrapper for Microsoft.Data.SqlCommand.ExecuteReader().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { MicrosoftSqlClientAssemblyName },
            TargetType = MicrosoftSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetSignatureTypes = new[] { MicrosoftSqlDataReaderTypeName },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static object MicrosoftSqlClientExecuteReader(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteReader(command, opCode, mdToken, moduleVersionPtr, MicrosoftSqlClientNamespace, MicrosoftSqlDataReaderTypeName);
        }

        private static object ExecuteReader(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr,
            string sqlClientNamespace,
            string dataReaderTypeName)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteReader;
            Func<object, object> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(sqlClientNamespace, SqlCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithNamespaceAndNameFilters(dataReaderTypeName)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: $"{sqlClientNamespace}.{SqlCommandTypeName}",
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, command as DbCommand))
            {
                try
                {
                    return instrumentedMethod(command);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for System.Data.SqlCommand.ExecuteReader(CommandBehavior).
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="behavior">The <see cref="CommandBehavior"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData },
            TargetType = SystemSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetSignatureTypes = new[] { SystemSqlDataReaderTypeName, AdoNetConstants.TypeNames.CommandBehavior },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { SystemSqlClientAssemblyName },
            TargetType = SystemSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetSignatureTypes = new[] { SystemSqlDataReaderTypeName, AdoNetConstants.TypeNames.CommandBehavior },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object SystemSqlClientExecuteReaderWithBehavior(
            object command,
            int behavior,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteReaderWithBehavior(command, behavior, opCode, mdToken, moduleVersionPtr, SystemSqlClientNamespace, SystemSqlDataReaderTypeName);
        }

        /// <summary>
        /// Instrumentation wrapper for Microsoft.Data.SqlCommand.ExecuteReader().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="behavior">The <see cref="CommandBehavior"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { MicrosoftSqlClientAssemblyName },
            TargetType = MicrosoftSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetSignatureTypes = new[] { MicrosoftSqlDataReaderTypeName, AdoNetConstants.TypeNames.CommandBehavior },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static object MicrosoftSqlClientExecuteReaderWithBehavior(
            object command,
            int behavior,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteReaderWithBehavior(command, behavior, opCode, mdToken, moduleVersionPtr, MicrosoftSqlClientNamespace, MicrosoftSqlDataReaderTypeName);
        }

        private static object ExecuteReaderWithBehavior(
            object command,
            int behavior,
            int opCode,
            int mdToken,
            long moduleVersionPtr,
            string sqlClientNamespace,
            string dataReaderTypeName)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteReader;
            var commandBehavior = (CommandBehavior)behavior;
            Func<object, CommandBehavior, object> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(sqlClientNamespace, SqlCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<object, CommandBehavior, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithParameters(commandBehavior)
                       .WithNamespaceAndNameFilters(dataReaderTypeName, AdoNetConstants.TypeNames.CommandBehavior)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: $"{sqlClientNamespace}.{SqlCommandTypeName}",
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, command as DbCommand))
            {
                try
                {
                    return instrumentedMethod(command, commandBehavior);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for System.Data.SqlCommand.ExecuteReaderAsync().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="behavior">The <see cref="CommandBehavior"/> value used in the original method call.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, SystemSqlClientAssemblyName },
            TargetType = SystemSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReaderAsync,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Data.SqlClient.SqlDataReader>", AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object SystemSqlClientExecuteReaderAsync(
            object command,
            int behavior,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteReaderAsync(command, behavior, boxedCancellationToken, opCode, mdToken, moduleVersionPtr, SystemSqlClientNamespace);
        }

        /// <summary>
        /// Instrumentation wrapper for Microsoft.Data.SqlCommand.ExecuteReaderAsync().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="behavior">The <see cref="CommandBehavior"/> value used in the original method call.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { MicrosoftSqlClientAssemblyName },
            TargetType = MicrosoftSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReaderAsync,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<Microsoft.Data.SqlClient.SqlDataReader>", AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static object MicrosoftSqlClientExecuteReaderAsync(
            object command,
            int behavior,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteReaderAsync(command, behavior, boxedCancellationToken, opCode, mdToken, moduleVersionPtr, MicrosoftSqlClientNamespace);
        }

        private static object ExecuteReaderAsync(
            object command,
            int behavior,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr,
            string sqlClientNamespace)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteReaderAsync;
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var commandBehavior = (CommandBehavior)behavior;

            Type sqlCommandType;
            Type sqlDataReaderType;

            try
            {
                sqlCommandType = command.GetInstrumentedType(sqlClientNamespace, SqlCommandTypeName);
                sqlDataReaderType = sqlCommandType.Assembly.GetType($"{sqlClientNamespace}.{SqlDataReaderTypeName}");
            }
            catch (Exception ex)
            {
                // This shouldn't happen because the assembly holding the SqlCommand and SqlDataReader types should have been loaded already.
                // Profiled app will not continue working as expected without this method.
                Log.Error(ex, "Error finding the SqlCommand or SqlDataReader type");
                throw;
            }

            Func<DbCommand, CommandBehavior, CancellationToken, object> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, CommandBehavior, CancellationToken, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(sqlCommandType)
                       .WithParameters(commandBehavior, cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: $"{sqlClientNamespace}.{SqlCommandTypeName}",
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            return AsyncHelper.InvokeGenericTaskDelegate(
                owningType: command.GetType(),
                taskResultType: sqlDataReaderType,
                nameOfIntegrationMethod: nameof(ExecuteReaderAsyncInternal),
                integrationType: typeof(SqlCommandIntegration),
                (DbCommand)command,
                commandBehavior,
                cancellationToken,
                instrumentedMethod);
        }

        /// <summary>
        /// Calls the underlying ExecuteReaderAsync and traces the request.
        /// </summary>
        /// <typeparam name="T">The type of the generic Task instantiation</typeparam>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="commandBehavior">The <see cref="CommandBehavior"/> value used in the original method call.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <param name="instrumentedMethod">A delegate for the method we are instrumenting</param>
        /// <returns>A task with the result</returns>
        private static async Task<T> ExecuteReaderAsyncInternal<T>(
            DbCommand command,
            CommandBehavior commandBehavior,
            CancellationToken cancellationToken,
            Func<DbCommand, CommandBehavior, CancellationToken, object> instrumentedMethod)
        {
            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, command))
            {
                try
                {
                    var task = (Task<T>)instrumentedMethod(command, commandBehavior, cancellationToken);
                    return await task.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for System.Data.SqlCommand.ExecuteNonQuery().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData },
            TargetType = SystemSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteNonQuery,
            TargetSignatureTypes = new[] { ClrNames.Int32 },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { SystemSqlClientAssemblyName },
            TargetType = SystemSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteNonQuery,
            TargetSignatureTypes = new[] { ClrNames.Int32 },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static int SystemSqlClientExecuteNonQuery(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteNonQuery(command, opCode, mdToken, moduleVersionPtr, SystemSqlClientNamespace);
        }

        /// <summary>
        /// Instrumentation wrapper for Microsoft.Data.SqlCommand.ExecuteNonQuery().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { MicrosoftSqlClientAssemblyName },
            TargetType = MicrosoftSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteNonQuery,
            TargetSignatureTypes = new[] { ClrNames.Int32 },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static int MicrosoftSqlClientExecuteNonQuery(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteNonQuery(command, opCode, mdToken, moduleVersionPtr, MicrosoftSqlClientNamespace);
        }

        private static int ExecuteNonQuery(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr,
            string sqlClientNamespace)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteNonQuery;
            Func<DbCommand, int> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(sqlClientNamespace, SqlCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, int>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithNamespaceAndNameFilters(ClrNames.Int32)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: $"{sqlClientNamespace}.{SqlCommandTypeName}",
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            var dbCommand = command as DbCommand;

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, dbCommand))
            {
                try
                {
                    return instrumentedMethod(dbCommand);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for System.Data.SqlCommand.ExecuteNonQueryAsync(CancellationToken).
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, SystemSqlClientAssemblyName },
            TargetType = SystemSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteNonQueryAsync,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Int32>", ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object SystemSqlClientExecuteNonQueryAsync(
            object command,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteNonQueryAsyncInternal(
                (DbCommand)command,
                (CancellationToken)boxedCancellationToken,
                opCode,
                mdToken,
                moduleVersionPtr,
                SystemSqlClientNamespace);
        }

        /// <summary>
        /// Instrumentation wrapper for Microsoft.Data.SqlCommand.ExecuteNonQueryAsync(CancellationToken).
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { MicrosoftSqlClientAssemblyName },
            TargetType = MicrosoftSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteNonQueryAsync,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Int32>", ClrNames.CancellationToken },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static object MicrosoftSqlClientExecuteNonQueryAsync(
            object command,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteNonQueryAsyncInternal(
                (DbCommand)command,
                (CancellationToken)boxedCancellationToken,
                opCode,
                mdToken,
                moduleVersionPtr,
                MicrosoftSqlClientNamespace);
        }

        private static async Task<int> ExecuteNonQueryAsyncInternal(
            DbCommand command,
            CancellationToken cancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr,
            string sqlClientNamespace)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteNonQueryAsync;
            Func<DbCommand, CancellationToken, Task<int>> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(sqlClientNamespace, SqlCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, CancellationToken, Task<int>>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithParameters(cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: $"{sqlClientNamespace}.{SqlCommandTypeName}",
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, command))
            {
                try
                {
                    return await instrumentedMethod(command, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for System.Data.SqlCommand.ExecuteScalar().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData },
            TargetType = SystemSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteScalar,
            TargetSignatureTypes = new[] { ClrNames.Object },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { SystemSqlClientAssemblyName },
            TargetType = SystemSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteScalar,
            TargetSignatureTypes = new[] { ClrNames.Object },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object SystemSqlClientExecuteScalar(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteScalar(command, opCode, mdToken, moduleVersionPtr, SystemSqlClientNamespace);
        }

        /// <summary>
        /// Instrumentation wrapper for Microsoft.Data.SqlCommand.ExecuteScalar().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { MicrosoftSqlClientAssemblyName },
            TargetType = MicrosoftSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteScalar,
            TargetSignatureTypes = new[] { ClrNames.Object },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static object MicrosoftSqlClientExecuteScalar(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteScalar(command, opCode, mdToken, moduleVersionPtr, MicrosoftSqlClientNamespace);
        }

        private static object ExecuteScalar(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr,
            string sqlClientNamespace)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteScalar;
            Func<DbCommand, object> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(sqlClientNamespace, SqlCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithNamespaceAndNameFilters(ClrNames.Object)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: $"{sqlClientNamespace}.{SqlCommandTypeName}",
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            var dbCommand = command as DbCommand;

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, dbCommand))
            {
                try
                {
                    return instrumentedMethod(dbCommand);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for System.Data.SqlCommand.ExecuteScalarAsync(CancellationToken).
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, SystemSqlClientAssemblyName },
            TargetType = SystemSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteScalarAsync,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Object>", ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object SystemSqlClientExecuteScalarAsync(
            object command,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteScalarAsyncInternal(
                (DbCommand)command,
                (CancellationToken)boxedCancellationToken,
                opCode,
                mdToken,
                moduleVersionPtr,
                SystemSqlClientNamespace);
        }

        /// <summary>
        /// Instrumentation wrapper for Microsoft.Data.SqlCommand.ExecuteScalarAsync(CancellationToken).
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { MicrosoftSqlClientAssemblyName },
            TargetType = MicrosoftSqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteScalarAsync,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Object>", ClrNames.CancellationToken },
            TargetMinimumVersion = Major1,
            TargetMaximumVersion = Major2)]
        public static object MicrosoftSqlClientExecuteScalarAsync(
            object command,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            return ExecuteScalarAsyncInternal(
                (DbCommand)command,
                (CancellationToken)boxedCancellationToken,
                opCode,
                mdToken,
                moduleVersionPtr,
                MicrosoftSqlClientNamespace);
        }

        private static async Task<object> ExecuteScalarAsyncInternal(
            DbCommand command,
            CancellationToken cancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr,
            string sqlClientNamespace)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteScalarAsync;
            Func<DbCommand, CancellationToken, Task<object>> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(sqlClientNamespace, SqlCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, CancellationToken, Task<object>>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithParameters(cancellationToken)
                       .WithNamespaceAndNameFilters(ClrNames.GenericTask, ClrNames.CancellationToken)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: $"{sqlClientNamespace}.{SqlCommandTypeName}",
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, command))
            {
                try
                {
                    return await instrumentedMethod(command, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }
    }
}
