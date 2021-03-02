namespace Datadog.Trace.Ci
{
    /// <summary>
    /// Common Span tags for test/build data model
    /// </summary>
    internal static class CommonTags
    {
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
        /// OS architecture
        /// </summary>
        public const string OSArchitecture = "os.architecture";

        /// <summary>
        /// OS platform
        /// </summary>
        public const string OSPlatform = "os.platform";

        /// <summary>
        /// OS version
        /// </summary>
        public const string OSVersion = "os.version";

        /// <summary>
        /// Runtime architecture
        /// </summary>
        public const string RuntimeArchitecture = "runtime.architecture";

        /// <summary>
        /// Runtime version
        /// </summary>
        public const string RuntimeVersion = "runtime.version";
    }
}
