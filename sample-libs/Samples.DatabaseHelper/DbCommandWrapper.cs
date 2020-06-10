using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.DatabaseHelper
{
    public class DbCommandWrapper
    {
        private readonly DbCommand _command;

        public DbCommandWrapper(DbCommand command)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
        }

        public int ExecuteNonQuery()
        {
            return _command.ExecuteNonQuery();
        }

        public DbDataReader ExecuteReader()
        {
            return _command.ExecuteReader();
        }

        public DbDataReader ExecuteReader(CommandBehavior behavior)
        {
            return _command.ExecuteReader(behavior);
        }

        public Task<int> ExecuteNonQueryAsync()
        {
            return _command.ExecuteNonQueryAsync();
        }

        public Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken)
        {
            return _command.ExecuteNonQueryAsync(cancellationToken);
        }

        public Task<DbDataReader> ExecuteReaderAsync()
        {
            return _command.ExecuteReaderAsync();
        }

        public Task<DbDataReader> ExecuteReaderAsync(CancellationToken cancellationToken)
        {
            return _command.ExecuteReaderAsync(cancellationToken);
        }

        public Task<DbDataReader> ExecuteReaderAsync(CommandBehavior behavior)
        {
            return _command.ExecuteReaderAsync(behavior);
        }

        public Task<DbDataReader> ExecuteReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken)
        {
            return _command.ExecuteReaderAsync(behavior, cancellationToken);
        }

        public object ExecuteScalar()
        {
            return _command.ExecuteScalar();
        }

        public Task<object> ExecuteScalarAsync()
        {
            return _command.ExecuteScalarAsync();
        }

        public Task<object> ExecuteScalarAsync(CancellationToken cancellationToken)
        {
            return _command.ExecuteScalarAsync(cancellationToken);
        }
    }
}
