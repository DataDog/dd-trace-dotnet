using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.ExtensionMethods;
using Datadog.Trace.ClrProfiler.Services;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Interfaces;
using Datadog.Trace.Logging;

namespace Datadog.Trace.ClrProfiler.Integrations
{
    /// <summary>
    ///     SqlServer handles tracing System.Data.SqlClient
    /// </summary>
    public static class AdoNetIntegration
    {
        private static readonly ILog Log = LogProvider.GetLogger(typeof(AdoNetIntegration));

        /// <summary>
        ///     Wrapper method that instruments <see cref="DbCommand.ExecuteDbDataReader" />.
        /// </summary>
        /// <param name="thisObj">The <see cref="DbCommand" /> that is references by the "this" pointer in the instrumented method.</param>
        /// <param name="behavior">A value from <see cref="CommandBehavior" />.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Data", // .NET Framework
            TargetType = "System.Data.Common.DbCommand")]
        [InterceptMethod(
            TargetAssembly = "System.Data.Common", // .NET Core
            TargetType = "System.Data.Common.DbCommand")]
        public static object ExecuteDbDataReader(object thisObj, int behavior)
        {
            var commandBehavior = (CommandBehavior)behavior;

            var executeReaderFunc = DynamicMethodBuilder<Func<object, CommandBehavior, object>>
               .GetOrCreateMethodCallDelegate(
                                              thisObj.GetType(),
                                              "ExecuteDbDataReader");

            if (!(thisObj is DbCommand dbCommand))
            {
                Log.DebugFormat("this object reference passed to ExecuteDbDataReaderAsync is a [{0}], not a DbCommand", thisObj.GetType());

                return executeReaderFunc(thisObj, commandBehavior);
            }

            return ExecuteDbOperation(dbCommand, db => executeReaderFunc(db, commandBehavior));
        }

        /// <summary>
        ///     Wrapper method that instruments <see cref="System.Data.Common.DbCommand.ExecuteDbDataReader" />.
        /// </summary>
        /// <param name="thisObj">The <see cref="DbCommand" /> that is references by the "this" pointer in the instrumented method.</param>
        /// <param name="behavior">A value from <see cref="CommandBehavior" />.</param>
        /// <param name="cancellationTokenSource">A cancellation token source that can be used to cancel the async operation.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Data", // .NET Framework
            TargetType = "System.Data.Common.DbCommand")]
        [InterceptMethod(
            TargetAssembly = "System.Data.Common", // .NET Core
            TargetType = "System.Data.Common.DbCommand")]
        public static object ExecuteDbDataReaderAsync(object thisObj, int behavior, object cancellationTokenSource)
        {
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            var commandBehavior = (CommandBehavior)behavior;

            var executeReaderFunc = DynamicMethodBuilder<Func<object, CommandBehavior, CancellationToken, Task<object>>>
               .GetOrCreateMethodCallDelegate(
                                              thisObj.GetType(),
                                              "ExecuteDbDataReaderAsync");

            if (!(thisObj is DbCommand dbCommand))
            {
                Log.DebugFormat("this object reference passed to ExecuteDbDataReaderAsync is a [{0}], not a DbCommand", thisObj.GetType());

                return executeReaderFunc(thisObj, commandBehavior, cancellationToken);
            }

            return ExecuteDbOperationAsync(dbCommand, db => executeReaderFunc(db, commandBehavior, cancellationToken));
        }

        /*
        /// <summary>
        ///     Wrapper method that instruments System.Data.SqlClient.SqlCommand.ExecuteReaderAsync(CommandBehavior, CancellationToken)" />.
        /// </summary>
        /// <param name="thisObj">The SqlCommand that is references by the "this" pointer in the instrumented method.</param>
        /// <param name="behavior">A value from <see cref="CommandBehavior" />.</param>
        /// <param name="cancellationTokenSource">A cancellation token source that can be used to cancel the async operation.</param>
        /// <returns>The value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Data", // .NET Framework
            TargetType = "System.Data.SqlClient.SqlCommand",
            CallerAssembly = "System.Data.SqlClient")]
        [InterceptMethod(
            TargetAssembly = "System.Data.SqlClient", // .NET Core
            TargetType = "System.Data.SqlClient.SqlCommand",
            CallerAssembly = "System.Data.SqlClient")]
        public static object ExecuteReaderAsync(object thisObj, int behavior, object cancellationTokenSource)
        {
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;
            var commandBehavior = (CommandBehavior)behavior;

            var executeReaderFunc = DynamicMethodBuilder<Func<object, CommandBehavior, CancellationToken, Task<object>>>
               .GetOrCreateMethodCallDelegate(
                                              thisObj.GetType(),
                                              "ExecuteReaderAsync");

            if (!(thisObj is DbCommand dbCommand))
            {
                Log.DebugFormat("this object reference passed to ExecuteReaderAsync is a [{0}], not a DbCommand", thisObj.GetType());

                return executeReaderFunc(thisObj, commandBehavior, cancellationToken);
            }

            return ExecuteDbOperationAsync(dbCommand, db => executeReaderFunc(db, commandBehavior, cancellationToken));
        }
        */

        /// <summary>
        ///     Wrapper method that instruments <see cref="DbCommand.ExecuteNonQuery" />.
        /// </summary>
        /// <param name="thisObj">The <see cref="DbCommand" /> begin instrumented.</param>
        /// <returns>The int value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Data", // .NET Framework
            TargetType = "System.Data.Common.DbCommand")]
        [InterceptMethod(
            TargetAssembly = "System.Data.Common", // .NET Core
            TargetType = "System.Data.Common.DbCommand")]
        public static int ExecuteNonQuery(object thisObj)
        {
            var execNonQueryFunc = DynamicMethodBuilder<Func<object, int>>.GetOrCreateMethodCallDelegate(
                                                                                                         thisObj.GetType(),
                                                                                                         "ExecuteNonQuery");

            if (!(thisObj is DbCommand dbCommand))
            {
                Log.DebugFormat("this object reference passed to ExecuteNonQuery is a [{0}], not a DbCommand", thisObj.GetType());

                return execNonQueryFunc(thisObj);
            }

            return ExecuteDbOperation(dbCommand, db => execNonQueryFunc(db));
        }

        /// <summary>
        ///     Wrapper method that instruments <see cref="DbCommand.ExecuteNonQueryAsync(CancellationToken)" />.
        /// </summary>
        /// <param name="thisObj">The <see cref="DbCommand" /> begin instrumented.</param>
        /// <param name="cancellationTokenSource">A cancellation token source that can be used to cancel the async operation.</param>
        /// <returns>The int value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Data", // .NET Framework
            TargetType = "System.Data.Common.DbCommand")]
        [InterceptMethod(
            TargetAssembly = "System.Data.Common", // .NET Core
            TargetType = "System.Data.Common.DbCommand")]
        public static object ExecuteNonQueryAsync(object thisObj, object cancellationTokenSource)
        {
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;

            var execNonQueryFunc = DynamicMethodBuilder<Func<object, CancellationToken, Task<int>>>.GetOrCreateMethodCallDelegate(
                                                                                                                                  thisObj.GetType(),
                                                                                                                                  "ExecuteNonQueryAsync");

            if (!(thisObj is DbCommand dbCommand))
            {
                Log.DebugFormat("this object reference passed to ExecuteNonQueryAsync is a [{0}], not a DbCommand", thisObj.GetType());

                return execNonQueryFunc(thisObj, cancellationToken);
            }

            return ExecuteDbOperationAsync(dbCommand, db => execNonQueryFunc(db, cancellationToken));
        }

        /// <summary>
        ///     Wrapper method that instruments <see cref="DbCommand.ExecuteScalar" />.
        /// </summary>
        /// <param name="thisObj">The <see cref="DbCommand" /> begin instrumented.</param>
        /// <returns>The int value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Data", // .NET Framework
            TargetType = "System.Data.Common.DbCommand")]
        [InterceptMethod(
            TargetAssembly = "System.Data.Common", // .NET Core
            TargetType = "System.Data.Common.DbCommand")]
        public static object ExecuteScalar(object thisObj)
        {
            var execScalarFunc = DynamicMethodBuilder<Func<object, object>>.GetOrCreateMethodCallDelegate(
                                                                                                          thisObj.GetType(),
                                                                                                          "ExecuteScalar");

            if (!(thisObj is DbCommand dbCommand))
            {
                Log.DebugFormat("this object reference passed to ExecuteScalar is a [{0}], not a DbCommand", thisObj.GetType());

                return execScalarFunc(thisObj);
            }

            var result = ExecuteDbOperation(dbCommand, db => execScalarFunc(db));

            return result;
        }

        /// <summary>
        ///     Wrapper method that instruments <see cref="DbCommand.ExecuteScalar" />.
        /// </summary>
        /// <param name="thisObj">The <see cref="DbCommand" /> begin instrumented.</param>
        /// <param name="cancellationTokenSource">A cancellation token that can be used to cancel the async operation.</param>
        /// <returns>The int value returned by the instrumented method.</returns>
        [InterceptMethod(
            TargetAssembly = "System.Data", // .NET Framework
            TargetType = "System.Data.Common.DbCommand")]
        [InterceptMethod(
            TargetAssembly = "System.Data.Common", // .NET Core
            TargetType = "System.Data.Common.DbCommand")]
        public static object ExecuteScalarAsync(object thisObj, object cancellationTokenSource)
        {
            var tokenSource = cancellationTokenSource as CancellationTokenSource;
            var cancellationToken = tokenSource?.Token ?? CancellationToken.None;

            var execScalarFunc = DynamicMethodBuilder<Func<object, CancellationToken, Task<object>>>.GetOrCreateMethodCallDelegate(
                                                                                                                                   thisObj.GetType(),
                                                                                                                                   "ExecuteScalar");

            if (!(thisObj is DbCommand dbCommand))
            {
                Log.DebugFormat("this object reference passed to ExecuteScalar is a [{0}], not a DbCommand", thisObj.GetType());

                return execScalarFunc(thisObj, cancellationToken);
            }

            return ExecuteDbOperationAsync(dbCommand, db => execScalarFunc(db, cancellationToken));
        }

        private static async Task<T> ExecuteDbOperationAsync<T>(DbCommand dbCommand, Func<DbCommand, Task<T>> block)
        {
            var result = await ExecuteDbOperation(dbCommand, async db => await block(db).ConfigureAwait(false)).ConfigureAwait(false);

            return result;
        }

        private static T ExecuteDbOperation<T>(DbCommand dbCommand, Func<DbCommand, T> block)
        {
            var rethrowException = false;

            try
            {
                using (var scope = CreateScope(dbCommand))
                {
                    DecorateSpan(scope.Span, dbCommand);

                    // At this point the exception is rethrown to the app
                    rethrowException = true;

                    try
                    {
                        return block(dbCommand);
                    }
                    catch (Exception ex) when (scope.Span.SetExceptionAndReturnFalse(ex))
                    {
                        // This will never get hit...
                        throw;
                    }
                }
            }
            catch (Exception ex) when (!rethrowException)
            {
                Log.ErrorException("Datadog ADO.NET instrumentation error", ex);

                return block(dbCommand);
            }
        }

        private static Scope CreateScope(IDbCommand command)
        {
            var dbTagName = command.ToTagName();
            var serviceName = $"{Tracer.Instance.DefaultServiceName}-{dbTagName}";
            var operationName = $"{dbTagName}.query";

            return Tracer.Instance.StartActive(operationName, serviceName: serviceName);
        }

        private static void DecorateSpan(ISpan span, IDbCommand dbCommand)
        {
            var decorator = DefaultSpanDecorationBuilder.Create()
                                                        .With(dbCommand.AllDbCommandSpanDecorator())
                                                        .Build();

            span.DecorateWith(decorator);
        }
    }
}
