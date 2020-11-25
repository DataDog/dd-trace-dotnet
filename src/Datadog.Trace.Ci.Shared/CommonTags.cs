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
        public const string GitCommit = "git.commit.sha";

        /// <summary>
        /// GIT Branch name
        /// </summary>
        public const string GitBranch = "git.branch";

        /// <summary>
        /// GIT Tag name
        /// </summary>
        public const string GitTag = "git.tag";

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
        /// CI Pipeline name
        /// </summary>
        public const string CIPipelineName = "ci.pipeline.name";

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
        /// CI Job url
        /// </summary>
        public const string CIWorkspacePath = "ci.workspace_path";

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
