using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Samples.DatabaseHelper.NetFramework20
{
    public class DbCommandInterfaceExecutor20
    {
        public string CommandTypeName => nameof(IDbCommand) + "20";

        public void ExecuteNonQuery(IDbCommand command) => command.ExecuteNonQuery();

        public void ExecuteScalar(IDbCommand command) => command.ExecuteScalar();

        public void ExecuteReader(IDbCommand command)
        {
            using IDataReader reader = command.ExecuteReader();
        }

        public void ExecuteReader(IDbCommand command, CommandBehavior behavior)
        {
            using IDataReader reader = command.ExecuteReader(behavior);
        }
    }
}
