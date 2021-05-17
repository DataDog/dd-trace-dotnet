// <copyright file="CommonTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

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
        [FeatureTracking]
        public const string GitRepository = "git.repository_url";

        /// <summary>
        /// GIT Commit hash
        /// </summary>
        [FeatureTracking]
        public const string GitCommit = "git.commit.sha";

        /// <summary>
        /// GIT Branch name
        /// </summary>
        [FeatureTracking]
        public const string GitBranch = "git.branch";

        /// <summary>
        /// GIT Tag name
        /// </summary>
        [FeatureTracking]
        public const string GitTag = "git.tag";

        /// <summary>
        /// GIT Commit Author name
        /// </summary>
        [FeatureTracking]
        public const string GitCommitAuthorName = "git.commit.author.name";

        /// <summary>
        /// GIT Commit Author email
        /// </summary>
        [FeatureTracking]
        public const string GitCommitAuthorEmail = "git.commit.author.email";

        /// <summary>
        /// GIT Commit Author date
        /// </summary>
        [FeatureTracking]
        public const string GitCommitAuthorDate = "git.commit.author.date";

        /// <summary>
        /// GIT Commit Committer name
        /// </summary>
        [FeatureTracking]
        public const string GitCommitCommitterName = "git.commit.committer.name";

        /// <summary>
        /// GIT Commit Committer email
        /// </summary>
        [FeatureTracking]
        public const string GitCommitCommitterEmail = "git.commit.committer.email";

        /// <summary>
        /// GIT Commit Committer date
        /// </summary>
        [FeatureTracking]
        public const string GitCommitCommitterDate = "git.commit.committer.date";

        /// <summary>
        /// GIT Commit message
        /// </summary>
        [FeatureTracking]
        public const string GitCommitMessage = "git.commit.message";

        /// <summary>
        /// Build Source root
        /// </summary>
        [FeatureTracking]
        public const string BuildSourceRoot = "build.source_root";

        /// <summary>
        /// CI Provider
        /// </summary>
        [FeatureTracking]
        public const string CIProvider = "ci.provider.name";

        /// <summary>
        /// CI Pipeline id
        /// </summary>
        [FeatureTracking]
        public const string CIPipelineId = "ci.pipeline.id";

        /// <summary>
        /// CI Pipeline name
        /// </summary>
        [FeatureTracking]
        public const string CIPipelineName = "ci.pipeline.name";

        /// <summary>
        /// CI Pipeline number
        /// </summary>
        [FeatureTracking]
        public const string CIPipelineNumber = "ci.pipeline.number";

        /// <summary>
        /// CI Pipeline url
        /// </summary>
        [FeatureTracking]
        public const string CIPipelineUrl = "ci.pipeline.url";

        /// <summary>
        /// CI Job url
        /// </summary>
        [FeatureTracking]
        public const string CIJobUrl = "ci.job.url";

        /// <summary>
        /// CI Job Name
        /// </summary>
        [FeatureTracking]
        public const string CIJobName = "ci.job.name";

        /// <summary>
        /// CI Stage Name
        /// </summary>
        [FeatureTracking]
        public const string StageName = "ci.stage.name";

        /// <summary>
        /// CI Job url
        /// </summary>
        [FeatureTracking]
        public const string CIWorkspacePath = "ci.workspace_path";

        /// <summary>
        /// Runtime name
        /// </summary>
        [FeatureTracking]
        public const string RuntimeName = "runtime.name";

        /// <summary>
        /// OS architecture
        /// </summary>
        [FeatureTracking]
        public const string OSArchitecture = "os.architecture";

        /// <summary>
        /// OS platform
        /// </summary>
        [FeatureTracking]
        public const string OSPlatform = "os.platform";

        /// <summary>
        /// OS version
        /// </summary>
        [FeatureTracking]
        public const string OSVersion = "os.version";

        /// <summary>
        /// Runtime architecture
        /// </summary>
        [FeatureTracking]
        public const string RuntimeArchitecture = "runtime.architecture";

        /// <summary>
        /// Runtime version
        /// </summary>
        [FeatureTracking]
        public const string RuntimeVersion = "runtime.version";
    }
}
