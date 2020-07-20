using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.DatabaseHelper
{
    public class DbCommandNetStandardClassExecutor : IDbCommandExecutor
    {
        public string CommandTypeName => nameof(DbCommand) + "-netstandard";

        public bool SupportsAsyncMethods => true;

        public void ExecuteNonQuery(DbCommand command) => command.ExecuteNonQuery();

        public Task ExecuteNonQueryAsync(DbCommand command) => command.ExecuteNonQueryAsync();

        public Task ExecuteNonQueryAsync(DbCommand command, CancellationToken cancellationToken) => command.ExecuteNonQueryAsync(cancellationToken);

        public void ExecuteScalar(DbCommand command) => command.ExecuteScalar();

        public Task ExecuteScalarAsync(DbCommand command) => command.ExecuteScalarAsync();

        public Task ExecuteScalarAsync(DbCommand command, CancellationToken cancellationToken) => command.ExecuteScalarAsync(cancellationToken);

        public void ExecuteReader(DbCommand command)
        {
            using DbDataReader reader = command.ExecuteReader();
        }

        public void ExecuteReader(DbCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = command.ExecuteReader(behavior);
        }

        public async Task ExecuteReaderAsync(DbCommand command)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync();
        }

        public async Task ExecuteReaderAsync(DbCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior);
        }

        public async Task ExecuteReaderAsync(DbCommand command, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        }

        public async Task ExecuteReaderAsync(DbCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior, cancellationToken);
        }
    }
}
