using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Samples.DatabaseHelper;
using Samples.DatabaseHelper.NetFramework20;

namespace Samples.SqlServer.NetFramework20
{
    public class DbCommandInterfaceGenericExecutor20Adapter<TCommand> : DbCommandExecutor<TCommand>
        where TCommand : IDbCommand
    {
        private readonly DbCommandInterfaceGenericExecutor20<TCommand> _dbCommandInterfaceGenericExecutor20;

        public DbCommandInterfaceGenericExecutor20Adapter(DbCommandInterfaceGenericExecutor20<TCommand> dbCommandInterfaceGenericExecutor20)
        {
            _dbCommandInterfaceGenericExecutor20 = dbCommandInterfaceGenericExecutor20;
        }

        public override string CommandTypeName => _dbCommandInterfaceGenericExecutor20.CommandTypeName;

        public override bool SupportsAsyncMethods => false;

        public override void ExecuteNonQuery(TCommand command) => _dbCommandInterfaceGenericExecutor20.ExecuteNonQuery(command);

        public override Task ExecuteNonQueryAsync(TCommand command) => Task.CompletedTask;

        public override Task ExecuteNonQueryAsync(TCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public override void ExecuteScalar(TCommand command) => _dbCommandInterfaceGenericExecutor20.ExecuteScalar(command);

        public override Task ExecuteScalarAsync(TCommand command) => Task.CompletedTask;

        public override Task ExecuteScalarAsync(TCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public override void ExecuteReader(TCommand command) => _dbCommandInterfaceGenericExecutor20.ExecuteReader(command);

        public override void ExecuteReader(TCommand command, CommandBehavior behavior) => _dbCommandInterfaceGenericExecutor20.ExecuteReader(command, behavior);

        public override Task ExecuteReaderAsync(TCommand command) => Task.CompletedTask;

        public override Task ExecuteReaderAsync(TCommand command, CommandBehavior behavior) => Task.CompletedTask;

        public override Task ExecuteReaderAsync(TCommand command, CancellationToken cancellationToken) => Task.CompletedTask;

        public override Task ExecuteReaderAsync(TCommand command, CommandBehavior behavior, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
