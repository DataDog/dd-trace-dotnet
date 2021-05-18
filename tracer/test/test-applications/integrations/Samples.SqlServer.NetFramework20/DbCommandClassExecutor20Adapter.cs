using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Samples.DatabaseHelper;
using Samples.DatabaseHelper.NetFramework20;

namespace Samples.SqlServer.NetFramework20
{
    public class DbCommandClassExecutor20Adapter : DbCommandExecutor<DbCommand>
    {
        private static readonly Task CompletedTask = Task.FromResult(0);

        private readonly DbCommandClassExecutor20 _dbCommandClassExecutor20;

        public DbCommandClassExecutor20Adapter(DbCommandClassExecutor20 dbCommandClassExecutor20)
        {
            _dbCommandClassExecutor20 = dbCommandClassExecutor20;
        }

        public override string CommandTypeName => _dbCommandClassExecutor20.CommandTypeName;

        public override bool SupportsAsyncMethods => false;

        public override void ExecuteNonQuery(DbCommand command) => _dbCommandClassExecutor20.ExecuteNonQuery(command);

        public override Task ExecuteNonQueryAsync(DbCommand command) => CompletedTask;

        public override Task ExecuteNonQueryAsync(DbCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override void ExecuteScalar(DbCommand command) => _dbCommandClassExecutor20.ExecuteScalar(command);

        public override Task ExecuteScalarAsync(DbCommand command) => CompletedTask;

        public override Task ExecuteScalarAsync(DbCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override void ExecuteReader(DbCommand command) => _dbCommandClassExecutor20.ExecuteReader(command);

        public override void ExecuteReader(DbCommand command, CommandBehavior behavior) => _dbCommandClassExecutor20.ExecuteReader(command, behavior);

        public override Task ExecuteReaderAsync(DbCommand command) => CompletedTask;

        public override Task ExecuteReaderAsync(DbCommand command, CommandBehavior behavior) => CompletedTask;

        public override Task ExecuteReaderAsync(DbCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override Task ExecuteReaderAsync(DbCommand command, CommandBehavior behavior, CancellationToken cancellationToken) => CompletedTask;
    }
}
