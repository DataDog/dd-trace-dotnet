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

        /// <summary>
        /// GIT Repository
        /// </summary>
        public const string GitRepository = "git.repository_url";

        /// <summary>
        /// GIT Commit hash
        /// </summary>
        public const string GitCommit = "git.commit_sha";

        /// <summary>
        /// GIT Branch name
        /// </summary>
        public const string GitBranch = "git.branch";

        /// <summary>
        /// Build Source root
        /// </summary>
        public const string BuildSourceRoot = "build.source_root";

        /// <summary>
        /// CI Provider
        /// </summary>
        public const string CIProvider = "ci.provider.name";

        /// <summary>
        /// CI Pipeline id
        /// </summary>
        public const string CIPipelineId = "ci.pipeline.id";

        /// <summary>
        /// CI Pipeline number
        /// </summary>
        public const string CIPipelineNumber = "ci.pipeline.number";

        /// <summary>
        /// CI Pipeline url
        /// </summary>
        public const string CIPipelineUrl = "ci.pipeline.url";

        /// <summary>
        /// CI Job url
        /// </summary>
        public const string CIJobUrl = "ci.job.url";

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
