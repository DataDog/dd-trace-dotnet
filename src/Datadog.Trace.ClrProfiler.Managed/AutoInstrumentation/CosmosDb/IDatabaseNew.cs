using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.Database for duct typing
    /// </summary>
    public interface IDatabaseNew
    {
        /// <summary>
        /// Gets the Id of the Cosmos database
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the parent Cosmos client instance related the database instance
        /// </summary>
        ICosmosClient Client { get; }
    }
}
