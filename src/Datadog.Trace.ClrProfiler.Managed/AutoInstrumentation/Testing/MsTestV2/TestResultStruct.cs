using System;
using Datadog.Trace.DuckTyping;

namespace Datadog.Trace.ClrProfiler.AutoInstrumentation.Testing.MsTestV2
{
    /// <summary>
    /// TestResult ducktype struct
    /// </summary>
    [DuckCopy]
    public struct TestResultStruct
    {
        /// <summary>
        /// Gets the outcome enum
        /// </summary>
        public UnitTestOutcome Outcome;

        /// <summary>
        /// Test failure exception
        /// </summary>
        public Exception TestFailureException;
    }
}
