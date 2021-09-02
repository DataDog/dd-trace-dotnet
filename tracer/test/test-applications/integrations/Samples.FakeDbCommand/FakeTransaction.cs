using System.Data;
using System.Data.Common;

namespace Samples.FakeDbCommand
{
    internal class FakeTransaction : DbTransaction
    {
        internal FakeTransaction(DbConnection connection, IsolationLevel level)
        {
            DbConnection = connection;
            IsolationLevel = level;
        }
        
        public override void Commit()
        {
        }

        public override void Rollback()
        {
        }

        protected override DbConnection DbConnection { get; }
        public override IsolationLevel IsolationLevel { get; }
    }
}
