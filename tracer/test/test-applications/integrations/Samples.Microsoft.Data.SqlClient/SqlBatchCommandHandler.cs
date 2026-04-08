#if HAS_BATCH_SUPPORT && NET6_0_OR_GREATER
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Samples.DatabaseHelper;

namespace Samples.Microsoft.Data.SqlClient;

public class SqlBatchCommandHandler : IBatchCommandHandler
{
    public string BatchTypeName => nameof(SqlBatch);

    public DbBatch CreateBatch(IDbConnection connection)
    {
        var sqlConnection = (SqlConnection)connection;
        return new SqlBatch(sqlConnection);
    }

    public DbBatchCommand CreateBatchCommand(string commandText, params KeyValuePair<string, object>[] parameters)
    {
        var batchCommand = new SqlBatchCommand(commandText);
        foreach (var parameter in parameters)
        {
            var sqlParam = new SqlParameter(parameter.Key, parameter.Value);
            batchCommand.Parameters.Add(sqlParam);
        }

        return batchCommand;
    }
}
#endif
