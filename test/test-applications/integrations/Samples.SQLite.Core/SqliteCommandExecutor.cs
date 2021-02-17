using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Threading;
using System.Threading.Tasks;
using Samples.DatabaseHelper;

namespace Samples.SQLite.Core
{
    public class SqliteCommandExecutor : DbCommandExecutor<SQLiteCommand>
    {
        public override string CommandTypeName => nameof(SQLiteCommand);
        
        public override bool SupportsAsyncMethods => true;
        
        public override void ExecuteNonQuery(SQLiteCommand command) => command.ExecuteNonQuery();

        public override Task ExecuteNonQueryAsync(SQLiteCommand command) => command.ExecuteNonQueryAsync();

        public override Task ExecuteNonQueryAsync(SQLiteCommand command, CancellationToken cancellationToken) => command.ExecuteNonQueryAsync(cancellationToken);

        public override void ExecuteScalar(SQLiteCommand command) => command.ExecuteScalar();

        public override Task ExecuteScalarAsync(SQLiteCommand command) => command.ExecuteScalarAsync();

        public override Task ExecuteScalarAsync(SQLiteCommand command, CancellationToken cancellationToken) => command.ExecuteScalarAsync(cancellationToken);

        public override void ExecuteReader(SQLiteCommand command)
        {
            using DbDataReader reader = command.ExecuteReader();
        }

        public override void ExecuteReader(SQLiteCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = command.ExecuteReader(behavior);
        }

        public override async Task ExecuteReaderAsync(SQLiteCommand command)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync();
        }

        public override async Task ExecuteReaderAsync(SQLiteCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior);
        }

        public override async Task ExecuteReaderAsync(SQLiteCommand command, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        }

        public override async Task ExecuteReaderAsync(SQLiteCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior, cancellationToken);
        }
    }
}
