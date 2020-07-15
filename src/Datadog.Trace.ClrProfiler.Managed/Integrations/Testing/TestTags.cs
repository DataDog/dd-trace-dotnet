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
        public const string Id = "test.id";

        /// <summary>
        /// Test fqn
        /// </summary>
        public const string ProcessId = "test.processId";

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
        public const string SkipReason = "test.skip_reason";

        /// <summary>
        /// GIT Repository
        /// </summary>
        public const string GitRepository = "git.repository";

        /// <summary>
        /// GIT Commit hash
        /// </summary>
        public const string GitCommit = "git.commit";

        /// <summary>
        /// GIT Branch name
        /// </summary>
        public const string GitBranch = "git.branch";

        /// <summary>
        /// Build Source root
        /// </summary>
        public const string BuildSourceRoot = "build.source_root";

        /// <summary>
        /// Build InContainer flag
        /// </summary>
        public const string BuildInContainer = "build.incontainer";

        /// <summary>
        /// CI Provider
        /// </summary>
        public const string CIProvider = "ci.provider";

        /// <summary>
        /// CI Build id
        /// </summary>
        public const string CIBuildId = "ci.build_id";

        /// <summary>
        /// CI Build number
        /// </summary>
        public const string CIBuildNumber = "ci.build_number";

        /// <summary>
        /// CI Build url
        /// </summary>
        public const string CIBuildUrl = "ci.build_url";

        /// <summary>
        /// Runtime name
        /// </summary>
        public const string RuntimeName = "runtime.name";

        /// <summary>
        /// Runtime os architecture
        /// </summary>
        public const string RuntimeOSArchitecture = "runtime.os_architecture";

        /// <summary>
        /// Runtime os platform
        /// </summary>
        public const string RuntimeOSPlatform = "runtime.os_platform";

        /// <summary>
        /// Runtime process architecture
        /// </summary>
        public const string RuntimeProcessArchitecture = "runtime.process_architecture";

        /// <summary>
        /// Runtime version
        /// </summary>
        public const string RuntimeVersion = "runtime.version";
    }
}
