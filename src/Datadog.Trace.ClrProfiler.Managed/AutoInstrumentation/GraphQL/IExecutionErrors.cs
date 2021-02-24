using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    /// <summary>
    /// GraphQL.ExecutionErrors interface for ducktyping
    /// </summary>
    public interface IExecutionErrors
    {
        /// <summary>
        /// Gets the number of errors
        /// </summary>
        int Count { get; }
    }
}
