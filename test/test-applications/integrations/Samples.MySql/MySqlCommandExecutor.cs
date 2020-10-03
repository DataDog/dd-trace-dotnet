using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Samples.DatabaseHelper;

namespace Samples.MySql
{
    public class MySqlCommandExecutor : DbCommandExecutor<MySqlCommand>
    {
        public override string CommandTypeName => nameof(MySqlCommand);

        public override bool SupportsAsyncMethods => true;

        public override void ExecuteNonQuery(MySqlCommand command) => command.ExecuteNonQuery();

        public override Task ExecuteNonQueryAsync(MySqlCommand command) => command.ExecuteNonQueryAsync();

        public override Task ExecuteNonQueryAsync(MySqlCommand command, CancellationToken cancellationToken) => command.ExecuteNonQueryAsync(cancellationToken);

        public override void ExecuteScalar(MySqlCommand command) => command.ExecuteScalar();

        public override Task ExecuteScalarAsync(MySqlCommand command) => command.ExecuteScalarAsync();

        public override Task ExecuteScalarAsync(MySqlCommand command, CancellationToken cancellationToken) => command.ExecuteScalarAsync(cancellationToken);

        public override void ExecuteReader(MySqlCommand command)
        {
            using DbDataReader reader = command.ExecuteReader();
        }

        public override void ExecuteReader(MySqlCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = command.ExecuteReader(behavior);
        }

        public override async Task ExecuteReaderAsync(MySqlCommand command)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync();
        }

        public override async Task ExecuteReaderAsync(MySqlCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior);
        }

        public override async Task ExecuteReaderAsync(MySqlCommand command, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        }

        public override async Task ExecuteReaderAsync(MySqlCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior, cancellationToken);
        }
    }
}
