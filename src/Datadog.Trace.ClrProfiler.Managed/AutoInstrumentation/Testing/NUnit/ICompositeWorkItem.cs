using System.Collections;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.Execution.CompositeWorkItem
    /// </summary>
    public interface ICompositeWorkItem
    {
        /// <summary>
        /// Gets the List of Child WorkItems
        /// </summary>
        IEnumerable Children { get; }
    }
}
