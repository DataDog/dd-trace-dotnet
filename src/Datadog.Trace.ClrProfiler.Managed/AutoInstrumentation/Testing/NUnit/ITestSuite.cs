using System.Collections;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.TestSuite
    /// </summary>
    public interface ITestSuite : ITest
    {
        /// <summary>
        /// Gets the children tests
        /// </summary>
        IEnumerable Tests { get; }
    }
}
