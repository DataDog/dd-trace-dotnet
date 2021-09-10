using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Threading;
using System.Threading.Tasks;
using Samples.DatabaseHelper;
using Samples.DatabaseHelper.NetFramework20;

namespace Samples.SqlServer.NetFramework20
{
    public class SqlCommandExecutor20Adapter : DbCommandExecutor<SqlCommand>
    {
        private static readonly Task CompletedTask = Task.FromResult(0);

        private readonly SqlCommandExecutor20 _sqlCommandExecutor20;

        public SqlCommandExecutor20Adapter(SqlCommandExecutor20 sqlCommandExecutor20)
        {
            _sqlCommandExecutor20 = sqlCommandExecutor20;
        }

        public override string CommandTypeName => _sqlCommandExecutor20.CommandTypeName;

        public override bool SupportsAsyncMethods => false;

        public override void ExecuteNonQuery(SqlCommand command) => _sqlCommandExecutor20.ExecuteNonQuery(command);

        public override Task ExecuteNonQueryAsync(SqlCommand command) => CompletedTask;

        public override Task ExecuteNonQueryAsync(SqlCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override void ExecuteScalar(SqlCommand command) => _sqlCommandExecutor20.ExecuteScalar(command);

        public override Task ExecuteScalarAsync(SqlCommand command) => CompletedTask;

        public override Task ExecuteScalarAsync(SqlCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override void ExecuteReader(SqlCommand command) => _sqlCommandExecutor20.ExecuteReader(command);

        public override void ExecuteReader(SqlCommand command, CommandBehavior behavior) => _sqlCommandExecutor20.ExecuteReader(command, behavior);

        public override Task ExecuteReaderAsync(SqlCommand command) => CompletedTask;

        public override Task ExecuteReaderAsync(SqlCommand command, CommandBehavior behavior) => CompletedTask;

        public override Task ExecuteReaderAsync(SqlCommand command, CancellationToken cancellationToken) => CompletedTask;

        public override Task ExecuteReaderAsync(SqlCommand command, CommandBehavior behavior, CancellationToken cancellationToken) => CompletedTask;
    }
}
