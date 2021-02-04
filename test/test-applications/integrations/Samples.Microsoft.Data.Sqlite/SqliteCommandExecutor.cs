using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Samples.DatabaseHelper;

namespace Samples.Microsoft.Data.Sqlite
{
    public class SqliteCommandExecutor : DbCommandExecutor<SqliteCommand>
    {
        public override string CommandTypeName => nameof(SqliteCommand);
        
        public override bool SupportsAsyncMethods => true;
        
        public override void ExecuteNonQuery(SqliteCommand command) => command.ExecuteNonQuery();

        public override Task ExecuteNonQueryAsync(SqliteCommand command) => command.ExecuteNonQueryAsync();

        public override Task ExecuteNonQueryAsync(SqliteCommand command, CancellationToken cancellationToken) => command.ExecuteNonQueryAsync(cancellationToken);

        public override void ExecuteScalar(SqliteCommand command) => command.ExecuteScalar();

        public override Task ExecuteScalarAsync(SqliteCommand command) => command.ExecuteScalarAsync();

        public override Task ExecuteScalarAsync(SqliteCommand command, CancellationToken cancellationToken) => command.ExecuteScalarAsync(cancellationToken);

        public override void ExecuteReader(SqliteCommand command)
        {
            using DbDataReader reader = command.ExecuteReader();
        }

        public override void ExecuteReader(SqliteCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = command.ExecuteReader(behavior);
        }

        public override async Task ExecuteReaderAsync(SqliteCommand command)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync();
        }

        public override async Task ExecuteReaderAsync(SqliteCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior);
        }

        public override async Task ExecuteReaderAsync(SqliteCommand command, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        }

        public override async Task ExecuteReaderAsync(SqliteCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior, cancellationToken);
        }
    }
}
