using System;
using System.Data;
using System.Data.Common;
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
        private const string IntegrationName = "AdoNet";
        private const string Major4 = "4";

        // ReSharper disable InconsistentNaming
        private const string IDbCommandTypeName = "System.Data.IDbCommand";
        private const string IDataReaderTypeName = "System.Data.IDataReader";
        // ReSharper restore InconsistentNaming

        private static readonly Vendors.Serilog.ILogger Log = DatadogLogging.GetLogger(typeof(IDbCommandIntegration));

        /// <summary>
        /// Instrumentation wrapper for <see cref="IDbCommand.ExecuteReader()"/>.
        /// </summary>
        /// <param name="command">The object referenced "this" in the instrumented method.</param>
        /// <param name="opCode">The OpCode used in the original method call.</param>
        /// <param name="mdToken">The mdToken of the original method call.</param>
        /// <param name="moduleVersionPtr">A pointer to the module version GUID.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { IDataReaderTypeName },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteReader(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<IDbCommand, IDataReader> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<IDbCommand, IDataReader>>
                       .Start(moduleVersionPtr, mdToken, opCode, AdoNetConstants.MethodNames.ExecuteReader)
                       .WithConcreteType(typeof(IDbCommand))
                       .WithNamespaceAndNameFilters(IDataReaderTypeName)
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
                    methodName: nameof(ExecuteReader),
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            var dbCommand = command as IDbCommand;

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, dbCommand, IntegrationName))
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
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetMethod = AdoNetConstants.MethodNames.ExecuteReader,
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { IDataReaderTypeName, AdoNetConstants.TypeNames.CommandBehavior },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteReaderWithBehavior(
            object command,
            int behavior,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<IDbCommand, CommandBehavior, IDataReader> instrumentedMethod;
            var commandBehavior = (CommandBehavior)behavior;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<IDbCommand, CommandBehavior, IDataReader>>
                       .Start(moduleVersionPtr, mdToken, opCode, AdoNetConstants.MethodNames.ExecuteReader)
                       .WithConcreteType(typeof(IDbCommand))
                       .WithParameters(commandBehavior)
                       .WithNamespaceAndNameFilters(IDataReaderTypeName, AdoNetConstants.TypeNames.CommandBehavior)
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
                    methodName: nameof(ExecuteReader),
                    instanceType: command.GetType().AssemblyQualifiedName);
                throw;
            }

            var dbCommand = command as IDbCommand;

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, dbCommand, IntegrationName))
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
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Int32 },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static int ExecuteNonQuery(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<IDbCommand, int> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<IDbCommand, int>>
                       .Start(moduleVersionPtr, mdToken, opCode, AdoNetConstants.MethodNames.ExecuteNonQuery)
                       .WithConcreteType(typeof(IDbCommand))
                       .WithNamespaceAndNameFilters(ClrNames.Int32)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error resolving {IDbCommandTypeName}.{AdoNetConstants.MethodNames.ExecuteNonQuery}(...)");
                throw;
            }

            var dbCommand = command as IDbCommand;

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, dbCommand, IntegrationName))
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
            TargetAssemblies = new[] { AdoNetConstants.AssemblyNames.SystemData, AdoNetConstants.AssemblyNames.SystemDataCommon },
            TargetType = IDbCommandTypeName,
            TargetSignatureTypes = new[] { ClrNames.Object },
            TargetMinimumVersion = Major4,
            TargetMaximumVersion = Major4)]
        public static object ExecuteScalar(
            object command,
            int opCode,
            int mdToken,
            long moduleVersionPtr)
        {
            Func<IDbCommand, object> instrumentedMethod;

            try
            {
                instrumentedMethod =
                    MethodBuilder<Func<IDbCommand, object>>
                       .Start(moduleVersionPtr, mdToken, opCode, AdoNetConstants.MethodNames.ExecuteScalar)
                       .WithConcreteType(typeof(IDbCommand))
                       .WithNamespaceAndNameFilters(ClrNames.Object)
                       .Build();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Error resolving {IDbCommandTypeName}.{AdoNetConstants.MethodNames.ExecuteScalar}(...)");
                throw;
            }

            var dbCommand = command as IDbCommand;

            using (var scope = ScopeFactory.CreateDbCommandScope(Tracer.Instance, dbCommand, IntegrationName))
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
