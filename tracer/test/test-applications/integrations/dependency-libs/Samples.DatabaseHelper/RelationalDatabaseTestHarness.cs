using System;
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
#if NET6_0_OR_GREATER
using System.Data.Common;
#endif

// ReSharper disable MethodHasAsyncOverloadWithCancellation
// ReSharper disable MethodSupportsCancellation

namespace Samples.DatabaseHelper
{
    public static class RelationalDatabaseTestHarness
    {
        /// <summary>
        /// Helper method that runs ADO.NET test suite for the specified <see cref="IDbCommandExecutor"/>
        /// in addition to other built-in implementations.
        /// Does NOT run batch commands, this is done in <see cref="RunBatchAsync"/>
        /// </summary>
        /// <param name="connection">The <see cref="IDbConnection"/> to use to connect to the database.</param>
        /// <param name="commandFactory">A <see cref="DbCommandFactory"/> implementation specific to an ADO.NET provider, e.g. SqlCommand, NpgsqlCommand.</param>
        /// <param name="providerSpecificCommandExecutor">A <see cref="IDbCommandExecutor"/> specific to an ADO.NET provider, e.g. SqlCommand, NpgsqlCommand, used to call DbCommand methods.</param>
        /// <param name="cancellationToken">A cancellation token passed into downstream async methods.</param>
        /// <param name="useTransactionScope">Whether or not a Transcation Scope should be created.</param>
        /// <typeparam name="TCommand">The DbCommand implementation specific to an ADO.NET provider, e.g. SqlCommand, NpgsqlCommand.</typeparam>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync<TCommand>(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            IDbCommandExecutor providerSpecificCommandExecutor,
            CancellationToken cancellationToken,
            bool useTransactionScope = true)
            where TCommand : IDbCommand
        {
            var executors = new List<IDbCommandExecutor>
                            {
                                // call methods directly like SqlCommand.ExecuteScalar(), provided by caller
                                providerSpecificCommandExecutor,

                                // call methods through DbCommand reference
                                new DbCommandClassExecutor(),

                                // call methods through IDbCommand reference
                                new DbCommandInterfaceExecutor(),

                                // call methods through IDbCommand reference, but using a generic constraint
                                new DbCommandInterfaceGenericExecutor<TCommand>(),

                                // call methods through DbCommand reference (referencing netstandard.dll)
                                new DbCommandNetStandardClassExecutor(),

                                // call methods through IDbCommand reference (referencing netstandard.dll)
                                new DbCommandNetStandardInterfaceExecutor(),

                                // call methods through IDbCommand reference (referencing netstandard.dll), but using a generic constraint
                                new DbCommandNetStandardInterfaceGenericExecutor<TCommand>(),
                            };

            using (var root = SampleHelpers.CreateScope("RunAllAsync<TCommand>"))
            {
                foreach (var executor in executors)
                {
                    await RunAsync(connection, commandFactory, executor, cancellationToken, useTransactionScope);
                }
            }
        }

        /// <summary>
        /// Helper method that runs ADO.NET test suite for the built-in implementations.
        /// </summary>
        /// <param name="connection">The <see cref="IDbConnection"/> to use to connect to the database.</param>
        /// <param name="commandFactory">A <see cref="DbCommandFactory"/> implementation specific to an ADO.NET provider, e.g. SqlCommand, NpgsqlCommand.</param>
        /// <param name="cancellationToken">A cancellation token passed into downstream async methods.</param>
        /// <param name="useTransactionScope">Whether or not a Transcation Scope should be created.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunBaseClassesAsync(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            CancellationToken cancellationToken,
            bool useTransactionScope = true)
        {
            var executors = new List<IDbCommandExecutor>
                            {
                                // call methods through DbCommand reference
                                new DbCommandClassExecutor(),

                                // call methods through IDbCommand reference
                                new DbCommandInterfaceExecutor(),

                                // call methods through DbCommand reference (referencing netstandard.dll)
                                new DbCommandNetStandardClassExecutor(),

                                // call methods through IDbCommand reference (referencing netstandard.dll)
                                new DbCommandNetStandardInterfaceExecutor(),
                            };

            using (var root = SampleHelpers.CreateScope("RunBaseClassesAsync"))
            {
                foreach (var executor in executors)
                {
                    await RunAsync(connection, commandFactory, executor, cancellationToken, useTransactionScope);
                }
            }
        }

        public static async Task RunSingleAsync(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            IDbCommandExecutor providerSpecificCommandExecutor,
            CancellationToken cancellationToken,
            bool useTransactionScope = true)
        {
            var executors = new List<IDbCommandExecutor>
                            {
                                providerSpecificCommandExecutor
                            };

            using (var root = SampleHelpers.CreateScope("RunSingleAsync"))
            {
                foreach (var executor in executors)
                {
                    await RunAsync(connection, commandFactory, executor, cancellationToken, useTransactionScope);
                }
            }
        }

        /// <summary>
        /// Helper method that runs ADO.NET test suite for the specified <see cref="IDbCommandExecutor"/>
        /// in addition to other built-in implementations.
        /// </summary>
        /// <param name="connection">The <see cref="IDbConnection"/> to use to connect to the database.</param>
        /// <param name="commandFactory">A <see cref="DbCommandFactory"/> implementation specific to an ADO.NET provider, e.g. SqlCommand, NpgsqlCommand.</param>
        /// <param name="cancellationToken">A cancellation token passed into downstream async methods.</param>
        /// <param name="providerSpecificCommandExecutors">A list of instantiated <see cref="IDbCommandExecutor"/> objects to directly control the ADO.NET providers tested.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            CancellationToken cancellationToken,
            bool useTransactionScope = true,
            params IDbCommandExecutor[] providerSpecificCommandExecutors)
        {
            using (var root = SampleHelpers.CreateScope("RunAllAsync"))
            {
                foreach (var executor in providerSpecificCommandExecutors)
                {
                    await RunAsync(connection, commandFactory, executor, cancellationToken, useTransactionScope);
                }
            }
        }

        /// <summary>
        /// Runs ADO.NET test suite for the specified <see cref="IDbCommandExecutor"/>.
        /// </summary>
        /// <param name="connection">The <see cref="IDbConnection"/> to use to connect to the database.</param>
        /// <param name="commandFactory">A <see cref="DbCommandFactory"/> implementation specific to an ADO.NET provider, e.g. SqlCommand, NpgsqlCommand.</param>
        /// <param name="commandExecutor">A <see cref="IDbCommandExecutor"/> used to call DbCommand methods.</param>
        /// <param name="cancellationToken">A cancellation token passed into downstream async methods.</param>
        /// <param name="useTransactionScope">Whether or not to start a transaction</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private static async Task RunAsync(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            IDbCommandExecutor commandExecutor,
            CancellationToken cancellationToken,
            bool useTransactionScope = true)
        {
            string commandName = commandExecutor.CommandTypeName;
            Console.WriteLine(commandName);

            using (var parentScope = SampleHelpers.CreateScope("command"))
            {
                SampleHelpers.TrySetResourceName(parentScope, commandName);
                IDbCommand command;

                using (var scope = SampleHelpers.CreateScope("sync"))
                {
                    SampleHelpers.TrySetResourceName(scope, commandName);

                    Console.WriteLine("  Synchronous");
                    Console.WriteLine();

                    using var transaction = useTransactionScope ? connection.BeginTransaction() : null;
                    command = commandFactory.GetCreateTableCommand(connection, transaction);
                    commandExecutor.ExecuteNonQuery(command);

                    command = commandFactory.GetInsertRowCommand(connection, transaction);
                    commandExecutor.ExecuteNonQuery(command);

                    command = commandFactory.GetSelectScalarCommand(connection, transaction);
                    commandExecutor.ExecuteScalar(command);

                    command = commandFactory.GetUpdateRowCommand(connection, transaction);
                    commandExecutor.ExecuteNonQuery(command);

                    command = commandFactory.GetSelectRowCommand(connection, transaction);
                    commandExecutor.ExecuteReader(command);

                    command = commandFactory.GetSelectRowCommand(connection, transaction);
                    commandExecutor.ExecuteReader(command, CommandBehavior.Default);

                    command = commandFactory.GetDeleteRowCommand(connection, transaction);
                    commandExecutor.ExecuteNonQuery(command);
                }

                if (commandExecutor.SupportsAsyncMethods)
                {
                    using (var scope = SampleHelpers.CreateScope("async"))
                    {
                        SampleHelpers.TrySetResourceName(scope, commandName);

                        Console.WriteLine("  Asynchronous");
                        Console.WriteLine();

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

                    using (var scope = SampleHelpers.CreateScope("async-with-cancellation"))
                    {
                        SampleHelpers.TrySetResourceName(scope, commandName);

                        Console.WriteLine("  Asynchronous with cancellation");
                        Console.WriteLine();

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
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Runs batch command tests for the specified batch command handler.
        /// Not all DBs support this, and it's only available on .NET 6.0+
        /// </summary>
        public static async Task RunBatchAsync(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            IBatchCommandHandler batchCommandHandler,
            CancellationToken cancellationToken)
        {
            var batchName = batchCommandHandler.BatchTypeName;
            Console.WriteLine($"BATCH command: {batchName}");

            using (var parentScope = SampleHelpers.CreateScope("batch"))
            {
                SampleHelpers.TrySetResourceName(parentScope, batchName);

                using (var scope = SampleHelpers.CreateScope("sync"))
                {
                    SampleHelpers.TrySetResourceName(scope, batchName);

                    Console.WriteLine("  Synchronous batch");
                    Console.WriteLine();

                    var batch = CreateStandardBatch(connection, commandFactory, batchCommandHandler);
                    batchCommandHandler.ExecuteBatch(batch);
                }

                using (var scope = SampleHelpers.CreateScope("async"))
                {
                    SampleHelpers.TrySetResourceName(scope, batchName);

                    Console.WriteLine("  Asynchronous batch");
                    Console.WriteLine();

                    var batch = CreateStandardBatch(connection, commandFactory, batchCommandHandler);
                    await batchCommandHandler.ExecuteBatchAsync(batch);
                }

                using (var scope = SampleHelpers.CreateScope("async-with-cancellation"))
                {
                    SampleHelpers.TrySetResourceName(scope, batchName);

                    Console.WriteLine("  Asynchronous batch with cancellation");
                    Console.WriteLine();

                    var batch = CreateStandardBatch(connection, commandFactory, batchCommandHandler);
                    await batchCommandHandler.ExecuteBatchAsync(batch, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Creates a commands batch containing first a (DROP TABLE, CREATE TABLE), and then an INSERT command
        /// </summary>
        private static DbBatch CreateStandardBatch(
            IDbConnection connection,
            DbCommandFactory commandFactory,
            IBatchCommandHandler batchCommandHandler)
        {
            var batch = batchCommandHandler.CreateBatch(connection);

            batch.BatchCommands.Add(batchCommandHandler.CreateBatchCommand(commandFactory.GetCreateTableCommand(connection)));
            batch.BatchCommands.Add(batchCommandHandler.CreateBatchCommand(commandFactory.GetInsertRowCommand(connection)));

            return batch;
        }
#endif
    }
}
