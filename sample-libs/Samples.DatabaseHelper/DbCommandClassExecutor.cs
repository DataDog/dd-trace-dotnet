using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.DatabaseHelper
{
    public class DbCommandClassExecutor : DbCommandExecutor<DbCommand>
    {
        public override string CommandTypeName => nameof(DbCommand);

        public override bool SupportsAsyncMethods => true;

        public override void ExecuteNonQuery(DbCommand command) => command.ExecuteNonQuery();

        public override Task ExecuteNonQueryAsync(DbCommand command) => command.ExecuteNonQueryAsync();

        public override Task ExecuteNonQueryAsync(DbCommand command, CancellationToken cancellationToken) => command.ExecuteNonQueryAsync(cancellationToken);

        public override void ExecuteScalar(DbCommand command) => command.ExecuteScalar();

        public override Task ExecuteScalarAsync(DbCommand command) => command.ExecuteScalarAsync();

        public override Task ExecuteScalarAsync(DbCommand command, CancellationToken cancellationToken) => command.ExecuteScalarAsync(cancellationToken);

        public override void ExecuteReader(DbCommand command)
        {
            using DbDataReader reader = command.ExecuteReader();
        }

        public override void ExecuteReader(DbCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = command.ExecuteReader(behavior);
        }

        public override async Task ExecuteReaderAsync(DbCommand command)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync();
        }

        public override async Task ExecuteReaderAsync(DbCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior);
        }

        public override async Task ExecuteReaderAsync(DbCommand command, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        }

        public override async Task ExecuteReaderAsync(DbCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior, cancellationToken);
        }
    }
}
