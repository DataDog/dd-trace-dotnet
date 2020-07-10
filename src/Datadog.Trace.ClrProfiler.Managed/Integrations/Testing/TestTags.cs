namespace Datadog.Trace.ClrProfiler.Integrations.Testing
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
        /// Test fqn
        /// </summary>
        public const string Fqn = "test.fqn";

        /// <summary>
        /// Test fqn
        /// </summary>
        public const string ExecutionId = "test.executionId";

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
        public const string StatusPass = "PASS";

        /// <summary>
        /// Test Fail status
        /// </summary>
        public const string StatusFail = "FAIL";

        /// <summary>
        /// Test Skip status
        /// </summary>
        public const string StatusSkip = "SKIP";

        /// <summary>
        /// Test skip reason
        /// </summary>
        public const string SkipReason = "test.skipReason";

        /// <summary>
        /// CI Provider
        /// </summary>
        public const string CIProvider = "ci.provider";

        /// <summary>
        /// CI Repository
        /// </summary>
        public const string CIRepository = "ci.repository";

        /// <summary>
        /// CI Commit hash
        /// </summary>
        public const string CICommit = "ci.commit";

        /// <summary>
        /// CI Branch name
        /// </summary>
        public const string CIBranch = "ci.branch";

        /// <summary>
        /// CI Source root
        /// </summary>
        public const string CISourceRoot = "ci.sourceRoot";

        /// <summary>
        /// CI Build id
        /// </summary>
        public const string CIBuildId = "ci.buildId";

        /// <summary>
        /// CI Build number
        /// </summary>
        public const string CIBuildNumber = "ci.buildNumber";

        /// <summary>
        /// CI Build url
        /// </summary>
        public const string CIBuildUrl = "ci.buildUrl";

        /// <summary>
        /// Runtime name
        /// </summary>
        public const string RuntimeName = "runtime.name";

        /// <summary>
        /// Runtime os architecture
        /// </summary>
        public const string RuntimeOSArchitecture = "runtime.osArchitecture";

        /// <summary>
        /// Runtime os platform
        /// </summary>
        public const string RuntimeOSPlatform = "runtime.osPlatform";

        /// <summary>
        /// Runtime process architecture
        /// </summary>
        public const string RuntimeProcessArchitecture = "runtime.processArchitecture";

        /// <summary>
        /// Runtime version
        /// </summary>
        public const string RuntimeVersion = "runtime.version";
    }
}
