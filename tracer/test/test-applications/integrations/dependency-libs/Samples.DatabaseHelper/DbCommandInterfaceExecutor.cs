using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.DatabaseHelper
{
    public class DbCommandInterfaceExecutor : DbCommandExecutor<IDbCommand>
    {
        private static readonly Task CompletedTask = Task.FromResult(0);

        public override string CommandTypeName => nameof(IDbCommand);

        public override bool SupportsAsyncMethods => false;

        public override void ExecuteNonQuery(IDbCommand command) => command.ExecuteNonQuery();

        public override Task ExecuteNonQueryAsync(IDbCommand command) => CompletedTask;

        public override Task ExecuteNonQueryAsync(IDbCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override void ExecuteScalar(IDbCommand command) => command.ExecuteScalar();

        public override Task ExecuteScalarAsync(IDbCommand command) => CompletedTask;

        public override Task ExecuteScalarAsync(IDbCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override void ExecuteReader(IDbCommand command)
        {
            using IDataReader reader = command.ExecuteReader();
        }

        public override void ExecuteReader(IDbCommand command, CommandBehavior behavior)
        {
            using IDataReader reader = command.ExecuteReader(behavior);
        }

        public override Task ExecuteReaderAsync(IDbCommand command) => CompletedTask;

        public override Task ExecuteReaderAsync(IDbCommand command, CommandBehavior behavior) => CompletedTask;

        public override Task ExecuteReaderAsync(IDbCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override Task ExecuteReaderAsync(IDbCommand command, CommandBehavior behavior, CancellationToken cancellationToken) => CompletedTask;
    }
}
