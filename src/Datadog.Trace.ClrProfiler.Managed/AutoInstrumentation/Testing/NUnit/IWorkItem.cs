namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.Execution.WorkItem
    /// </summary>
    public interface IWorkItem
    {
        /// <summary>
        /// Gets the test result
        /// </summary>
        ITestResult Result { get; }
    }
}
