using System;
using System.Data;
using Datadog.Trace.ClrProfiler.Emit;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations.AdoNet
{
    /// <summary>
    /// Instrumentation wrappers for <see cref="IDbCommand"/>.
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public static class IDbCommandIntegration
    {
        private const string Major2 = "2";
        private const string Major4 = "4";
        private const string Major5 = "5";

        // ReSharper disable once InconsistentNaming
        private const string IDbCommandTypeName = AdoNetConstants.TypeNames.IDbCommand;

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(IDbCommandIntegration));

        /// <summary>
        /// Instrumentation wrapper for <see cref="IDbCommand.ExecuteReader()"/>.
        /// </summary>
        /// <param name="command">The object referenced "this" in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData },
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.IDataReader },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.IDataReader },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.NetStandard },
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.IDataReader },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major2)]
        public static object ExecuteReader(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            const string methodName = AdoNetConstants.MethodNames.ExecuteReader;
            Func<IDbCommand, IDataReader> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedInterface(IDbCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<IDbCommand, IDataReader>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithNamespaceAndNameFilters(AdoNetConstants.TypeNames.IDataReader)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: IDbCommandTypeName,
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            var dbCommand = command as IDbCommand;

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
        /// Instrumentation wrapper for <see cref="IDbCommand.ExecuteReader(CommandBehavior)"/>.
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
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.IDataReader, AdoNetConstants.TypeNames.CommandBehavior },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.IDataReader, AdoNetConstants.TypeNames.CommandBehavior },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.NetStandard },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { AdoNetConstants.TypeNames.IDataReader, AdoNetConstants.TypeNames.CommandBehavior },
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
            Func<IDbCommand, CommandBehavior, IDataReader> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedInterface(IDbCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<IDbCommand, CommandBehavior, IDataReader>>
                       .Start(moduleVersionPtr, mdToken, opCode, methodName)
                       .WithConcreteType(targetType)
                       .WithParameters(commandBehavior)
                       .WithNamespaceAndNameFilters(AdoNetConstants.TypeNames.IDataReader, AdoNetConstants.TypeNames.CommandBehavior)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.ErrorRetrievingMethod(
                    exception: ex,
                    moduleVersionPointer: moduleVersionPtr,
                    mdToken: mdToken,
                    opCode: opCode,
                    instrumentedType: IDbCommandTypeName,
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            var dbCommand = command as IDbCommand;

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
        /// Instrumentation wrapper for IDbCommand.ExecuteNonQuery().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData },
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Int32 },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Int32 },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.NetStandard },
            TargetType = IDbCommandTypeName,
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
            Func<IDbCommand, int> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedInterface(IDbCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<IDbCommand, int>>
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
                    instrumentedType: IDbCommandTypeName,
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            var dbCommand = command as IDbCommand;

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
        /// Instrumentation wrapper for IDbCommand.ExecuteScalar().
        /// </summary>
        /// <param name="command">The object referenced by this in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData },
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Object },
            TargetMinimumVersion = Major2,
            TargetMaximumVersion = Major4)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Object },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major5)]
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.NetStandard },
            TargetType = IDbCommandTypeName,
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
            Func<IDbCommand, object> instrumentedMethod;

            try
            {
                var targetType = command.GetInstrumentedInterface(IDbCommandTypeName);

                instrumentedMethod =
                    MethodBuilder<Func<IDbCommand, object>>
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
                    instrumentedType: IDbCommandTypeName,
                    methodName: methodName,
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            var dbCommand = command as IDbCommand;

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
    }
}
