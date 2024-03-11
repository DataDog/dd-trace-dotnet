// <copyright file="CIEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;

namespace Datadog.Trace.Ci.CiEnvironment;

// ReSharper disable once InconsistentNaming

internal abstract class CIEnvironmentValues
{
    internal const string RepositoryUrlPattern = @"((http|git|ssh|http(s)|file|\/?)|(git@[\w\.\-]+))(:(\/\/)?)([\w\.@\:/\-~]+)(\.git)?(\/)?";
    protected static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CIEnvironmentValues));
    private static readonly Lazy<CIEnvironmentValues> LazyInstance = new(Create);

    private string? _gitSearchFolder = null;

    public static CIEnvironmentValues Instance => LazyInstance.Value;

    public string? GitSearchFolder
    {
        get => _gitSearchFolder;
        set
        {
            _gitSearchFolder = value;
            ReloadEnvironmentData();
        }
    }

    public bool IsCI { get; protected set; }

    public string? Provider { get; protected set; }

    public string? Repository { get; protected set; }

    public string? Commit { get; protected set; }

    public string? Branch { get; protected set; }

    public string? Tag { get; protected set; }

    public string? AuthorName { get; protected set; }

    public string? AuthorEmail { get; protected set; }

    public DateTimeOffset? AuthorDate { get; protected set; }

    public string? CommitterName { get; protected set; }

    public string? CommitterEmail { get; protected set; }

    public DateTimeOffset? CommitterDate { get; protected set; }

    public string? Message { get; protected set; }

    public string? SourceRoot { get; protected set; }

    public string? PipelineId { get; protected set; }

    public string? PipelineName { get; protected set; }

    public string? PipelineNumber { get; protected set; }

    public string? PipelineUrl { get; protected set; }

    public string? JobUrl { get; protected set; }

    public string? JobName { get; protected set; }

    public string? StageName { get; protected set; }

    public string? WorkspacePath { get; protected set; }

    public string? NodeName { get; protected set; }

    public string[]? NodeLabels { get; protected set; }

    public CodeOwners? CodeOwners { get; protected set; }

    public Dictionary<string, string?>? VariablesToBypass { get; protected set; }

    public static CIEnvironmentValues Create()
    {
        var values = CIEnvironmentValues<EnvironmentVariablesProvider>.Create(new EnvironmentVariablesProvider());
        values.ReloadEnvironmentData();
        return values;
    }

    public static CIEnvironmentValues Create(Dictionary<string, string> source)
    {
        var values = CIEnvironmentValues<DictionaryValuesProvider>.Create(new DictionaryValuesProvider(source));
        values.ReloadEnvironmentData();
        return values;
    }

    public static string? RemoveSensitiveInformationFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var value = uri.GetComponents(UriComponents.Fragment | UriComponents.Query | UriComponents.Path | UriComponents.Port | UriComponents.Host | UriComponents.Scheme, UriFormat.SafeUnescaped);
                // In some cases `GetComponents` introduces a slash at the end of the url
                if (!url!.EndsWith("/") && value.EndsWith("/"))
                {
                    value = value.Substring(0, value.Length - 1);
                }

                return value;
            }
        }
        else
        {
            var urlPattern = new Regex("^(ssh://)(.*@)(.*)");
            var urlMatch = urlPattern.Match(url);
            if (urlMatch.Success)
            {
                url = urlMatch.Result("$1$3");
            }
        }

        return url;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SetTagIfNotNullOrEmpty(Span span, string key, string? value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            span.SetTag(key, value);
        }
    }

    protected static bool IsHex(IEnumerable<char> chars)
    {
        foreach (var c in chars)
        {
            var isHex = (c is >= '0' and <= '9' ||
                         c is >= 'a' and <= 'f' ||
                         c is >= 'A' and <= 'F');

            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }

    public void DecorateSpan(Span span)
    {
        if (span == null)
        {
            return;
        }

        SetTagIfNotNullOrEmpty(span, CommonTags.CIProvider, Provider);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIPipelineId, PipelineId);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIPipelineName, PipelineName);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIPipelineNumber, PipelineNumber);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIPipelineUrl, PipelineUrl);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIJobUrl, JobUrl);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIJobName, JobName);
        SetTagIfNotNullOrEmpty(span, CommonTags.CINodeName, NodeName);
        if (NodeLabels is { } nodeLabels)
        {
            SetTagIfNotNullOrEmpty(span, CommonTags.CINodeLabels, Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(nodeLabels));
        }

        SetTagIfNotNullOrEmpty(span, CommonTags.StageName, StageName);
        SetTagIfNotNullOrEmpty(span, CommonTags.CIWorkspacePath, WorkspacePath);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitRepository, Repository);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommit, Commit);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitBranch, Branch);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitTag, Tag);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitAuthorName, AuthorName);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitAuthorEmail, AuthorEmail);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitAuthorDate, AuthorDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitCommitterName, CommitterName);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitCommitterEmail, CommitterEmail);
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitCommitterDate, CommitterDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
        SetTagIfNotNullOrEmpty(span, CommonTags.GitCommitMessage, Message);
        SetTagIfNotNullOrEmpty(span, CommonTags.BuildSourceRoot, SourceRoot);
        if (VariablesToBypass is { } variablesToBypass)
        {
            span.SetTag(CommonTags.CiEnvVars, Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.SerializeObject(variablesToBypass));
        }
    }

    protected void ReloadEnvironmentData()
    {
        // **********
        // Setup variables
        // **********
        Log.Information("CIEnvironmentValues: Loading environment data.");

        Provider = null;
        PipelineId = null;
        PipelineName = null;
        PipelineNumber = null;
        PipelineUrl = null;
        JobUrl = null;
        JobName = null;
        StageName = null;
        WorkspacePath = null;
        Repository = null;
        Commit = null;
        Branch = null;
        Tag = null;
        AuthorName = null;
        AuthorEmail = null;
        AuthorDate = null;
        CommitterName = null;
        CommitterEmail = null;
        CommitterDate = null;
        Message = null;
        SourceRoot = null;

        Setup(string.IsNullOrEmpty(_gitSearchFolder) ? GitInfo.GetCurrent() : GitInfo.GetFrom(_gitSearchFolder));

        // **********
        // Remove sensitive info from repository url
        // **********
        Repository = RemoveSensitiveInformationFromUrl(Repository);

        // **********
        // Clean Refs
        // **********

        CleanBranchAndTag();

        // **********
        // Sanitize Repository Url (Remove username:password info from the url)
        // **********
        if (!string.IsNullOrEmpty(Repository) &&
            Uri.TryCreate(Repository, UriKind.Absolute, out var uriRepository) &&
            !string.IsNullOrEmpty(uriRepository.UserInfo))
        {
            Repository = Repository!.Replace(uriRepository.UserInfo + "@", string.Empty);
            Repository = Repository.Replace(uriRepository.UserInfo, string.Empty);
        }

        // **********
        // Try load CodeOwners
        // **********
        if (!string.IsNullOrEmpty(SourceRoot))
        {
            foreach (var codeOwnersPath in GetCodeOwnersPaths(SourceRoot!))
            {
                Log.Debug("Looking for CODEOWNERS file in: {Path}", codeOwnersPath);
                if (File.Exists(codeOwnersPath))
                {
                    Log.Information("CODEOWNERS file found: {Path}", codeOwnersPath);
                    CodeOwners = new CodeOwners(codeOwnersPath);
                    break;
                }
            }
        }

        static IEnumerable<string> GetCodeOwnersPaths(string sourceRoot)
        {
            yield return Path.Combine(sourceRoot, "CODEOWNERS");
            yield return Path.Combine(sourceRoot, ".github", "CODEOWNERS");
            yield return Path.Combine(sourceRoot, ".gitlab", "CODEOWNERS");
            yield return Path.Combine(sourceRoot, ".docs", "CODEOWNERS");
        }
    }

    protected abstract void Setup(GitInfo gitInfo);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void CleanBranchAndTag()
    {
        var regex = new Regex(@"^refs\/heads\/tags\/(.*)|refs\/heads\/(.*)|refs\/tags\/(.*)|refs\/(.*)|origin\/tags\/(.*)|origin\/(.*)$");

        try
        {
            // Clean tag name
            if (!string.IsNullOrEmpty(Tag))
            {
                var match = regex.Match(Tag);
                if (match.Success && match.Groups.Count == 7)
                {
                    Tag =
                        !string.IsNullOrWhiteSpace(match.Groups[1].Value) ? match.Groups[1].Value :
                        !string.IsNullOrWhiteSpace(match.Groups[3].Value) ? match.Groups[3].Value :
                        !string.IsNullOrWhiteSpace(match.Groups[5].Value) ? match.Groups[5].Value :
                        !string.IsNullOrWhiteSpace(match.Groups[2].Value) ? match.Groups[2].Value :
                                                                            match.Groups[4].Value;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error fixing tag name: {TagName}", Tag);
        }

        try
        {
            // Clean branch name
            if (!string.IsNullOrEmpty(Branch))
            {
                var match = regex.Match(Branch);
                if (match.Success && match.Groups.Count == 7)
                {
                    Branch =
                        !string.IsNullOrWhiteSpace(match.Groups[2].Value) ? match.Groups[2].Value :
                        !string.IsNullOrWhiteSpace(match.Groups[4].Value) ? match.Groups[4].Value :
                                                                            match.Groups[6].Value;

                    if (string.IsNullOrEmpty(Tag))
                    {
                        Tag =
                            !string.IsNullOrWhiteSpace(match.Groups[1].Value) ? match.Groups[1].Value :
                            !string.IsNullOrWhiteSpace(match.Groups[3].Value) ? match.Groups[3].Value :
                                                                                match.Groups[5].Value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error fixing branch name: {BranchName}", Branch);
        }

        if (string.IsNullOrEmpty(Tag))
        {
            Tag = null;
        }

        if (string.IsNullOrEmpty(Branch))
        {
            Branch = null;
        }
    }

    public string MakeRelativePathFromSourceRoot(string absolutePath, bool useOSSeparator = true)
    {
        var pivotFolder = SourceRoot;
        if (string.IsNullOrEmpty(pivotFolder))
        {
            return absolutePath;
        }

        if (string.IsNullOrEmpty(absolutePath))
        {
            return pivotFolder!;
        }

        try
        {
            var folderSeparator = Path.DirectorySeparatorChar;
            if (pivotFolder![pivotFolder.Length - 1] != folderSeparator)
            {
                pivotFolder += folderSeparator;
            }

            var pivotFolderUri = new Uri(pivotFolder);
            var absolutePathUri = new Uri(absolutePath);
            var relativeUri = pivotFolderUri.MakeRelativeUri(absolutePathUri);
            if (useOSSeparator)
            {
                return Uri.UnescapeDataString(
                    relativeUri.ToString().Replace('/', folderSeparator));
            }

            return Uri.UnescapeDataString(relativeUri.ToString());
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error creating a relative path for '{AbsolutePath}' from '{BasePath}'", absolutePath, pivotFolder);
        }

        return absolutePath;
    }

    internal sealed class Constants
    {
        public const string Home = "HOME";
        public const string UserProfile = "USERPROFILE";

        // Travis CI Environment variables
        public const string Travis = "TRAVIS";
        public const string TravisPullRequestSlug = "TRAVIS_PULL_REQUEST_SLUG";
        public const string TravisRepoSlug = "TRAVIS_REPO_SLUG";
        public const string TravisCommit = "TRAVIS_COMMIT";
        public const string TravisTag = "TRAVIS_TAG";
        public const string TravisPullRequestBranch = "TRAVIS_PULL_REQUEST_BRANCH";
        public const string TravisBranch = "TRAVIS_BRANCH";
        public const string TravisBuildDir = "TRAVIS_BUILD_DIR";
        public const string TravisBuildId = "TRAVIS_BUILD_ID";
        public const string TravisBuildNumber = "TRAVIS_BUILD_NUMBER";
        public const string TravisBuildWebUrl = "TRAVIS_BUILD_WEB_URL";
        public const string TravisJobWebUrl = "TRAVIS_JOB_WEB_URL";
        public const string TravisCommitMessage = "TRAVIS_COMMIT_MESSAGE";

        // Circle CI Environment variables
        public const string CircleCI = "CIRCLECI";
        public const string CircleCIWorkflowId = "CIRCLE_WORKFLOW_ID";
        public const string CircleCIBuildNum = "CIRCLE_BUILD_NUM";
        public const string CircleCIRepositoryUrl = "CIRCLE_REPOSITORY_URL";
        public const string CircleCISha = "CIRCLE_SHA1";
        public const string CircleCITag = "CIRCLE_TAG";
        public const string CircleCIBranch = "CIRCLE_BRANCH";
        public const string CircleCIWorkingDirectory = "CIRCLE_WORKING_DIRECTORY";
        public const string CircleCIProjectRepoName = "CIRCLE_PROJECT_REPONAME";
        public const string CircleCIJob = "CIRCLE_JOB";
        public const string CircleCIBuildUrl = "CIRCLE_BUILD_URL";

        // Jenkins Environment variables
        public const string JenkinsUrl = "JENKINS_URL";
        public const string JenkinsCustomTraceId = "DD_CUSTOM_TRACE_ID";
        public const string JenkinsGitUrl = "GIT_URL";
        public const string JenkinsGitUrl1 = "GIT_URL_1";
        public const string JenkinsGitCommit = "GIT_COMMIT";
        public const string JenkinsGitBranch = "GIT_BRANCH";
        public const string JenkinsWorkspace = "WORKSPACE";
        public const string JenkinsBuildTag = "BUILD_TAG";
        public const string JenkinsBuildNumber = "BUILD_NUMBER";
        public const string JenkinsBuildUrl = "BUILD_URL";
        public const string JenkinsJobName = "JOB_NAME";
        public const string JenkinsNodeName = "NODE_NAME";
        public const string JenkinsNodeLabels = "NODE_LABELS";

        // Gitlab Environment variables
        public const string GitlabCI = "GITLAB_CI";
        public const string GitlabProjectUrl = "CI_PROJECT_URL";
        public const string GitlabPipelineId = "CI_PIPELINE_ID";
        public const string GitlabJobId = "CI_JOB_ID";
        public const string GitlabRepositoryUrl = "CI_REPOSITORY_URL";
        public const string GitlabCommitSha = "CI_COMMIT_SHA";
        public const string GitlabCommitBranch = "CI_COMMIT_BRANCH";
        public const string GitlabCommitTag = "CI_COMMIT_TAG";
        public const string GitlabCommitRefName = "CI_COMMIT_REF_NAME";
        public const string GitlabProjectDir = "CI_PROJECT_DIR";
        public const string GitlabProjectPath = "CI_PROJECT_PATH";
        public const string GitlabPipelineIId = "CI_PIPELINE_IID";
        public const string GitlabPipelineUrl = "CI_PIPELINE_URL";
        public const string GitlabJobUrl = "CI_JOB_URL";
        public const string GitlabJobName = "CI_JOB_NAME";
        public const string GitlabJobStage = "CI_JOB_STAGE";
        public const string GitlabCommitMessage = "CI_COMMIT_MESSAGE";
        public const string GitlabCommitAuthor = "CI_COMMIT_AUTHOR";
        public const string GitlabCommitTimestamp = "CI_COMMIT_TIMESTAMP";
        public const string GitlabRunnerId = "CI_RUNNER_ID";
        public const string GitlabRunnerTags = "CI_RUNNER_TAGS";

        // Appveyor CI Environment variables
        public const string Appveyor = "APPVEYOR";
        public const string AppveyorRepoProvider = "APPVEYOR_REPO_PROVIDER";
        public const string AppveyorRepoName = "APPVEYOR_REPO_NAME";
        public const string AppveyorRepoCommit = "APPVEYOR_REPO_COMMIT";
        public const string AppveyorBuildFolder = "APPVEYOR_BUILD_FOLDER";
        public const string AppveyorBuildId = "APPVEYOR_BUILD_ID";
        public const string AppveyorBuildNumber = "APPVEYOR_BUILD_NUMBER";
        public const string AppveyorPullRequestHeadRepoBranch = "APPVEYOR_PULL_REQUEST_HEAD_REPO_BRANCH";
        public const string AppveyorRepoTagName = "APPVEYOR_REPO_TAG_NAME";
        public const string AppveyorRepoBranch = "APPVEYOR_REPO_BRANCH";
        public const string AppveyorRepoCommitMessage = "APPVEYOR_REPO_COMMIT_MESSAGE";
        public const string AppveyorRepoCommitMessageExtended = "APPVEYOR_REPO_COMMIT_MESSAGE_EXTENDED";
        public const string AppveyorRepoCommitAuthor = "APPVEYOR_REPO_COMMIT_AUTHOR";
        public const string AppveyorRepoCommitAuthorEmail = "APPVEYOR_REPO_COMMIT_AUTHOR_EMAIL";

        // Azure CI Environment variables
        public const string AzureTFBuild = "TF_BUILD";
        public const string AzureSystemTeamProjectId = "SYSTEM_TEAMPROJECTID";
        public const string AzureBuildBuildId = "BUILD_BUILDID";
        public const string AzureSystemJobId = "SYSTEM_JOBID";
        public const string AzureBuildSourcesDirectory = "BUILD_SOURCESDIRECTORY";
        public const string AzureBuildDefinitionName = "BUILD_DEFINITIONNAME";
        public const string AzureSystemTeamFoundationServerUri = "SYSTEM_TEAMFOUNDATIONSERVERURI";
        public const string AzureSystemStageDisplayName = "SYSTEM_STAGEDISPLAYNAME";
        public const string AzureSystemJobDisplayName = "SYSTEM_JOBDISPLAYNAME";
        public const string AzureSystemTaskInstanceId = "SYSTEM_TASKINSTANCEID";
        public const string AzureSystemPullRequestSourceRepositoryUri = "SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI";
        public const string AzureBuildRepositoryUri = "BUILD_REPOSITORY_URI";
        public const string AzureSystemPullRequestSourceCommitId = "SYSTEM_PULLREQUEST_SOURCECOMMITID";
        public const string AzureBuildSourceVersion = "BUILD_SOURCEVERSION";
        public const string AzureSystemPullRequestSourceBranch = "SYSTEM_PULLREQUEST_SOURCEBRANCH";
        public const string AzureBuildSourceBranch = "BUILD_SOURCEBRANCH";
        public const string AzureBuildSourceBranchName = "BUILD_SOURCEBRANCHNAME";
        public const string AzureBuildSourceVersionMessage = "BUILD_SOURCEVERSIONMESSAGE";
        public const string AzureBuildRequestedForId = "BUILD_REQUESTEDFORID";
        public const string AzureBuildRequestedForEmail = "BUILD_REQUESTEDFOREMAIL";

        // BitBucket CI Environment variables
        public const string BitBucketCommit = "BITBUCKET_COMMIT";
        public const string BitBucketGitSshOrigin = "BITBUCKET_GIT_SSH_ORIGIN";
        public const string BitBucketGitHttpsOrigin = "BITBUCKET_GIT_HTTP_ORIGIN";
        public const string BitBucketBranch = "BITBUCKET_BRANCH";
        public const string BitBucketTag = "BITBUCKET_TAG";
        public const string BitBucketCloneDir = "BITBUCKET_CLONE_DIR";
        public const string BitBucketPipelineUuid = "BITBUCKET_PIPELINE_UUID";
        public const string BitBucketBuildNumber = "BITBUCKET_BUILD_NUMBER";
        public const string BitBucketRepoFullName = "BITBUCKET_REPO_FULL_NAME";

        // GitHub CI Environment variables
        public const string GitHubSha = "GITHUB_SHA";
        public const string GitHubServerUrl = "GITHUB_SERVER_URL";
        public const string GitHubRepository = "GITHUB_REPOSITORY";
        public const string GitHubRunId = "GITHUB_RUN_ID";
        public const string GitHubRunAttempt = "GITHUB_RUN_ATTEMPT";
        public const string GitHubHeadRef = "GITHUB_HEAD_REF";
        public const string GitHubRef = "GITHUB_REF";
        public const string GitHubWorkspace = "GITHUB_WORKSPACE";
        public const string GitHubRunNumber = "GITHUB_RUN_NUMBER";
        public const string GitHubWorkflow = "GITHUB_WORKFLOW";
        public const string GitHubJob = "GITHUB_JOB";

        // Teamcity CI Environment variables
        public const string TeamCityVersion = "TEAMCITY_VERSION";
        public const string TeamCityBuildConfName = "TEAMCITY_BUILDCONF_NAME";
        public const string TeamCityBuildUrl = "BUILD_URL";

        // BuildKite CI Environment variables
        public const string BuildKite = "BUILDKITE";
        public const string BuildKiteBuildId = "BUILDKITE_BUILD_ID";
        public const string BuildKiteJobId = "BUILDKITE_JOB_ID";
        public const string BuildKiteRepo = "BUILDKITE_REPO";
        public const string BuildKiteCommit = "BUILDKITE_COMMIT";
        public const string BuildKiteBranch = "BUILDKITE_BRANCH";
        public const string BuildKiteTag = "BUILDKITE_TAG";
        public const string BuildKiteBuildCheckoutPath = "BUILDKITE_BUILD_CHECKOUT_PATH";
        public const string BuildKiteBuildNumber = "BUILDKITE_BUILD_NUMBER";
        public const string BuildKitePipelineSlug = "BUILDKITE_PIPELINE_SLUG";
        public const string BuildKiteBuildUrl = "BUILDKITE_BUILD_URL";
        public const string BuildKiteMessage = "BUILDKITE_MESSAGE";
        public const string BuildKiteBuildAuthor = "BUILDKITE_BUILD_AUTHOR";
        public const string BuildKiteBuildAuthorEmail = "BUILDKITE_BUILD_AUTHOR_EMAIL";
        public const string BuildKiteBuildCreator = "BUILDKITE_BUILD_CREATOR";
        public const string BuildKiteBuildCreatorEmail = "BUILDKITE_BUILD_CREATOR_EMAIL";
        public const string BuildKiteAgentId = "BUILDKITE_AGENT_ID";
        public const string BuildKiteAgentMetadata = "BUILDKITE_AGENT_META_DATA_";

        // Bitrise CI Environment variables
        public const string BitriseBuildSlug = "BITRISE_BUILD_SLUG";
        public const string BitriseGitRepositoryUrl = "GIT_REPOSITORY_URL";
        public const string BitriseGitCommit = "BITRISE_GIT_COMMIT";
        public const string BitriseGitCloneCommitHash = "GIT_CLONE_COMMIT_HASH";
        public const string BitriseGitBranchDest = "BITRISEIO_GIT_BRANCH_DEST";
        public const string BitriseGitBranch = "BITRISE_GIT_BRANCH";
        public const string BitriseGitTag = "BITRISE_GIT_TAG";
        public const string BitriseSourceDir = "BITRISE_SOURCE_DIR";
        public const string BitriseBuildNumber = "BITRISE_BUILD_NUMBER";
        public const string BitriseTriggeredWorkflowId = "BITRISE_TRIGGERED_WORKFLOW_ID";
        public const string BitriseBuildUrl = "BITRISE_BUILD_URL";
        public const string BitriseGitMessage = "BITRISE_GIT_MESSAGE";
        public const string BitriseCloneCommitAuthorName = "GIT_CLONE_COMMIT_AUTHOR_NAME";
        public const string BitriseCloneCommitAuthorEmail = "GIT_CLONE_COMMIT_AUTHOR_EMAIL";
        public const string BitriseCloneCommitCommiterName = "GIT_CLONE_COMMIT_COMMITER_NAME";
        public const string BitriseCloneCommitCommiterEmail = "GIT_CLONE_COMMIT_COMMITER_EMAIL";

        // Buddy CI Environment variables
        public const string Buddy = "BUDDY";
        public const string BuddyScmUrl = "BUDDY_SCM_URL";
        public const string BuddyExecutionRevision = "BUDDY_EXECUTION_REVISION";
        public const string BuddyExecutionBranch = "BUDDY_EXECUTION_BRANCH";
        public const string BuddyExecutionTag = "BUDDY_EXECUTION_TAG";
        public const string BuddyPipelineId = "BUDDY_PIPELINE_ID";
        public const string BuddyExecutionId = "BUDDY_EXECUTION_ID";
        public const string BuddyPipelineName = "BUDDY_PIPELINE_NAME";
        public const string BuddyExecutionUrl = "BUDDY_EXECUTION_URL";
        public const string BuddyExecutionRevisionMessage = "BUDDY_EXECUTION_REVISION_MESSAGE";
        public const string BuddyExecutionRevisionCommitterName = "BUDDY_EXECUTION_REVISION_COMMITTER_NAME";
        public const string BuddyExecutionRevisionCommitterEmail = "BUDDY_EXECUTION_REVISION_COMMITTER_EMAIL";

        // Codefresh CI Environment variables
        public const string CodefreshBuildId = "CF_BUILD_ID";
        public const string CodefreshPipelineName = "CF_PIPELINE_NAME";
        public const string CodefreshBuildUrl = "CF_BUILD_URL";
        public const string CodefreshStepName = "CF_STEP_NAME";
        public const string CodefreshBranch = "CF_BRANCH";

        // AWS CodePipeline
        public const string AWSCodePipelineId = "DD_PIPELINE_EXECUTION_ID";
        public const string AWSCodePipelineBuildInitiator = "CODEBUILD_INITIATOR";
        public const string AWSCodePipelineBuildArn = "CODEBUILD_BUILD_ARN";
        public const string AWSCodePipelineActionExecutionId = "DD_ACTION_EXECUTION_ID";

        // Datadog Custom CI Environment variables
        public const string DDGitBranch = "DD_GIT_BRANCH";
        public const string DDGitTag = "DD_GIT_TAG";
        public const string DDGitRepository = "DD_GIT_REPOSITORY_URL";
        public const string DDGitCommitSha = "DD_GIT_COMMIT_SHA";
        public const string DDGitCommitMessage = "DD_GIT_COMMIT_MESSAGE";
        public const string DDGitCommitAuthorName = "DD_GIT_COMMIT_AUTHOR_NAME";
        public const string DDGitCommitAuthorEmail = "DD_GIT_COMMIT_AUTHOR_EMAIL";
        public const string DDGitCommitAuthorDate = "DD_GIT_COMMIT_AUTHOR_DATE";
        public const string DDGitCommitCommiterName = "DD_GIT_COMMIT_COMMITTER_NAME";
        public const string DDGitCommitCommiterEmail = "DD_GIT_COMMIT_COMMITTER_EMAIL";
        public const string DDGitCommitCommiterDate = "DD_GIT_COMMIT_COMMITTER_DATE";
    }
}
