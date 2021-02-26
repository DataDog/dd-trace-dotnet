using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.GraphQL
{
    /// <summary>
    /// GraphQL.Language.AST.Document interface for ducktyping
    /// </summary>
    public interface IDocument
    {
        /// <summary>
        /// Gets the original query from the document
        /// </summary>
        string OriginalQuery { get; }
    }
}
