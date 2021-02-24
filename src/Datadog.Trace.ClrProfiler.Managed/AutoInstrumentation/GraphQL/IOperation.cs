using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    /// <summary>
    /// GraphQL.Language.AST.Operation interface for ducktyping
    /// </summary>
    public interface IOperation
    {
        /// <summary>
        /// Gets the name of the operation
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Gets the type of the operation
        /// </summary>
        OperationTypeProxy OperationType { get; }
    }
}
