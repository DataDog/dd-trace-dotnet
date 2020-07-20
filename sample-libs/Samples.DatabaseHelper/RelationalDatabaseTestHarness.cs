using System;
using System.Collections.Generic;
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
            var executors = new List<IDbCommandExecutor>
                            {
                                commandExecutor,
                                new DbCommandClassExecutor(),
                                new DbCommandInterfaceExecutor(),
#if !NET45
                                // these ones reference types in netstandard.dll
                                new DbCommandNetStandardClassExecutor(),
                                new DbCommandNetStandardInterfaceExecutor(),
#endif
                            };

            using (var root = Tracer.Instance.StartActive("root"))
            {
                foreach (var executor in executors)
                {
                    await RunAsync(connection, commandFactory, executor, cancellationToken);
                }
            }
        }

        private static async Task RunAsync(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            IDbCommandExecutor commandExecutor,
            CancellationToken cancellationToken)
        {
            string commandName = commandExecutor.CommandTypeName;
            Console.WriteLine(commandName);

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

                if (commandExecutor.SupportsAsyncMethods)
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

            await Task.Delay(100, cancellationToken);
        }
    }
}
