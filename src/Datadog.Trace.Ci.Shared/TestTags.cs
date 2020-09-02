namespace Datadog.Trace.Ci
{
    /// <summary>
    /// Span tags for test data model
    /// </summary>
    internal static class TestTags
    {
        /// <summary>
        /// Test suite name
        /// </summary>
        public const string Suite = "test.suite";

        /// <summary>
        /// Test name
        /// </summary>
        public const string Name = "test.name";

        /// <summary>
        /// Test type
        /// </summary>
        public const string Type = "test.type";

        /// <summary>
        /// Test type test
        /// </summary>
        public const string TypeTest = "test";

        /// <summary>
        /// Test type benchmark
        /// </summary>
        public const string TypeBenchmark = "benchmark";

        /// <summary>
        /// Test framework
        /// </summary>
        public const string Framework = "test.framework";

        /// <summary>
        /// Test parameters
        /// </summary>
        public const string Arguments = "test.arguments";

        /// <summary>
        /// Test traits
        /// </summary>
        public const string Traits = "test.traits";

        /// <summary>
        /// Test status
        /// </summary>
        public const string Status = "test.status";

        /// <summary>
        /// Test Pass status
        /// </summary>
        public const string StatusPass = "pass";

        /// <summary>
        /// Test Fail status
        /// </summary>
        public const string StatusFail = "fail";

        /// <summary>
        /// Test Skip status
        /// </summary>
        public const string StatusSkip = "skip";

        /// <summary>
        /// Test skip reason
        /// </summary>
        public const string SkipReason = "test.skip_reason";
    }
}
