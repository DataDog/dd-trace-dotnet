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
    /// Instrumentation wrappers for <see cref="NpgsqlCommandIntegration"/>.
    /// </summary>
    public static class NpgsqlCommandIntegration
    {
        private const string Major4 = "4";

        private const string NpgsqlAssemblyName = "Npgsql";
        private const string NpgsqlCommandTypeName = "Npgsql.NpgsqlCommand";
        private const string NpgsqlDataReaderTypeName = "Npgsql.NpgsqlDataReader";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(NpgsqlCommandIntegration));

        /// <summary>
        /// Instrumentation wrapper for NpgsqlCommand.ExecuteReader() />
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { NpgsqlAssemblyName },
            TargetType = NpgsqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetSignatureTypes = new[] { NpgsqlDataReaderTypeName },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteReader(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteReader;
            Func<object, object> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(NpgsqlCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<object, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithNamespaceAndNameFilters(NpgsqlDataReaderTypeName)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: NpgsqlCommandTypeName,
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
        /// Instrumentation wrapper for NpgsqlCommand.ExecuteReader(CommandBehavior).
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="behavior">The <see cref="CommandBehavior"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { NpgsqlAssemblyName },
            TargetType = NpgsqlCommandTypeName,
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetSignatureTypes = new[] { NpgsqlDataReaderTypeName, AdoNetConstants.TypeNames.CommandBehavior },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteReaderWithBehavior(
            object command,
            int behavior,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteReader;
            Func<object, CommandBehavior, object> instrumentedMethod;
            var commandBehavior = (CommandBehavior)behavior;

            try
            {
                var targetType = command.GetInstrumentedType(NpgsqlCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<object, CommandBehavior, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithParameters(commandBehavior)
                       .WithNamespaceAndNameFilters(NpgsqlDataReaderTypeName, AdoNetConstants.TypeNames.CommandBehavior)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: NpgsqlCommandTypeName,
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
        /// Instrumentation wrapper for NpgsqlCommand.ExecuteReaderAsync(CancellationToken).
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { NpgsqlAssemblyName },
            TargetType = NpgsqlCommandTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<Npgsql.NpgsqlDataReader>", ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteReaderAsync(
            object command,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteReaderAsync;
            var cancellationToken = (CancellationToken)boxedCancellationToken;

            Type npgsqlComandType;
            Type npgsqlDataReaderType;

            try
            {
                npgsqlComandType = command.GetInstrumentedType(NpgsqlCommandTypeName);
                npgsqlDataReaderType = npgsqlComandType.Assembly.GetType(NpgsqlDataReaderTypeName);
            }
            catch (Exception ex)
            {
                // This shouldn't happen because the assembly holding the Npgsql.NpgsqlDataReader type should have been loaded already
                // profiled app will not continue working as expected without this method
                Log.Error(ex, "Error finding the Npgsql.NpgsqlDataReader type");
                throw;
            }

            Func<DbCommand, CancellationToken, object> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, CancellationToken, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(npgsqlComandType)
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
                    instrumentedType: NpgsqlCommandTypeName,
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            return AsyncHelper.InvokeGenericTaskDelegate(
                owningType: command.GetType(),
                taskResultType: npgsqlDataReaderType,
                nameOfIntegrationMethod: nameof(ExecuteReaderAsyncInternal),
                integrationType: typeof(NpgsqlCommandIntegration),
                (DbCommand)command,
                cancellationToken,
                instrumentedMethod);
        }

        /// <summary>
        /// Calls the underlying ExecuteReaderAsync and traces the request.
        /// </summary>
        /// <typeparam name="T">The type of the generic Task instantiation</typeparam>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <param name="instrumentedMethod">A delegate for the method we are instrumenting</param>
        /// <returns>A task with the result</returns>
        private static async Task<T> ExecuteReaderAsyncInternal<T>(
            DbCommand command,
            CancellationToken cancellationToken,
            Func<DbCommand, CancellationToken, object> instrumentedMethod)
        {
            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, command))
            {
                try
                {
                    var task = (Task<T>)instrumentedMethod(command, cancellationToken);
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
        /// Instrumentation wrapper for NpgsqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken).
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="behavior">The <see cref="CommandBehavior"/> value used in the original method call.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReaderAsync,
            TargetAssemblies = new[] { NpgsqlAssemblyName },
            TargetType = NpgsqlCommandTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<Npgsql.NpgsqlDataReader>", AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteReaderAsyncWithBehaviorAndCancellation(
            object command,
            int behavior,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteReaderAsync;
            var cancellationToken = (CancellationToken)boxedCancellationToken;
            var commandBehavior = (CommandBehavior)behavior;

            Type npgsqlComandType;
            Type npgsqlDataReaderType;

            try
            {
                npgsqlComandType = command.GetInstrumentedType(NpgsqlCommandTypeName);
                npgsqlDataReaderType = npgsqlComandType.Assembly.GetType(NpgsqlDataReaderTypeName);
            }
            catch (Exception ex)
            {
                // This shouldn't happen because the assembly holding the Npgsql.NpgsqlDataReader type should have been loaded already
                // profiled app will not continue working as expected without this method
                Log.Error(ex, "Error finding the Npgsql.NpgsqlDataReader type");
                throw;
            }

            Func<DbCommand, CommandBehavior, CancellationToken, object> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(NpgsqlCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, CommandBehavior, CancellationToken, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
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
                    instrumentedType: NpgsqlCommandTypeName,
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            return AsyncHelper.InvokeGenericTaskDelegate(
                owningType: command.GetType(),
                taskResultType: npgsqlDataReaderType,
                nameOfIntegrationMethod: nameof(ExecuteReaderAsyncWithBehaviorAndCancellationInternal),
                integrationType: typeof(NpgsqlCommandIntegration),
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
        /// <param name="commandBehavior">The command behavior</param>
        /// <param name="cancellationToken">The cancellation token</param>
        /// <param name="instrumentedMethod">A delegate for the method we are instrumenting</param>
        /// <returns>A task with the result</returns>
        private static async Task<T> ExecuteReaderAsyncWithBehaviorAndCancellationInternal<T>(
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
        /// Instrumentation wrapper for NpgsqlCommand.ExecuteNonQuery().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { NpgsqlAssemblyName },
            TargetType = NpgsqlCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Int32 },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static int ExecuteNonQuery(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteNonQuery;
            Func<DbCommand, int> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(NpgsqlCommandTypeName);

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
                    instrumentedType: NpgsqlCommandTypeName,
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
        /// Instrumentation wrapper for NpgsqlCommand.ExecuteNonQueryAsync(CancellationToken).
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { NpgsqlAssemblyName },
            TargetType = NpgsqlCommandTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Int32>", ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteNonQueryAsync(
            object command,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            var cancellationToken = (CancellationToken)boxedCancellationToken;

            return ExecuteNonQueryAsyncInternal(
                command as DbCommand,
                cancellationToken,
                opCode,
                mdToken,
                moduleVersionPtr);
        }

        private static async Task<int> ExecuteNonQueryAsyncInternal(
            DbCommand command,
            CancellationToken cancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteNonQueryAsync;
            Func<DbCommand, CancellationToken, Task<int>> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(NpgsqlCommandTypeName);

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
                    instrumentedType: NpgsqlCommandTypeName,
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
        /// Instrumentation wrapper for NpgsqlCommand.ExecuteScalar().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { NpgsqlAssemblyName },
            TargetType = NpgsqlCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Object },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteScalar(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteScalar;
            Func<DbCommand, object> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(NpgsqlCommandTypeName);

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
                    instrumentedType: NpgsqlCommandTypeName,
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
        /// Instrumentation wrapper for NpgsqlCommand.ExecuteScalarAsync(CancellationToken).
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { NpgsqlAssemblyName },
            TargetType = NpgsqlCommandTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Object>", ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteScalarAsync(
            object command,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            var cancellationToken = (CancellationToken)boxedCancellationToken;

            return ExecuteScalarAsyncInternal(
                command as DbCommand,
                cancellationToken,
                opCode,
                mdToken,
                moduleVersionPtr);
        }

        private static async Task<object> ExecuteScalarAsyncInternal(
            DbCommand command,
            CancellationToken cancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteScalarAsync;
            Func<DbCommand, CancellationToken, Task<object>> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(NpgsqlCommandTypeName);

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
                    instrumentedType: NpgsqlCommandTypeName,
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
