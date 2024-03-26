using System.Data;
using System.Data.Common;

namespace Samples.FakeDbCommand
{
    internal class FakeConnection : DbConnection
    {
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => new FakeTransaction(this, isolationLevel);

        public override void Close()
        {
        }

        public override void ChangeDatabase(string databaseName)
        {
        }

        public override void Open()
        {
        }

        public override string ConnectionString { get; set; } = "Database=fake";
        
        public override string Database { get; } = "Database";
        
        public override ConnectionState State { get; }
        
        public override string DataSource { get; } = "DataSource";
        
        public override string ServerVersion { get; } = "ServerVersion";

        protected override DbCommand CreateDbCommand()
        {
            return new FakeCommand { Connection =  this };
        }
    }
}
