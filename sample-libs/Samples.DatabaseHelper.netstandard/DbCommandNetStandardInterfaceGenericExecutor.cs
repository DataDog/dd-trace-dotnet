using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.DatabaseHelper
{
    public class DbCommandNetStandardInterfaceGenericExecutor<TCommand> : DbCommandExecutor<TCommand>
        where TCommand : IDbCommand
    {
        public override string CommandTypeName => "IDbCommandGenericConstraint<" + typeof(TCommand).Name + ">-netstandard";

        public override bool SupportsAsyncMethods => false;

        public override void ExecuteNonQuery(TCommand command) => command.ExecuteNonQuery();

        public override Task ExecuteNonQueryAsync(TCommand command) => Task.CompletedTask;

        public override Task ExecuteNonQueryAsync(TCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public override void ExecuteScalar(TCommand command) => command.ExecuteScalar();

        public override Task ExecuteScalarAsync(TCommand command) => Task.CompletedTask;

        public override Task ExecuteScalarAsync(TCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public override void ExecuteReader(TCommand command)
        {
            using IDataReader reader = command.ExecuteReader();
        }

        public override void ExecuteReader(TCommand command, CommandBehavior behavior)
        {
            using IDataReader reader = command.ExecuteReader(behavior);
        }

        public override Task ExecuteReaderAsync(TCommand command) => Task.CompletedTask;

        public override Task ExecuteReaderAsync(TCommand command, CommandBehavior behavior) => Task.CompletedTask;

        public override Task ExecuteReaderAsync(TCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task ExecuteReaderAsync(TCommand command, CommandBehavior behavior, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
