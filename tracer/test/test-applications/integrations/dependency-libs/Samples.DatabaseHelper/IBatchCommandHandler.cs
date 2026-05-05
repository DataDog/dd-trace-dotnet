#if NET6_0_OR_GREATER
using System.Collections.Generic;
using System.Data;
using System.Data.Common;

namespace Samples.DatabaseHelper;

/// <summary>
/// As opposed to regular commands that can be created in a generic way using connection.CreateCommand(),
/// batch commands need to be created using the framework specific constructor, which is why we need factory methods
/// </summary>
public interface IBatchCommandHandler
{
    /// <summary>
    /// Gets the name of the batch command type (e.g., "NpgsqlBatch", "SqlServerBatch")
    /// </summary>
    string BatchTypeName { get; }

    DbBatch CreateBatch(IDbConnection connection);

    DbBatchCommand CreateBatchCommand(string commandText, params KeyValuePair<string, object>[] parameters);
}
#endif
