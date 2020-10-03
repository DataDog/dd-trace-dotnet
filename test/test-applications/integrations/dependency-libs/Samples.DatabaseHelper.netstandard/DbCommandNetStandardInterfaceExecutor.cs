using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.DatabaseHelper
{
    public class DbCommandNetStandardInterfaceExecutor : DbCommandExecutor<IDbCommand>
    {
        public override string CommandTypeName => nameof(IDbCommand) + "-netstandard";

        public override bool SupportsAsyncMethods => false;

        public override void ExecuteNonQuery(IDbCommand command) => command.ExecuteNonQuery();

        public override Task ExecuteNonQueryAsync(IDbCommand command) => Task.CompletedTask;

        public override Task ExecuteNonQueryAsync(IDbCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public override void ExecuteScalar(IDbCommand command) => command.ExecuteScalar();

        public override Task ExecuteScalarAsync(IDbCommand command) => Task.CompletedTask;

        public override Task ExecuteScalarAsync(IDbCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public override void ExecuteReader(IDbCommand command)
        {
            using IDataReader reader = command.ExecuteReader();
        }

        public override void ExecuteReader(IDbCommand command, CommandBehavior behavior)
        {
            using IDataReader reader = command.ExecuteReader(behavior);
        }

        public override Task ExecuteReaderAsync(IDbCommand command) => Task.CompletedTask;

        public override Task ExecuteReaderAsync(IDbCommand command, CommandBehavior behavior) => Task.CompletedTask;

        public override Task ExecuteReaderAsync(IDbCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task ExecuteReaderAsync(IDbCommand command, CommandBehavior behavior, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
