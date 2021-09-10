using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.DatabaseHelper
{
    public class DbCommandInterfaceGenericExecutor<TCommand> : DbCommandExecutor<TCommand>
        where TCommand : IDbCommand
    {
        private static readonly Task CompletedTask = Task.FromResult(0);

        public override string CommandTypeName => "IDbCommandGenericConstraint<" + typeof(TCommand).Name + ">";

        public override bool SupportsAsyncMethods => false;

        public override void ExecuteNonQuery(TCommand command) => command.ExecuteNonQuery();

        public override Task ExecuteNonQueryAsync(TCommand command) => CompletedTask;

        public override Task ExecuteNonQueryAsync(TCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override void ExecuteScalar(TCommand command) => command.ExecuteScalar();

        public override Task ExecuteScalarAsync(TCommand command) => CompletedTask;

        public override Task ExecuteScalarAsync(TCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override void ExecuteReader(TCommand command)
        {
            using IDataReader reader = command.ExecuteReader();
        }

        public override void ExecuteReader(TCommand command, CommandBehavior behavior)
        {
            using IDataReader reader = command.ExecuteReader(behavior);
        }

        public override Task ExecuteReaderAsync(TCommand command) => CompletedTask;

        public override Task ExecuteReaderAsync(TCommand command, CommandBehavior behavior) => CompletedTask;

        public override Task ExecuteReaderAsync(TCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override Task ExecuteReaderAsync(TCommand command, CommandBehavior behavior, CancellationToken cancellationToken) => CompletedTask;
    }
}
