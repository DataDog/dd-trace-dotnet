// <copyright file="CommonTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

namespace Datadog.Trace.Ci.Tags;

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
    /// GIT Commit Author name
    /// </summary>
    public const string GitCommitAuthorName = "git.commit.author.name";

    /// <summary>
    /// GIT Commit Author email
    /// </summary>
    public const string GitCommitAuthorEmail = "git.commit.author.email";

    /// <summary>
    /// GIT Commit Author date
    /// </summary>
    public const string GitCommitAuthorDate = "git.commit.author.date";

    /// <summary>
    /// GIT Commit Committer name
    /// </summary>
    public const string GitCommitCommitterName = "git.commit.committer.name";

    /// <summary>
    /// GIT Commit Committer email
    /// </summary>
    public const string GitCommitCommitterEmail = "git.commit.committer.email";

    /// <summary>
    /// GIT Commit Committer date
    /// </summary>
    public const string GitCommitCommitterDate = "git.commit.committer.date";

    /// <summary>
    /// GIT Commit message
    /// </summary>
    public const string GitCommitMessage = "git.commit.message";

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
    /// CI Job Name
    /// </summary>
    public const string CIJobName = "ci.job.name";

    /// <summary>
    /// CI Job id
    /// </summary>
    public const string CIJobId = "ci.job.id";

    /// <summary>
    /// CI Node Name
    /// </summary>
    public const string CINodeName = "ci.node.name";

    /// <summary>
    /// CI Node Labels
    /// </summary>
    public const string CINodeLabels = "ci.node.labels";

    /// <summary>
    /// CI Stage Name
    /// </summary>
    public const string StageName = "ci.stage.name";

    /// <summary>
    /// CI Job url
    /// </summary>
    public const string CIWorkspacePath = "ci.workspace_path";

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

    /// <summary>
    /// Library Version
    /// </summary>
    public const string LibraryVersion = "library_version";

    /// <summary>
    /// Environment variables from CI
    /// </summary>
    public const string CiEnvVars = "_dd.ci.env_vars";

    /// <summary>
    /// XUnit testing framework
    /// </summary>
    public const string TestingFrameworkNameXUnit = "xUnit";

    /// <summary>
    /// XUnit.v3 testing framework
    /// </summary>
    public const string TestingFrameworkNameXUnitV3 = "xUnit.v3";

    /// <summary>
    /// NUnit testing framework
    /// </summary>
    public const string TestingFrameworkNameNUnit = "NUnit";

    /// <summary>
    /// MSTestV2 testing framework
    /// </summary>
    public const string TestingFrameworkNameMsTestV2 = "MSTestV2";

    /// <summary>
    /// BenchmarkDotNet testing framework
    /// </summary>
    public const string TestingFrameworkNameBenchmarkDotNet = "BenchmarkDotNet";

    /// <summary>
    /// Logical CPU count
    /// </summary>
    public const string LogicalCpuCount = "_dd.host.vcpu_count";

    /// <summary>
    /// GIT PR Base commit hash
    /// </summary>
    public const string GitPrBaseCommit = "git.pull_request.base_branch_sha";

    /// <summary>
    /// GIT PR Base head commit hash
    /// </summary>
    public const string GitPrBaseHeadCommit = "git.pull_request.base_branch_head_sha";

    /// <summary>
    /// GIT PR Base branch name
    /// </summary>
    public const string GitPrBaseBranch = "git.pull_request.base_branch";

    /// <summary>
    /// PR Number
    /// </summary>
    public const string PrNumber = "pr.number";

    /// <summary>
    /// Defines if the service is a user provided test service
    /// </summary>
    public const string UserProvidedTestServiceTag = "_dd.test.is_user_provided_service";

    /// <summary>
    /// GIT Head commit hash
    /// </summary>
    public const string GitHeadCommit = "git.commit.head.sha";

    /// <summary>
    /// GIT Head Commit Author name
    /// </summary>
    public const string GitHeadCommitAuthorName = "git.commit.head.author.name";

    /// <summary>
    /// GIT Head Commit Author email
    /// </summary>
    public const string GitHeadCommitAuthorEmail = "git.commit.head.author.email";

    /// <summary>
    /// GIT Head Commit Author date
    /// </summary>
    public const string GitHeadCommitAuthorDate = "git.commit.head.author.date";

    /// <summary>
    /// GIT Head Commit Committer name
    /// </summary>
    public const string GitHeadCommitCommitterName = "git.commit.head.committer.name";

    /// <summary>
    /// GIT Head Commit Committer email
    /// </summary>
    public const string GitHeadCommitCommitterEmail = "git.commit.head.committer.email";

    /// <summary>
    /// GIT Head Commit Committer date
    /// </summary>
    public const string GitHeadCommitCommitterDate = "git.commit.head.committer.date";

    /// <summary>
    /// GIT Head Commit message
    /// </summary>
    public const string GitHeadCommitMessage = "git.commit.head.message";
}
