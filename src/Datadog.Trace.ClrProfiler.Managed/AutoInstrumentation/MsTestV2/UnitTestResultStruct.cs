using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.MsTestV2
{
    /// <summary>
    /// UnitTestResult ducktype struct
    /// </summary>
    [DuckCopy]
    public struct UnitTestResultStruct
    {
        /// <summary>
        /// Gets the error message
        /// </summary>
        public string ErrorMessage;

        /// <summary>
        /// Gets the error stacktrace
        /// </summary>
        public string ErrorStackTrace;

        /// <summary>
        /// Gets the outcome enum
        /// </summary>
        public UnitTestResultOutcome Outcome;
    }
}
