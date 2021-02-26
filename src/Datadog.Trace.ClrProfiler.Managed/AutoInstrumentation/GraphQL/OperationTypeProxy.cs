using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    /// <summary>
    /// A proxy enum for GraphQL.Language.AST.OperationType.
    /// The enum values must match those of GraphQL.Language.AST.OperationType for spans
    /// to be decorated with the correct operation. Since the original type is public,
    /// we not expect changes between minor versions of the GraphQL library.
    /// </summary>
    public enum OperationTypeProxy
    {
        /// <summary>
        /// A query operation.
        /// </summary>
        Query,

        /// <summary>
        /// A mutation operation.
        /// </summary>
        Mutation,

        /// <summary>
        /// A subscription operation.
        /// </summary>
        Subscription
    }
}
