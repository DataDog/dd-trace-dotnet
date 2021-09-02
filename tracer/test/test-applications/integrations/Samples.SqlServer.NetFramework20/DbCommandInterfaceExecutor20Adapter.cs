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
    class DbCommandInterfaceExecutor20Adapter : DbCommandExecutor<IDbCommand>
    {
        private static readonly Task CompletedTask = Task.FromResult(0);

        private readonly DbCommandInterfaceExecutor20 _dbCommandInterfaceExecutor20;

        public DbCommandInterfaceExecutor20Adapter(DbCommandInterfaceExecutor20 dbCommandInterfaceExecutor20)
        {
            _dbCommandInterfaceExecutor20 = dbCommandInterfaceExecutor20;
        }

        public override string CommandTypeName => _dbCommandInterfaceExecutor20.CommandTypeName;

        public override bool SupportsAsyncMethods => false;

        public override void ExecuteNonQuery(IDbCommand command) => _dbCommandInterfaceExecutor20.ExecuteNonQuery(command);

        public override Task ExecuteNonQueryAsync(IDbCommand command) => CompletedTask;

        public override Task ExecuteNonQueryAsync(IDbCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override void ExecuteScalar(IDbCommand command) => _dbCommandInterfaceExecutor20.ExecuteScalar(command);

        public override Task ExecuteScalarAsync(IDbCommand command) => CompletedTask;

        public override Task ExecuteScalarAsync(IDbCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override void ExecuteReader(IDbCommand command) => _dbCommandInterfaceExecutor20.ExecuteReader(command);

        public override void ExecuteReader(IDbCommand command, CommandBehavior behavior) => _dbCommandInterfaceExecutor20.ExecuteReader(command, behavior);

        public override Task ExecuteReaderAsync(IDbCommand command) => CompletedTask;

        public override Task ExecuteReaderAsync(IDbCommand command, CommandBehavior behavior) => CompletedTask;

        public override Task ExecuteReaderAsync(IDbCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override Task ExecuteReaderAsync(IDbCommand command, CommandBehavior behavior, CancellationToken cancellationToken) => CompletedTask;
    }
}
