using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.QueryDefinition for duck typing
    /// </summary>
    public interface IQueryDefinition
    {
        /// <summary>
        /// Gets the text of the Azure Cosmos DB SQL query.
        /// </summary>
        /// <value>The text of the SQL query.</value>
        string QueryText { get; }
    }
}
