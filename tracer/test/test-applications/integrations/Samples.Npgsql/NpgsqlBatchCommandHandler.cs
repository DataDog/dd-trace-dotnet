#if HAS_BATCH_SUPPORT && NET6_0_OR_GREATER
using System.Collections.Generic;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using System.Data.Common;
using Npgsql;
using Samples.DatabaseHelper;

namespace Samples.Npgsql;

public class NpgsqlBatchCommandHandler : IBatchCommandHandler
{
    public string BatchTypeName => nameof(NpgsqlBatch);

    public DbBatch CreateBatch(IDbConnection connection)
    {
        var npgsqlConnection = (NpgsqlConnection)connection;
        return new NpgsqlBatch(npgsqlConnection);
    }

    public DbBatchCommand CreateBatchCommand(string commandText, params KeyValuePair<string, object>[] parameters)
    {
        var batchCommand = new NpgsqlBatchCommand(commandText);
        foreach (var parameter in parameters)
        {
            var npgsqlParam = new NpgsqlParameter(parameter.Key, parameter.Value);
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
