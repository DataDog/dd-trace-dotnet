using System.Data;
using System.Data.Common;

namespace Samples.FakeDbCommand
{
    public class FakeCommand : DbCommand
    {
        public FakeCommand()
        {
            DbParameterCollection = new FakeParameterCollection();
        }
        
        public override void Prepare()
        {
        }

        public override string CommandText { get; set; }
        
        public override int CommandTimeout { get; set; }
        
        public override CommandType CommandType { get; set; }
        
        public override UpdateRowSource UpdatedRowSource { get; set; }
        
        protected override DbConnection DbConnection { get; set; }
        
        protected override DbParameterCollection DbParameterCollection { get; }
        
        protected override DbTransaction DbTransaction { get; set; }
        
        public override bool DesignTimeVisible { get; set; }

        public override void Cancel()
        {
        }

        protected override DbParameter CreateDbParameter() => new FakeParameter();

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => null;

        public override int ExecuteNonQuery() => 0;

        public override object ExecuteScalar() => null;
    }
}
