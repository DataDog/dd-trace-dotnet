using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Samples.DatabaseHelper;

namespace Samples.Npgsql
{
    public class NpgsqlCommandExecutor : DbCommandExecutor<NpgsqlCommand>
    {
        public override string CommandTypeName => nameof(NpgsqlCommand);

        public override bool SupportsAsyncMethods => true;

        public override void ExecuteNonQuery(NpgsqlCommand command) => command.ExecuteNonQuery();

        public override Task ExecuteNonQueryAsync(NpgsqlCommand command) => command.ExecuteNonQueryAsync();

        public override Task ExecuteNonQueryAsync(NpgsqlCommand command, CancellationToken cancellationToken) => command.ExecuteNonQueryAsync(cancellationToken);

        public override void ExecuteScalar(NpgsqlCommand command) => command.ExecuteScalar();

        public override Task ExecuteScalarAsync(NpgsqlCommand command) => command.ExecuteScalarAsync();

        public override Task ExecuteScalarAsync(NpgsqlCommand command, CancellationToken cancellationToken) => command.ExecuteScalarAsync(cancellationToken);

        public override void ExecuteReader(NpgsqlCommand command)
        {
            using DbDataReader reader = command.ExecuteReader();
        }

        public override void ExecuteReader(NpgsqlCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = command.ExecuteReader(behavior);
        }

        public override async Task ExecuteReaderAsync(NpgsqlCommand command)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync();
        }

        public override async Task ExecuteReaderAsync(NpgsqlCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior);
        }

        public override async Task ExecuteReaderAsync(NpgsqlCommand command, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        }

        public override async Task ExecuteReaderAsync(NpgsqlCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior, cancellationToken);
        }
    }
}
