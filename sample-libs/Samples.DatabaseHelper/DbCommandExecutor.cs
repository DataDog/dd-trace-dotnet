using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.DatabaseHelper
{
    public class DbCommandExecutor<TCommand, TDataReader>
        where TCommand : class, IDbCommand
        where TDataReader : class, IDataReader
    {
        private static readonly string CommandTypeName = typeof(TCommand).Name;

        private readonly Func<TCommand, int> _executeNonQuery;
        private readonly Func<TCommand, object> _executeScalar;
        private readonly Func<TCommand, TDataReader> _executeReader;
        private readonly Func<TCommand, CommandBehavior, TDataReader> _executeReaderWithBehavior;

        private readonly Func<TCommand, Task<int>> _executeNonQueryAsync;
        private readonly Func<TCommand, CancellationToken, Task<int>> _executeNonQueryWithCancellationAsync;
        private readonly Func<TCommand, Task<object>> _executeScalarAsync;
        private readonly Func<TCommand, CancellationToken, Task<object>> _executeScalarWithCancellationAsync;
        private readonly Func<TCommand, Task<TDataReader>> _executeReaderAsync;
        private readonly Func<TCommand, CommandBehavior, Task<TDataReader>> _executeReaderWithBehaviorAsync;
        private readonly Func<TCommand, CancellationToken, Task<TDataReader>> _executeReaderWithCancellationAsync;
        private readonly Func<TCommand, CommandBehavior, CancellationToken, Task<TDataReader>> _executeReaderWithBehaviorAndCancellationAsync;

        public Type DbCommandType { get; } = typeof(TCommand);

        public DbCommandExecutor(
            Func<TCommand, int> executeNonQuery,
            Func<TCommand, object> executeScalar,
            Func<TCommand, TDataReader> executeReader,
            Func<TCommand, CommandBehavior, TDataReader> executeReaderWithBehavior,
            Func<TCommand, Task<int>> executeNonQueryAsync,
            Func<TCommand, CancellationToken, Task<int>> executeNonQueryWithCancellationAsync,
            Func<TCommand, Task<object>> executeScalarAsync,
            Func<TCommand, CancellationToken, Task<object>> executeScalarWithCancellationAsync,
            Func<TCommand, Task<TDataReader>> executeReaderAsync,
            Func<TCommand, CommandBehavior, Task<TDataReader>> executeReaderWithBehaviorAsync,
            Func<TCommand, CancellationToken, Task<TDataReader>> executeReaderWithCancellationAsync,
            Func<TCommand, CommandBehavior, CancellationToken, Task<TDataReader>> executeReaderWithBehaviorAndCancellationAsync)
        {
            _executeNonQuery = executeNonQuery;
            _executeScalar = executeScalar;
            _executeReader = executeReader;
            _executeReaderWithBehavior = executeReaderWithBehavior;

            _executeNonQueryAsync = executeNonQueryAsync;
            _executeNonQueryWithCancellationAsync = executeNonQueryWithCancellationAsync;
            _executeScalarAsync = executeScalarAsync;
            _executeScalarWithCancellationAsync = executeScalarWithCancellationAsync;
            _executeReaderAsync = executeReaderAsync;
            _executeReaderWithBehaviorAsync = executeReaderWithBehaviorAsync;
            _executeReaderWithCancellationAsync = executeReaderWithCancellationAsync;
            _executeReaderWithBehaviorAndCancellationAsync = executeReaderWithBehaviorAndCancellationAsync;
        }

        public void ExecuteNonQuery(TCommand command)
        {
            var func = _executeNonQuery;

            if (func != null)
            {
                int count = func(command);
                WriteOutput("ExecuteNonQuery()", command.CommandText, $"{count} rows affected");
            }
        }

        public async Task ExecuteNonQueryAsync(TCommand command)
        {
            var func = _executeNonQueryAsync;

            if (func != null)
            {
                int count = await func(command);
                WriteOutput("ExecuteNonQueryAsync()", command.CommandText, $"{count} rows affected");
            }
        }

        public async Task ExecuteNonQueryAsync(TCommand command, CancellationToken cancellationToken)
        {
            var func = _executeNonQueryWithCancellationAsync;

            if (func != null)
            {
                int count = await func(command, cancellationToken);
                WriteOutput("ExecuteNonQueryAsync(CancellationToken)", command.CommandText, $"{count} rows affected");
            }
        }

        public void ExecuteScalar(TCommand command)
        {
            var func = _executeScalar;

            if (func != null)
            {
                object result = func(command);
                WriteOutput("ExecuteScalar()", command.CommandText, $"Returned: {result}");
            }
        }

        public async Task ExecuteScalarAsync(TCommand command)
        {
            var func = _executeScalarAsync;

            if (func != null)
            {
                object result = await func(command);
                WriteOutput("ExecuteScalarAsync()", command.CommandText, $"Returned: {result}");
            }
        }

        public async Task ExecuteScalarAsync(TCommand command, CancellationToken cancellationToken)
        {
            var func = _executeScalarWithCancellationAsync;

            if (func != null)
            {
                object result = await func(command, cancellationToken);
                WriteOutput("ExecuteScalarAsync(CancellationToken)", command.CommandText, $"Returned: {result}");
            }
        }

        public void ExecuteReader(TCommand command)
        {
            var func = _executeReader;

            if (func != null)
            {
                using (TDataReader reader = func(command))
                {
                    int count = reader.AsDataRecords().Count();
                    WriteOutput("ExecuteReader()", command.CommandText, $"Returned {count} records");
                }
            }
        }

        public void ExecuteReader(TCommand command, CommandBehavior behavior)
        {
            var func = _executeReaderWithBehavior;

            if (func != null)
            {
                using (TDataReader reader = func(command, behavior))
                {
                    int count = reader.AsDataRecords().Count();
                    WriteOutput("ExecuteReader(CommandBehavior)", command.CommandText, $"Returned {count} records");
                }
            }
        }

        public async Task ExecuteReaderAsync(TCommand command)
        {
            var func = _executeReaderAsync;

            if (func != null)
            {
                using (TDataReader reader = await func(command))
                {
                    int count = reader.AsDataRecords().Count();
                    WriteOutput("ExecuteReaderAsync()", command.CommandText, $"Returned {count} records");
                }
            }
        }

        public async Task ExecuteReaderAsync(TCommand command, CommandBehavior behavior)
        {
            var func = _executeReaderWithBehaviorAsync;

            if (func != null)
            {
                using (TDataReader reader = await func(command, behavior))
                {
                    int count = reader.AsDataRecords().Count();
                    WriteOutput("ExecuteReaderAsync(CommandBehavior)", command.CommandText, $"Returned {count} records");
                }
            }
        }

        public async Task ExecuteReaderAsync(TCommand command, CancellationToken cancellationToken)
        {
            var func = _executeReaderWithCancellationAsync;

            if (func != null)
            {
                using (TDataReader reader = await func(command, cancellationToken))
                {
                    int count = reader.AsDataRecords().Count();
                    WriteOutput("ExecuteReaderAsync(CancellationToken)", command.CommandText, $"Returned {count} records");
                }
            }
        }

        public async Task ExecuteReaderAsync(TCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
        {
            var func = _executeReaderWithBehaviorAndCancellationAsync;

            if (func != null)
            {
                using (TDataReader reader = await func(command, behavior, cancellationToken))
                {
                    int count = reader.AsDataRecords().Count();
                    WriteOutput("ExecuteReaderAsync(CommandBehavior, CancellationToken)", command.CommandText, $"Returned {count} records");
                }
            }
        }

        private void WriteOutput(string method, string commandText, string message)
        {
            Console.WriteLine($"    {CommandTypeName}.{method}");
            Console.WriteLine($"    {commandText}");
            Console.WriteLine($"    {message}");
            Console.WriteLine();
        }
    }

    public static class DbCommandExecutor
    {
        public static DbCommandExecutor<DbCommand, DbDataReader> GetDbCommandExecutor()
        {
            return new DbCommandExecutor<DbCommand, DbDataReader>(
                command => command.ExecuteNonQuery(),
                command => command.ExecuteScalar(),
                command => command.ExecuteReader(),
                (command, behavior) => command.ExecuteReader(behavior),
                command => command.ExecuteNonQueryAsync(),
                (command, ct) => command.ExecuteNonQueryAsync(ct),
                command => command.ExecuteScalarAsync(),
                (command, ct) => command.ExecuteScalarAsync(ct),
                command => command.ExecuteReaderAsync(),
                (command, behavior) => command.ExecuteReaderAsync(behavior),
                (command, ct) => command.ExecuteReaderAsync(ct),
                (command, behavior, ct) => command.ExecuteReaderAsync(behavior, ct));
        }

        public static DbCommandExecutor<IDbCommand, IDataReader> GetIDbCommandExecutor()
        {
            return new DbCommandExecutor<IDbCommand, IDataReader>(
                command => command.ExecuteNonQuery(),
                command => command.ExecuteScalar(),
                command => command.ExecuteReader(),
                (command, behavior) => command.ExecuteReader(behavior),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }

#if !NET45
        public static DbCommandExecutor<DbCommand, DbDataReader> GetDbWrapperExecutor()
        {
            return new DbCommandExecutor<DbCommand, DbDataReader>(
                command => new DbCommandWrapper(command).ExecuteNonQuery(),
                command => new DbCommandWrapper(command).ExecuteScalar(),
                command => new DbCommandWrapper(command).ExecuteReader(),
                (command, behavior) => new DbCommandWrapper(command).ExecuteReader(behavior),
                command => new DbCommandWrapper(command).ExecuteNonQueryAsync(),
                (command, ct) => new DbCommandWrapper(command).ExecuteNonQueryAsync(ct),
                command => new DbCommandWrapper(command).ExecuteScalarAsync(),
                (command, ct) => new DbCommandWrapper(command).ExecuteScalarAsync(ct),
                command => new DbCommandWrapper(command).ExecuteReaderAsync(),
                (command, behavior) => new DbCommandWrapper(command).ExecuteReaderAsync(behavior),
                (command, ct) => new DbCommandWrapper(command).ExecuteReaderAsync(ct),
                (command, behavior, ct) => new DbCommandWrapper(command).ExecuteReaderAsync(behavior, ct));
        }

        public static DbCommandExecutor<IDbCommand, IDataReader> GetIDbWrapperExecutor()
        {
            return new DbCommandExecutor<IDbCommand, IDataReader>(
                command => new IDbCommandWrapper(command).ExecuteNonQuery(),
                command => new IDbCommandWrapper(command).ExecuteScalar(),
                command => new IDbCommandWrapper(command).ExecuteReader(),
                (command, behavior) => new IDbCommandWrapper(command).ExecuteReader(behavior),
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
        }
#endif
    }
}
