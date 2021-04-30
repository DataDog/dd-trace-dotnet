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
    public interface IContainerNew
    {
        /// <summary>
        /// Gets the Id of the Cosmos container
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Gets the parent Database reference
        /// </summary>
        IDatabaseNew Database { get; }
    }
}
