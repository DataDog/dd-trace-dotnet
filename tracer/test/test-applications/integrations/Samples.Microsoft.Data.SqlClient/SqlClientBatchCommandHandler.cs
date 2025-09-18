#if NET6_0_OR_GREATER && HAS_BATCH_SUPPORT
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using Samples.DatabaseHelper;

namespace Samples.Microsoft.Data.SqlClient;

public class SqlClientBatchCommandHandler : IBatchCommandHandler
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
            var npgsqlParam = new SqlParameter(parameter.Key, parameter.Value);
            batchCommand.Parameters.Add(npgsqlParam);
        }

        return batchCommand;
    }

    public void ExecuteBatch(DbBatch batch)
    {
        batch.ExecuteNonQuery();
    }

    public Task ExecuteBatchAsync(DbBatch batch)
    {
        return batch.ExecuteNonQueryAsync();
    }

    public Task ExecuteBatchAsync(DbBatch batch, CancellationToken cancellationToken)
    {
        return batch.ExecuteNonQueryAsync(cancellationToken);
    }
}
#endif
