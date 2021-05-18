using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Oracle.ManagedDataAccess.Client;
using Samples.DatabaseHelper;

namespace Samples.OracleMDA
{
    public class OracleCommandExecutor : DbCommandExecutor<OracleCommand>
    {
        public override string CommandTypeName => nameof(OracleCommand);
        
        public override bool SupportsAsyncMethods => true;
        
        public override void ExecuteNonQuery(OracleCommand command) => command.ExecuteNonQuery();

        public override Task ExecuteNonQueryAsync(OracleCommand command) => command.ExecuteNonQueryAsync();

        public override Task ExecuteNonQueryAsync(OracleCommand command, CancellationToken cancellationToken) => command.ExecuteNonQueryAsync(cancellationToken);

        public override void ExecuteScalar(OracleCommand command) => command.ExecuteScalar();

        public override Task ExecuteScalarAsync(OracleCommand command) => command.ExecuteScalarAsync();

        public override Task ExecuteScalarAsync(OracleCommand command, CancellationToken cancellationToken) => command.ExecuteScalarAsync(cancellationToken);

        public override void ExecuteReader(OracleCommand command)
        {
            using DbDataReader reader = command.ExecuteReader();
        }

        public override void ExecuteReader(OracleCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = command.ExecuteReader(behavior);
        }

        public override async Task ExecuteReaderAsync(OracleCommand command)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync();
        }

        public override async Task ExecuteReaderAsync(OracleCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior);
        }

        public override async Task ExecuteReaderAsync(OracleCommand command, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        }

        public override async Task ExecuteReaderAsync(OracleCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior, cancellationToken);
        }
    }
}
