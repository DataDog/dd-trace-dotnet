using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;

// ReSharper disable MethodHasAsyncOverloadWithCancellation
// ReSharper disable MethodSupportsCancellation

namespace Samples.DatabaseHelper
{
    public static class RelationalDatabaseTestHarness
    {
        public static async Task RunAllAsync(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            IDbCommandExecutor commandExecutor,
            CancellationToken cancellationToken)
        {
            using (var root = Tracer.Instance.StartActive("root"))
            {
                root.Span.ResourceName = commandExecutor.CommandTypeName;

                await RunAsync(connection, commandFactory, commandExecutor, runAsyncMethods: true, cancellationToken);

                var dbCommandExecutor = new DbCommandClassExecutor();
                await RunAsync(connection, commandFactory, dbCommandExecutor, runAsyncMethods: true, cancellationToken);

                var idbCommandExecutor = new DbCommandInterfaceExecutor();
                await RunAsync(connection, commandFactory, idbCommandExecutor, runAsyncMethods: false, cancellationToken);

#if !NET45
                // use DbCommandWrapper to reference DbCommand in netstandard.dll
                var dbCommandWrapperExecutor = new DbCommandNetStandardClassExecutor();
                await RunAsync(connection, commandFactory, dbCommandWrapperExecutor, runAsyncMethods: true, cancellationToken);

                // use IDbCommandWrapper to reference IDbCommand in netstandard.dll
                var idbCommandWrapperExecutor = new DbCommandNetStandardInterfaceExecutor();
                await RunAsync(connection, commandFactory, idbCommandWrapperExecutor, runAsyncMethods: false, cancellationToken);
#endif
            }
        }

        private static async Task RunAsync(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            IDbCommandExecutor commandExecutor,
            bool runAsyncMethods,
            CancellationToken cancellationToken)
        {
            string commandName = commandExecutor.CommandTypeName;
            Console.WriteLine(commandName);

            connection.Open();

            using (var parentScope = Tracer.Instance.StartActive("command"))
            {
                parentScope.Span.ResourceName = commandName;
                IDbCommand command;

                using (var scope = Tracer.Instance.StartActive("sync"))
                {
                    scope.Span.ResourceName = commandName;

                    Console.WriteLine("  Synchronous");
                    Console.WriteLine();
                    await Task.Delay(100, cancellationToken);

                    command = commandFactory.GetCreateTableCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);

                    command = commandFactory.GetInsertRowCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);

                    command = commandFactory.GetSelectScalarCommand(connection);
                    commandExecutor.ExecuteScalar(command);

                    command = commandFactory.GetUpdateRowCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);

                    command = commandFactory.GetSelectRowCommand(connection);
                    commandExecutor.ExecuteReader(command);

                    command = commandFactory.GetSelectRowCommand(connection);
                    commandExecutor.ExecuteReader(command, CommandBehavior.Default);

                    command = commandFactory.GetDeleteRowCommand(connection);
                    commandExecutor.ExecuteNonQuery(command);
                }

                if (runAsyncMethods)
                {
                    await Task.Delay(100, cancellationToken);

                    using (var scope = Tracer.Instance.StartActive("async"))
                    {
                        scope.Span.ResourceName = commandName;

                        Console.WriteLine("  Asynchronous");
                        Console.WriteLine();
                        await Task.Delay(100, cancellationToken);

                        command = commandFactory.GetCreateTableCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command);

                        command = commandFactory.GetInsertRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command);

                        command = commandFactory.GetSelectScalarCommand(connection);
                        await commandExecutor.ExecuteScalarAsync(command);

                        command = commandFactory.GetUpdateRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command);

                        command = commandFactory.GetSelectRowCommand(connection);
                        await commandExecutor.ExecuteReaderAsync(command);

                        command = commandFactory.GetSelectRowCommand(connection);
                        await commandExecutor.ExecuteReaderAsync(command, CommandBehavior.Default);

                        command = commandFactory.GetDeleteRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command);
                    }

                    await Task.Delay(100, cancellationToken);

                    using (var scope = Tracer.Instance.StartActive("async-with-cancellation"))
                    {
                        scope.Span.ResourceName = commandName;

                        Console.WriteLine("  Asynchronous with cancellation");
                        Console.WriteLine();
                        await Task.Delay(100, cancellationToken);

                        command = commandFactory.GetCreateTableCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                        command = commandFactory.GetInsertRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                        command = commandFactory.GetSelectScalarCommand(connection);
                        await commandExecutor.ExecuteScalarAsync(command, cancellationToken);

                        command = commandFactory.GetUpdateRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                        command = commandFactory.GetSelectRowCommand(connection);
                        await commandExecutor.ExecuteReaderAsync(command, cancellationToken);

                        command = commandFactory.GetSelectRowCommand(connection);
                        await commandExecutor.ExecuteReaderAsync(command, CommandBehavior.Default, cancellationToken);

                        command = commandFactory.GetDeleteRowCommand(connection);
                        await commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);
                    }
                }
            }

            connection.Close();
            await Task.Delay(100, cancellationToken);
        }
    }
}
