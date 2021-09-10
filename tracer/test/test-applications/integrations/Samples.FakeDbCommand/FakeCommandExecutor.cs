using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Samples.DatabaseHelper;

namespace Samples.FakeDbCommand
{
    public class FakeCommandExecutor : DbCommandExecutor<FakeCommand>
    {
        public override string CommandTypeName => nameof(FakeCommand);
        
        public override bool SupportsAsyncMethods => true;
        
        public override void ExecuteNonQuery(FakeCommand command) => command.ExecuteNonQuery();

        public override Task ExecuteNonQueryAsync(FakeCommand command) => command.ExecuteNonQueryAsync();

        public override Task ExecuteNonQueryAsync(FakeCommand command, CancellationToken cancellationToken) => command.ExecuteNonQueryAsync(cancellationToken);

        public override void ExecuteScalar(FakeCommand command) => command.ExecuteScalar();

        public override Task ExecuteScalarAsync(FakeCommand command) => command.ExecuteScalarAsync();

        public override Task ExecuteScalarAsync(FakeCommand command, CancellationToken cancellationToken) => command.ExecuteScalarAsync(cancellationToken);

        public override void ExecuteReader(FakeCommand command)
        {
            using DbDataReader reader = command.ExecuteReader();
        }

        public override void ExecuteReader(FakeCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = command.ExecuteReader(behavior);
        }

        public override async Task ExecuteReaderAsync(FakeCommand command)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync();
        }

        public override async Task ExecuteReaderAsync(FakeCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior);
        }

        public override async Task ExecuteReaderAsync(FakeCommand command, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        }

        public override async Task ExecuteReaderAsync(FakeCommand command, CommandBehavior behavior, CancellationToken cancellationToken)
        {
            using DbDataReader reader = await command.ExecuteReaderAsync(behavior, cancellationToken);
        }
    }
}
