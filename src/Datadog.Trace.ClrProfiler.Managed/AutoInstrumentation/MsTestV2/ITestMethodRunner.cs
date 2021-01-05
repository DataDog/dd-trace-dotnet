using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MsTestV2
{
    /// <summary>
    /// TestMethodRunner ducktype interface
    /// </summary>
    public interface ITestMethodRunner
    {
        /// <summary>
        /// Gets the TestMethodInfo instance
        /// </summary>
        [Duck(Name = "testMethodInfo", Kind = DuckKind.Field)]
        ITestMethod TestMethodInfo { get; }
    }
}
