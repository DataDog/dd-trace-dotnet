namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.NUnit
{
    /// <summary>
    /// DuckTyping interface for NUnit.Framework.Internal.TestResult
    /// </summary>
    public interface ITestResult
    {
        /// <summary>
        /// Gets the test with which this result is associated.
        /// </summary>
        ITest Test { get; }
    }
}
