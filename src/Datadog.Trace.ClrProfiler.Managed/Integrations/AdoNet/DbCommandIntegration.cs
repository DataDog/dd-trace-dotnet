using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.AdoNet
{
    /// <summary>
    /// Instrumentation wrappers for <see cref="DbCommand"/>.
    /// </summary>
    public static class DbCommandIntegration
    {
        private const string Major2 = "2";
        private const string Major4 = "4";
        private const string Major5 = "5";

        private const string DbCommandTypeName = AdoNetConstants.TypeNames.DbCommand;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(DbCommandIntegration));

        /// <summary>
        /// Instrumentation wrapper for <see cref="DbCommand.ExecuteReader()"/>.
        /// </summary>
        /// <param name="command">The object referenced "this" in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.DbDataReader },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.DbDataReader },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.NetStandard },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.DbDataReader },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static object ExecuteReader(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteReader;
            Func<DbCommand, DbDataReader> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(DbCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, DbDataReader>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithNamespaceAndNameFilters(AdoNetConstants.TypeNames.DbDataReader)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: DbCommandTypeName,
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
        /// Instrumentation wrapper for <see cref="DbCommand.ExecuteReader(CommandBehavior)"/>.
        /// </summary>
        /// <param name="command">The object referenced "this" in the instrumented method.</param>
        /// <param name="behavior">The <see cref="CommandBehavior"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.DbDataReader, AdoNetConstants.TypeNames.CommandBehavior },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.DbDataReader, AdoNetConstants.TypeNames.CommandBehavior },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.NetStandard },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.DbDataReader, AdoNetConstants.TypeNames.CommandBehavior },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static object ExecuteReaderWithBehavior(
            object command,
            int behavior,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteReader;
            var commandBehavior = (CommandBehavior)behavior;
            Func<DbCommand, CommandBehavior, DbDataReader> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(DbCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, CommandBehavior, DbDataReader>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithParameters(commandBehavior)
                       .WithNamespaceAndNameFilters(AdoNetConstants.TypeNames.DbDataReader, AdoNetConstants.TypeNames.CommandBehavior)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: DbCommandTypeName,
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            var dbCommand = command as DbCommand;

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, dbCommand))
            {
                try
                {
                    return instrumentedMethod(dbCommand, commandBehavior);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for <see cref="DbCommand.ExecuteReaderAsync()"/>.
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="behavior">The <see cref="CommandBehavior"/> value used in the original method call.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Data.Common.DbDataReader>", AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.NetStandard },
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Data.Common.DbDataReader>", AdoNetConstants.TypeNames.CommandBehavior, ClrNames.CancellationToken },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static object ExecuteReaderAsync(
            object command,
            int behavior,
            object boxedCancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            var cancellationToken = (CancellationToken)boxedCancellationToken;

            return ExecuteReaderAsyncInternal(
                command as DbCommand,
                (CommandBehavior)behavior,
                cancellationToken,
                opCode,
                mdToken,
                moduleVersionPtr);
        }

        private static async Task<DbDataReader> ExecuteReaderAsyncInternal(
            DbCommand command,
            CommandBehavior commandBehavior,
            CancellationToken cancellationToken,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteReaderAsync;
            Func<DbCommand, CommandBehavior, CancellationToken, Task<DbDataReader>> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedType(DbCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<DbCommand, CommandBehavior, CancellationToken, Task<DbDataReader>>>
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
                    instrumentedType: DbCommandTypeName,
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, command))
            {
                try
                {
                    return await instrumentedMethod(command, commandBehavior, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    scope?.Span.SetException(ex);
                    throw;
                }
            }
        }

        /// <summary>
        /// Instrumentation wrapper for <see cref="DbCommand.ExecuteNonQuery"/>.
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteNonQuery,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Int32 },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteNonQuery,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Int32 },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.NetStandard },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteNonQuery,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Int32 },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
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
                var targetType = command.GetInstrumentedType(DbCommandTypeName);

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
                    instrumentedType: DbCommandTypeName,
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
        /// Instrumentation wrapper for <see cref="DbCommand.ExecuteNonQueryAsync(CancellationToken)"/>
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Int32>", ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.NetStandard },
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Int32>", ClrNames.CancellationToken },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
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
                var targetType = command.GetInstrumentedType(DbCommandTypeName);

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
                    instrumentedType: DbCommandTypeName,
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
        /// Instrumentation wrapper for <see cref="DbCommand.ExecuteScalar"/>
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteScalar,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Object },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteScalar,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Object },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.NetStandard },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteScalar,
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Object },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
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
                var targetType = command.GetInstrumentedType(DbCommandTypeName);

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
                    instrumentedType: DbCommandTypeName,
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
        /// Instrumentation wrapper for <see cref="DbCommand.ExecuteScalarAsync(CancellationToken)"/>
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="boxedCancellationToken">The <see cref="CancellationToken"/> value used in the original method call.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Object>", ClrNames.CancellationToken },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.NetStandard },
            TargetType = DbCommandTypeName,
            TargetSignatureTypes = new[] { "System.Threading.Tasks.Task`1<System.Object>", ClrNames.CancellationToken },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
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
                var targetType = command.GetInstrumentedType(DbCommandTypeName);

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
                    instrumentedType: DbCommandTypeName,
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
