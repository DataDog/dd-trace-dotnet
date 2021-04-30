using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.CosmosDb
{
    /// <summary>
    /// Microsoft.Azure.Cosmos.Container for duck typing
    /// </summary>
    public interface IContainerOld
    {
        /// <summary>
        /// Gets the Id of the Cosmos container
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the parent Database reference
        /// </summary>
        IDatabaseOld Database { get; }
    }
}
