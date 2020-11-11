using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace Samples.DatabaseHelper.NetFramework20
{
    public class DbCommandInterfaceGenericExecutor20<TCommand>
        where TCommand : IDbCommand
    {
        public string CommandTypeName => nameof(TCommand) + "20";

        public void ExecuteNonQuery(TCommand command) => command.ExecuteNonQuery();

        public void ExecuteScalar(TCommand command) => command.ExecuteScalar();

        public void ExecuteReader(TCommand command)
        {
            using IDataReader reader = command.ExecuteReader();
        }

        public void ExecuteReader(TCommand command, CommandBehavior behavior)
        {
            using IDataReader reader = command.ExecuteReader(behavior);
        }
    }
}
