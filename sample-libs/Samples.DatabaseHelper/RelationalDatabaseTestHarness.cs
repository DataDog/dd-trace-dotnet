using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace;

namespace Samples.DatabaseHelper
{
    // ReSharper disable MethodSupportsCancellation
    // ReSharper disable MethodHasAsyncOverloadWithCancellation
    public class RelationalDatabaseTestHarness<TConnection, TCommand, TDataReader>
        where TConnection : class, IDbConnection
        where TCommand : class, IDbCommand
        where TDataReader : class, IDataReader
    {
        private readonly DbCommandFactory _commandFactory;
        private readonly DbCommandExecutor<TCommand, TDataReader> _commandExecutor;

        public RelationalDatabaseTestHarness(DbCommandFactory commandFactory, DbCommandExecutor<TCommand, TDataReader> commandExecutor)
        {
            _commandFactory = commandFactory ?? throw new ArgumentNullException(nameof(commandFactory));
            _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        }

        public async Task RunAsync(TConnection connection, string spanName, CancellationToken cancellationToken)
        {
            using (var parentScope = Tracer.Instance.StartActive(spanName))
            {
                var commandType = typeof(TCommand).FullName;
                parentScope.Span.SetTag("command-type", commandType);
                connection.Open();
                TCommand command;

                using (var scope = Tracer.Instance.StartActive("run.sync"))
                {
                    scope.Span.SetTag("command-type", commandType);
                    await Task.Delay(100, cancellationToken);

                    command = (TCommand)_commandFactory.GetCreateTableCommand(connection);
                    _commandExecutor.ExecuteNonQuery(command);

                    command = (TCommand)_commandFactory.GetInsertRowCommand(connection);
                    _commandExecutor.ExecuteNonQuery(command);

                    command = (TCommand)_commandFactory.GetSelectScalarCommand(connection);
                    _commandExecutor.ExecuteScalar(command);

                    command = (TCommand)_commandFactory.GetUpdateRowCommand(connection);
                    _commandExecutor.ExecuteNonQuery(command);

                    command = (TCommand)_commandFactory.GetSelectRowCommand(connection);
                    _commandExecutor.ExecuteReader(command);

                    command = (TCommand)_commandFactory.GetSelectRowCommand(connection);
                    _commandExecutor.ExecuteReader(command, CommandBehavior.Default);

                    command = (TCommand)_commandFactory.GetDeleteRowCommand(connection);
                    _commandExecutor.ExecuteNonQuery(command);
                }

                await Task.Delay(100, cancellationToken);

                using (var scope = Tracer.Instance.StartActive("run.async"))
                {
                    scope.Span.SetTag("command-type", commandType);
                    await Task.Delay(100, cancellationToken);

                    command = (TCommand)_commandFactory.GetCreateTableCommand(connection);
                    await _commandExecutor.ExecuteNonQueryAsync(command);

                    command = (TCommand)_commandFactory.GetInsertRowCommand(connection);
                    await _commandExecutor.ExecuteNonQueryAsync(command);

                    command = (TCommand)_commandFactory.GetSelectScalarCommand(connection);
                    await _commandExecutor.ExecuteScalarAsync(command);

                    command = (TCommand)_commandFactory.GetUpdateRowCommand(connection);
                    await _commandExecutor.ExecuteNonQueryAsync(command);

                    command = (TCommand)_commandFactory.GetSelectRowCommand(connection);
                    await _commandExecutor.ExecuteReaderAsync(command);

                    command = (TCommand)_commandFactory.GetSelectRowCommand(connection);
                    await _commandExecutor.ExecuteReaderAsync(command, CommandBehavior.Default);

                    command = (TCommand)_commandFactory.GetDeleteRowCommand(connection);
                    await _commandExecutor.ExecuteNonQueryAsync(command);
                }

                await Task.Delay(100, cancellationToken);

                using (var scope = Tracer.Instance.StartActive("run.async.cancellation-token"))
                {
                    scope.Span.SetTag("command-type", commandType);
                    await Task.Delay(100, cancellationToken);

                    command = (TCommand)_commandFactory.GetCreateTableCommand(connection);
                    await _commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                    command = (TCommand)_commandFactory.GetInsertRowCommand(connection);
                    await _commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                    command = (TCommand)_commandFactory.GetSelectScalarCommand(connection);
                    await _commandExecutor.ExecuteScalarAsync(command, cancellationToken);

                    command = (TCommand)_commandFactory.GetUpdateRowCommand(connection);
                    await _commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);

                    command = (TCommand)_commandFactory.GetSelectRowCommand(connection);
                    await _commandExecutor.ExecuteReaderAsync(command, cancellationToken);

                    command = (TCommand)_commandFactory.GetSelectRowCommand(connection);
                    await _commandExecutor.ExecuteReaderAsync(command, CommandBehavior.Default, cancellationToken);

                    command = (TCommand)_commandFactory.GetDeleteRowCommand(connection);
                    await _commandExecutor.ExecuteNonQueryAsync(command, cancellationToken);
                }

                connection.Close();
            }
        }
    }
}
