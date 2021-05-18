using System.Data;
using System.Data.Common;
using System.Data.SqlClient;

namespace Samples.DatabaseHelper.NetFramework20
{
    public class SqlCommandExecutor20
    {
        public string CommandTypeName => nameof(SqlCommand) + "20";

        public void ExecuteNonQuery(SqlCommand command) => command.ExecuteNonQuery();

        public void ExecuteScalar(SqlCommand command) => command.ExecuteScalar();

        public void ExecuteReader(SqlCommand command)
        {
            using DbDataReader reader = command.ExecuteReader();
        }

        public void ExecuteReader(SqlCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = command.ExecuteReader(behavior);
        }
    }
}
