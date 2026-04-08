#if HAS_BATCH_SUPPORT && NET6_0_OR_GREATER
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
#if MYSQLCONNECTOR_0
using MySql.Data.MySqlClient;
#else
using MySqlConnector;
#endif
using Samples.DatabaseHelper;

namespace Samples.MySqlConnector;

public class MySqlBatchCommandHandler : IBatchCommandHandler
{
    public string BatchTypeName => nameof(MySqlBatch);

    public DbBatch CreateBatch(IDbConnection connection)
    {
        var mySqlConnection = (MySqlConnection)connection;
        return new MySqlBatch(mySqlConnection);
    }

    public DbBatchCommand CreateBatchCommand(string commandText, params KeyValuePair<string, object>[] parameters)
    {
        var batchCommand = new MySqlBatchCommand(commandText);
        foreach (var parameter in parameters)
        {
            var mySqlParameter = new MySqlParameter(parameter.Key, parameter.Value);
            batchCommand.Parameters.Add(mySqlParameter);
        }

        return batchCommand;
    }
}
#endif
