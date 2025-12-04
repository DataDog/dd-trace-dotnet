// <copyright file="PlatformKeys.Ci.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
namespace Datadog.Trace.Configuration;

internal static partial class PlatformKeys
{
    /// <summary>
    /// Platform keys for CI/CD environment variables from external CI providers
    /// </summary>
    internal static class Ci
    {
        /// <summary>
        /// Common environment variables
        /// </summary>
        public const string Home = "HOME";

        /// <summary>
        /// Common environment variables
        /// </summary>
        public const string UserProfile = "USERPROFILE";

        /// <summary>
        /// Travis CI environment variables
        /// </summary>
        internal static class Travis
        {
            public const string Name = "TRAVIS";
            public const string PullRequestSlug = "TRAVIS_PULL_REQUEST_SLUG";
            public const string RepoSlug = "TRAVIS_REPO_SLUG";
            public const string Commit = "TRAVIS_COMMIT";
            public const string Tag = "TRAVIS_TAG";
            public const string PullRequestBranch = "TRAVIS_PULL_REQUEST_BRANCH";
            public const string Branch = "TRAVIS_BRANCH";
            public const string BuildDir = "TRAVIS_BUILD_DIR";
            public const string BuildId = "TRAVIS_BUILD_ID";
            public const string BuildNumber = "TRAVIS_BUILD_NUMBER";
            public const string BuildWebUrl = "TRAVIS_BUILD_WEB_URL";
            public const string JobWebUrl = "TRAVIS_JOB_WEB_URL";
            public const string CommitMessage = "TRAVIS_COMMIT_MESSAGE";
            public const string PullRequestSha = "TRAVIS_PULL_REQUEST_SHA";
            public const string PullRequestNumber = "TRAVIS_PULL_REQUEST";
        }

        /// <summary>
        /// Circle CI environment variables
        /// </summary>
        internal static class CircleCI
        {
            public const string Name = "CIRCLECI";
            public const string WorkflowId = "CIRCLE_WORKFLOW_ID";
            public const string BuildNum = "CIRCLE_BUILD_NUM";
            public const string RepositoryUrl = "CIRCLE_REPOSITORY_URL";
            public const string Sha = "CIRCLE_SHA1";
            public const string Tag = "CIRCLE_TAG";
            public const string Branch = "CIRCLE_BRANCH";
            public const string WorkingDirectory = "CIRCLE_WORKING_DIRECTORY";
            public const string ProjectRepoName = "CIRCLE_PROJECT_REPONAME";
            public const string Job = "CIRCLE_JOB";
            public const string BuildUrl = "CIRCLE_BUILD_URL";
            public const string PrNumber = "CIRCLE_PR_NUMBER";
        }

        /// <summary>
        /// Jenkins environment variables
        /// </summary>
        internal static class Jenkins
        {
            public const string Url = "JENKINS_URL";
            public const string GitUrl = "GIT_URL";
            public const string GitUrl1 = "GIT_URL_1";
            public const string GitCommit = "GIT_COMMIT";
            public const string GitBranch = "GIT_BRANCH";
            public const string Workspace = "WORKSPACE";
            public const string BuildTag = "BUILD_TAG";
            public const string BuildNumber = "BUILD_NUMBER";
            public const string BuildUrl = "BUILD_URL";
            public const string JobName = "JOB_NAME";
            public const string NodeName = "NODE_NAME";
            public const string NodeLabels = "NODE_LABELS";
            public const string ChangeTarget = "CHANGE_TARGET";
            public const string ChangeId = "CHANGE_ID";
        }

        /// <summary>
        /// GitLab CI environment variables
        /// </summary>
        internal static class GitLab
        {
            public const string Name = "GITLAB_CI";
            public const string ProjectUrl = "CI_PROJECT_URL";
            public const string PipelineId = "CI_PIPELINE_ID";
            public const string JobId = "CI_JOB_ID";
            public const string RepositoryUrl = "CI_REPOSITORY_URL";
            public const string CommitSha = "CI_COMMIT_SHA";
            public const string CommitBranch = "CI_COMMIT_BRANCH";
            public const string CommitTag = "CI_COMMIT_TAG";
            public const string CommitRefName = "CI_COMMIT_REF_NAME";
            public const string ProjectDir = "CI_PROJECT_DIR";
            public const string ProjectPath = "CI_PROJECT_PATH";
            public const string PipelineIId = "CI_PIPELINE_IID";
            public const string PipelineUrl = "CI_PIPELINE_URL";
            public const string JobUrl = "CI_JOB_URL";
            public const string JobName = "CI_JOB_NAME";
            public const string JobStage = "CI_JOB_STAGE";
            public const string CommitMessage = "CI_COMMIT_MESSAGE";
            public const string CommitAuthor = "CI_COMMIT_AUTHOR";
            public const string CommitTimestamp = "CI_COMMIT_TIMESTAMP";
            public const string RunnerId = "CI_RUNNER_ID";
            public const string RunnerTags = "CI_RUNNER_TAGS";
            public const string MergeRequestSourceBranchSha = "CI_MERGE_REQUEST_SOURCE_BRANCH_SHA";
            public const string MergeRequestTargetBranchSha = "CI_MERGE_REQUEST_TARGET_BRANCH_SHA";
            public const string MergeRequestDiffBaseSha = "CI_MERGE_REQUEST_DIFF_BASE_SHA";
            public const string MergeRequestTargetBranchName = "CI_MERGE_REQUEST_TARGET_BRANCH_NAME";
            public const string MergeRequestId = "CI_MERGE_REQUEST_IID";
        }

        /// <summary>
        /// AppVeyor CI environment variables
        /// </summary>
        internal static class AppVeyor
        {
            public const string Name = "APPVEYOR";
            public const string RepoProvider = "APPVEYOR_REPO_PROVIDER";
            public const string RepoName = "APPVEYOR_REPO_NAME";
            public const string RepoCommit = "APPVEYOR_REPO_COMMIT";
            public const string BuildFolder = "APPVEYOR_BUILD_FOLDER";
            public const string BuildId = "APPVEYOR_BUILD_ID";
            public const string BuildNumber = "APPVEYOR_BUILD_NUMBER";
            public const string PullRequestHeadRepoBranch = "APPVEYOR_PULL_REQUEST_HEAD_REPO_BRANCH";
            public const string PullRequestHeadCommit = "APPVEYOR_PULL_REQUEST_HEAD_COMMIT";
            public const string PullRequestNumber = "APPVEYOR_PULL_REQUEST_NUMBER";
            public const string RepoTagName = "APPVEYOR_REPO_TAG_NAME";
            public const string RepoBranch = "APPVEYOR_REPO_BRANCH";
            public const string RepoCommitMessage = "APPVEYOR_REPO_COMMIT_MESSAGE";
            public const string RepoCommitMessageExtended = "APPVEYOR_REPO_COMMIT_MESSAGE_EXTENDED";
            public const string RepoCommitAuthor = "APPVEYOR_REPO_COMMIT_AUTHOR";
            public const string RepoCommitAuthorEmail = "APPVEYOR_REPO_COMMIT_AUTHOR_EMAIL";
            public const string PullRequestBaseRepoBranch = "APPVEYOR_REPO_BRANCH";
        }

        /// <summary>
        /// Azure Pipelines environment variables
        /// </summary>
        internal static class Azure
        {
            public const string TFBuild = "TF_BUILD";
            public const string SystemTeamProjectId = "SYSTEM_TEAMPROJECTID";
            public const string BuildBuildId = "BUILD_BUILDID";
            public const string SystemJobId = "SYSTEM_JOBID";
            public const string BuildSourcesDirectory = "BUILD_SOURCESDIRECTORY";
            public const string BuildDefinitionName = "BUILD_DEFINITIONNAME";
            public const string SystemTeamFoundationServerUri = "SYSTEM_TEAMFOUNDATIONSERVERURI";
            public const string SystemStageDisplayName = "SYSTEM_STAGEDISPLAYNAME";
            public const string SystemJobDisplayName = "SYSTEM_JOBDISPLAYNAME";
            public const string SystemTaskInstanceId = "SYSTEM_TASKINSTANCEID";
            public const string SystemPullRequestSourceRepositoryUri = "SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI";
            public const string BuildRepositoryUri = "BUILD_REPOSITORY_URI";
            public const string SystemPullRequestSourceCommitId = "SYSTEM_PULLREQUEST_SOURCECOMMITID";
            public const string BuildSourceVersion = "BUILD_SOURCEVERSION";
            public const string SystemPullRequestSourceBranch = "SYSTEM_PULLREQUEST_SOURCEBRANCH";
            public const string BuildSourceBranch = "BUILD_SOURCEBRANCH";
            public const string BuildSourceBranchName = "BUILD_SOURCEBRANCHNAME";
            public const string BuildSourceVersionMessage = "BUILD_SOURCEVERSIONMESSAGE";
            public const string BuildRequestedForId = "BUILD_REQUESTEDFORID";
            public const string BuildRequestedForEmail = "BUILD_REQUESTEDFOREMAIL";
            public const string SystemPullRequestTargetBranch = "SYSTEM_PULLREQUEST_TARGETBRANCH";
            public const string SystemPullRequestNumber = "SYSTEM_PULLREQUEST_PULLREQUESTNUMBER";
        }

        /// <summary>
        /// Bitbucket Pipelines environment variables
        /// </summary>
        internal static class Bitbucket
        {
            public const string Commit = "BITBUCKET_COMMIT";
            public const string GitSshOrigin = "BITBUCKET_GIT_SSH_ORIGIN";
            public const string GitHttpsOrigin = "BITBUCKET_GIT_HTTP_ORIGIN";
            public const string Branch = "BITBUCKET_BRANCH";
            public const string Tag = "BITBUCKET_TAG";
            public const string CloneDir = "BITBUCKET_CLONE_DIR";
            public const string PipelineUuid = "BITBUCKET_PIPELINE_UUID";
            public const string BuildNumber = "BITBUCKET_BUILD_NUMBER";
            public const string RepoFullName = "BITBUCKET_REPO_FULL_NAME";
            public const string PullRequestDestinationBranch = "BITBUCKET_PR_DESTINATION_BRANCH";
            public const string PullRequestNumber = "BITBUCKET_PR_ID";
        }

        /// <summary>
        /// GitHub Actions environment variables
        /// </summary>
        internal static class GitHub
        {
            public const string Sha = "GITHUB_SHA";
            public const string ServerUrl = "GITHUB_SERVER_URL";
            public const string Repository = "GITHUB_REPOSITORY";
            public const string RunId = "GITHUB_RUN_ID";
            public const string RunAttempt = "GITHUB_RUN_ATTEMPT";
            public const string HeadRef = "GITHUB_HEAD_REF";
            public const string Ref = "GITHUB_REF";
            public const string Workspace = "GITHUB_WORKSPACE";
            public const string RunNumber = "GITHUB_RUN_NUMBER";
            public const string Workflow = "GITHUB_WORKFLOW";
            public const string Job = "GITHUB_JOB";
            public const string EventPath = "GITHUB_EVENT_PATH";
            public const string BaseRef = "GITHUB_BASE_REF";
        }

        /// <summary>
        /// TeamCity environment variables
        /// </summary>
        internal static class TeamCity
        {
            public const string Version = "TEAMCITY_VERSION";
            public const string BuildConfName = "TEAMCITY_BUILDCONF_NAME";
            public const string BuildUrl = "BUILD_URL";
            public const string PrNumber = "TEAMCITY_PULLREQUEST_NUMBER";
            public const string PrTargetBranch = "TEAMCITY_PULLREQUEST_TARGET_BRANCH";
        }

        /// <summary>
        /// Buildkite environment variables
        /// </summary>
        internal static class Buildkite
        {
            public const string Name = "BUILDKITE";
            public const string BuildId = "BUILDKITE_BUILD_ID";
            public const string JobId = "BUILDKITE_JOB_ID";
            public const string Repo = "BUILDKITE_REPO";
            public const string Commit = "BUILDKITE_COMMIT";
            public const string Branch = "BUILDKITE_BRANCH";
            public const string Tag = "BUILDKITE_TAG";
            public const string BuildCheckoutPath = "BUILDKITE_BUILD_CHECKOUT_PATH";
            public const string BuildNumber = "BUILDKITE_BUILD_NUMBER";
            public const string PipelineSlug = "BUILDKITE_PIPELINE_SLUG";
            public const string BuildUrl = "BUILDKITE_BUILD_URL";
            public const string Message = "BUILDKITE_MESSAGE";
            public const string BuildAuthor = "BUILDKITE_BUILD_AUTHOR";
            public const string BuildAuthorEmail = "BUILDKITE_BUILD_AUTHOR_EMAIL";
            public const string BuildCreator = "BUILDKITE_BUILD_CREATOR";
            public const string BuildCreatorEmail = "BUILDKITE_BUILD_CREATOR_EMAIL";
            public const string AgentId = "BUILDKITE_AGENT_ID";
            public const string AgentMetadata = "BUILDKITE_AGENT_META_DATA_";
            public const string PullRequestBaseBranch = "BUILDKITE_PULL_REQUEST_BASE_BRANCH";
            public const string PullRequestNumber = "BUILDKITE_PULL_REQUEST";
        }

        /// <summary>
        /// Bitrise environment variables
        /// </summary>
        internal static class Bitrise
        {
            public const string BuildSlug = "BITRISE_BUILD_SLUG";
            public const string GitRepositoryUrl = "GIT_REPOSITORY_URL";
            public const string GitCommit = "BITRISE_GIT_COMMIT";
            public const string GitCloneCommitHash = "GIT_CLONE_COMMIT_HASH";
            public const string GitBranchDest = "BITRISEIO_GIT_BRANCH_DEST";
            public const string GitBranch = "BITRISE_GIT_BRANCH";
            public const string GitTag = "BITRISE_GIT_TAG";
            public const string SourceDir = "BITRISE_SOURCE_DIR";
            public const string BuildNumber = "BITRISE_BUILD_NUMBER";
            public const string TriggeredWorkflowId = "BITRISE_TRIGGERED_WORKFLOW_ID";
            public const string BuildUrl = "BITRISE_BUILD_URL";
            public const string GitMessage = "BITRISE_GIT_MESSAGE";
            public const string CloneCommitAuthorName = "GIT_CLONE_COMMIT_AUTHOR_NAME";
            public const string CloneCommitAuthorEmail = "GIT_CLONE_COMMIT_AUTHOR_EMAIL";
            public const string CloneCommitCommiterName = "GIT_CLONE_COMMIT_COMMITER_NAME";
            public const string CloneCommitCommiterEmail = "GIT_CLONE_COMMIT_COMMITER_EMAIL";
            public const string PullRequestHeadBranch = "BITRISEIO_PULL_REQUEST_HEAD_BRANCH";
            public const string PullRequestNumber = "BITRISE_PULL_REQUEST";
        }

        /// <summary>
        /// Buddy CI environment variables
        /// </summary>
        internal static class Buddy
        {
            public const string Name = "BUDDY";
            public const string ScmUrl = "BUDDY_SCM_URL";
            public const string ExecutionRevision = "BUDDY_EXECUTION_REVISION";
            public const string ExecutionBranch = "BUDDY_EXECUTION_BRANCH";
            public const string ExecutionTag = "BUDDY_EXECUTION_TAG";
            public const string PipelineId = "BUDDY_PIPELINE_ID";
            public const string ExecutionId = "BUDDY_EXECUTION_ID";
            public const string PipelineName = "BUDDY_PIPELINE_NAME";
            public const string ExecutionUrl = "BUDDY_EXECUTION_URL";
            public const string ExecutionRevisionMessage = "BUDDY_EXECUTION_REVISION_MESSAGE";
            public const string ExecutionRevisionCommitterName = "BUDDY_EXECUTION_REVISION_COMMITTER_NAME";
            public const string ExecutionRevisionCommitterEmail = "BUDDY_EXECUTION_REVISION_COMMITTER_EMAIL";
            public const string PullRequestBaseBranch = "BUDDY_RUN_PR_BASE_BRANCH";
            public const string PullRequestNumber = "BUDDY_RUN_PR_NO";
        }

        /// <summary>
        /// Codefresh environment variables
        /// </summary>
        internal static class Codefresh
        {
            public const string BuildId = "CF_BUILD_ID";
            public const string PipelineName = "CF_PIPELINE_NAME";
            public const string BuildUrl = "CF_BUILD_URL";
            public const string StepName = "CF_STEP_NAME";
            public const string Branch = "CF_BRANCH";
            public const string PullRequestTarget = "CF_PULL_REQUEST_TARGET";
            public const string PullRequestNumber = "CF_PULL_REQUEST_NUMBER";
        }

        /// <summary>
        /// AWS CodePipeline environment variables
        /// </summary>
        internal static class AwsCodePipeline
        {
            public const string BuildInitiator = "CODEBUILD_INITIATOR";
            public const string BuildArn = "CODEBUILD_BUILD_ARN";
        }

        /// <summary>
        /// Drone CI environment variables
        /// </summary>
        internal static class Drone
        {
            public const string Name = "DRONE";
            public const string Branch = "DRONE_BRANCH";
            public const string BuildLink = "DRONE_BUILD_LINK";
            public const string BuildNumber = "DRONE_BUILD_NUMBER";
            public const string CommitAuthorEmail = "DRONE_COMMIT_AUTHOR_EMAIL";
            public const string CommitAuthorName = "DRONE_COMMIT_AUTHOR_NAME";
            public const string CommitMessage = "DRONE_COMMIT_MESSAGE";
            public const string CommitSha = "DRONE_COMMIT_SHA";
            public const string GitHttpUrl = "DRONE_GIT_HTTP_URL";
            public const string StageName = "DRONE_STAGE_NAME";
            public const string StepName = "DRONE_STEP_NAME";
            public const string Tag = "DRONE_TAG";
            public const string Workspace = "DRONE_WORKSPACE";
            public const string PullRequest = "DRONE_PULL_REQUEST";
            public const string TargetBranch = "DRONE_TARGET_BRANCH";
        }
    }
}
