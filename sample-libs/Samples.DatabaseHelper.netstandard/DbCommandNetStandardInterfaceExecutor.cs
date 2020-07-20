using System.Data;
using System.Threading;
using System.Threading.Tasks;

namespace Samples.DatabaseHelper
{
    public class DbCommandNetStandardInterfaceExecutor : IDbCommandExecutor
    {
        public string CommandTypeName => nameof(IDbCommand) + "-netstandard";

        public bool SupportsAsyncMethods => false;

        public void ExecuteNonQuery(IDbCommand command) => command.ExecuteNonQuery();

        public Task ExecuteNonQueryAsync(IDbCommand command) => Task.CompletedTask;

        public Task ExecuteNonQueryAsync(IDbCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public void ExecuteScalar(IDbCommand command) => command.ExecuteScalar();

        public Task ExecuteScalarAsync(IDbCommand command) => Task.CompletedTask;

        public Task ExecuteScalarAsync(IDbCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public void ExecuteReader(IDbCommand command)
        {
            using IDataReader reader = command.ExecuteReader();
        }

        public void ExecuteReader(IDbCommand command, CommandBehavior behavior)
        {
            using IDataReader reader = command.ExecuteReader(behavior);
        }

        public Task ExecuteReaderAsync(IDbCommand command) => Task.CompletedTask;

        public Task ExecuteReaderAsync(IDbCommand command, CommandBehavior behavior) => Task.CompletedTask;

        public Task ExecuteReaderAsync(IDbCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ExecuteReaderAsync(IDbCommand command, CommandBehavior behavior, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
