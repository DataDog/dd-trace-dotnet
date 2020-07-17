using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.DatabaseHelper
{
    // ReSharper disable MethodSupportsCancellation
    // ReSharper disable MethodHasAsyncOverloadWithCancellation
    public static class RelationalDatabaseTestHarness
    {
        public static async Task RunAllAsync<TConnection, TCommand, TDataReader>(
            TConnection connection,
            DbCommandFactory commandFactory,
            DbCommandExecutor<TCommand, TDataReader> commandExecutor,
            CancellationToken cancellationToken)
            where TConnection : class, IDbConnection
            where TCommand : class, IDbCommand
            where TDataReader : class, IDataReader
        {
            using (var root = Tracer.Instance.StartActive("root"))
            {
                await RunAsync(connection, commandFactory, commandExecutor, cancellationToken);

                var dbCommandExecutor = DbCommandExecutor.GetDbCommandExecutor();
                await RunAsync(connection, commandFactory, dbCommandExecutor, cancellationToken);

                var idbCommandExecutor = DbCommandExecutor.GetIDbCommandExecutor();
                await RunAsync(connection, commandFactory, idbCommandExecutor, cancellationToken);

#if !NET45
                // use DbCommandWrapper to reference DbCommand in netstandard.dll
                var dbCommandWrapperExecutor = DbCommandExecutor.GetDbWrapperExecutor();
                await RunAsync(connection, commandFactory, dbCommandWrapperExecutor, cancellationToken);

                // use IDbCommandWrapper to reference IDbCommand in netstandard.dll
                var idbCommandWrapperExecutor = DbCommandExecutor.GetDbWrapperExecutor();
                await RunAsync(connection, commandFactory, idbCommandWrapperExecutor, cancellationToken);
#endif
            }
        }

        private static async Task RunAsync<TConnection, TCommand, TDataReader>(
            TConnection connection,
            DbCommandFactory commandFactory,
            DbCommandExecutor<TCommand, TDataReader> commandExecutor,
            CancellationToken cancellationToken)
            where TConnection : class, IDbConnection
            where TCommand : class, IDbCommand
            where TDataReader : class, IDataReader
        {
            string commandType = typeof(TCommand).Name;
            Console.WriteLine(commandExecutor.DbCommandType.AssemblyQualifiedName);

            using (var parentScope = Tracer.Instance.StartActive(commandType))
            {
                parentScope.Span.SetTag("command-type", commandType);
                connection.Open();
                TCommand command;

                using (var scope = Tracer.Instance.StartActive("run.sync"))
                {
                    Console.WriteLine("  Synchronous");
                    Console.WriteLine();
                    scope.Span.SetTag("command-type", commandType);
                    await Task.Delay(100, cancellationToken);

                    command = (TCommand)commandFactory.GetCreateTableCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);

                    command = (TCommand)commandFactory.GetInsertRowCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);

                    command = (TCommand)commandFactory.GetSelectScalarCommand(connection);
                    commandExecutor.ExecuteScalar(command);

                    command = (TCommand)commandFactory.GetUpdateRowCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);

                    command = (TCommand)commandFactory.GetSelectRowCommand(connection);
                    commandExecutor.ExecuteReader(command);

                    command = (TCommand)commandFactory.GetSelectRowCommand(connection);
                    commandExecutor.ExecuteReader(command, CommandBehavior.Default);

                    command = (TCommand)commandFactory.GetDeleteRowCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);
                }

                await Task.Delay(100, cancellationToken);

                using (var scope = Tracer.Instance.StartActive("run.async"))
                {
                    Console.WriteLine("  Asynchronous");
                    Console.WriteLine();
                    scope.Span.SetTag("command-type", commandType);
                    await Task.Delay(100, cancellationToken);

                    command = (TCommand)commandFactory.GetCreateTableCommand(connection);
                    await commandExecutor.ExecuteNonQueryAsync(command);

                    command = (TCommand)commandFactory.GetInsertRowCommand(connection);
                    await commandExecutor.ExecuteNonQueryAsync(command);

                    command = (TCommand)commandFactory.GetSelectScalarCommand(connection);
                    await commandExecutor.ExecuteScalarAsync(command);

                    command = (TCommand)commandFactory.GetUpdateRowCommand(connection);
                    await commandExecutor.ExecuteNonQueryAsync(command);

                    command = (TCommand)commandFactory.GetSelectRowCommand(connection);
                    await commandExecutor.ExecuteReaderAsync(command);

                    command = (TCommand)commandFactory.GetSelectRowCommand(connection);
                    await commandExecutor.ExecuteReaderAsync(command, CommandBehavior.Default);

                    command = (TCommand)commandFactory.GetDeleteRowCommand(connection);
                    await commandExecutor.ExecuteNonQueryAsync(command);
                }

                await Task.Delay(100, cancellationToken);

                using (var scope = Tracer.Instance.StartActive("run.async.with-cancellation"))
                {
                    Console.WriteLine(" Asynchronous with cancellation");
                    Console.WriteLine();
                    scope.Span.SetTag("command-type", commandType);
                    await Task.Delay(100, cancellationToken);

                    command = (TCommand)commandFactory.GetCreateTableCommand(connection);
                    await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                    command = (TCommand)commandFactory.GetInsertRowCommand(connection);
                    await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                    command = (TCommand)commandFactory.GetSelectScalarCommand(connection);
                    await commandExecutor.ExecuteScalarAsync(command, cancellationToken);

                    command = (TCommand)commandFactory.GetUpdateRowCommand(connection);
                    await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                    command = (TCommand)commandFactory.GetSelectRowCommand(connection);
                    await commandExecutor.ExecuteReaderAsync(command, cancellationToken);

                    command = (TCommand)commandFactory.GetSelectRowCommand(connection);
                    await commandExecutor.ExecuteReaderAsync(command, CommandBehavior.Default, cancellationToken);

                    command = (TCommand)commandFactory.GetDeleteRowCommand(connection);
                    await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);
                }

                connection.Close();
            }

            Console.WriteLine();
            await Task.Delay(100, cancellationToken);
        }
    }
}
