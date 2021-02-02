namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.TestExecutionContext
    /// </summary>
    public interface ITestExecutionContext
    {
        /// <summary>
        /// Gets the current test
        /// </summary>
        ITest CurrentTest { get; }
    }
}
