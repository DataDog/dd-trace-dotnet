using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;

namespace Samples.DatabaseHelper.NetFramework20
{
    public class DbCommandClassExecutor20
    {
        public string CommandTypeName => nameof(DbCommand) + "20";

        public void ExecuteNonQuery(DbCommand command) => command.ExecuteNonQuery();

        public void ExecuteScalar(DbCommand command) => command.ExecuteScalar();

        public void ExecuteReader(DbCommand command)
        {
            using DbDataReader reader = command.ExecuteReader();
        }

        public void ExecuteReader(DbCommand command, CommandBehavior behavior)
        {
            using DbDataReader reader = command.ExecuteReader(behavior);
        }
    }
}
