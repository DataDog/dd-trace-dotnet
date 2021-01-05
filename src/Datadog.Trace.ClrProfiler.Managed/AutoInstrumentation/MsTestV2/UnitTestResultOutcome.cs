namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MsTestV2
{
    /// <summary>
    /// UnitTestResult Outcome
    /// </summary>
    public enum UnitTestResultOutcome
    {
        /// <summary>
        /// Error
        /// </summary>
        Error,

        /// <summary>
        /// Failed
        /// </summary>
        Failed,

        /// <summary>
        /// Timeout
        /// </summary>
        Timeout,

        /// <summary>
        /// Inconclusive
        /// </summary>
        Inconclusive,

        /// <summary>
        /// Ignored
        /// </summary>
        Ignored,

        /// <summary>
        /// Not Runnable
        /// </summary>
        NotRunnable,

        /// <summary>
        /// Passed
        /// </summary>
        Passed,

        /// <summary>
        /// Not Found
        /// </summary>
        NotFound,

        /// <summary>
        /// In Progress
        /// </summary>
        InProgress,
    }
}
