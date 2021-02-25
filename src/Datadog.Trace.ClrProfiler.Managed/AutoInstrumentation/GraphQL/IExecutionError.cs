using System.Collections.Generic;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    /// <summary>
    /// GraphQL.ExecutionError interface for ducktyping
    /// </summary>
    public interface IExecutionError
    {
        /// <summary>
        /// Gets a code for the error
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Gets a list of locations in the document where the error applies
        /// </summary>
        IEnumerable<object> Locations { get; }

        /// <summary>
        /// Gets a message for the error
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Gets the path in the document where the error applies
        /// </summary>
        IEnumerable<string> Path { get; }
    }
}
