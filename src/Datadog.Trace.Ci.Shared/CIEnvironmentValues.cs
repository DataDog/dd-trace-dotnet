// <copyright file="CIEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci
{
    internal static class CIEnvironmentValues
    {
        private static readonly IDatadogLogger Logger = DatadogLogging.GetLoggerFor(typeof(CIEnvironmentValues));

        static CIEnvironmentValues()
        {
            ReloadEnvironmentData();
        }

        public static bool IsCI { get; private set; }

        public static string Provider { get; private set; }

        public static string Repository { get; private set; }

        public static string Commit { get; private set; }

        public static string Branch { get; private set; }

        public static string Tag { get; private set; }

        public static string AuthorName { get; private set; }

        public static string AuthorEmail { get; private set; }

        public static DateTimeOffset? AuthorDate { get; private set; }

        public static string CommitterName { get; private set; }

        public static string CommitterEmail { get; private set; }

        public static DateTimeOffset? CommitterDate { get; private set; }

        public static string Message { get; private set; }

        public static string SourceRoot { get; private set; }

        public static string PipelineId { get; private set; }

        public static string PipelineName { get; private set; }

        public static string PipelineNumber { get; private set; }

        public static string PipelineUrl { get; private set; }

        public static string JobUrl { get; private set; }

        public static string JobName { get; private set; }

        public static string StageName { get; private set; }

        public static string WorkspacePath { get; private set; }

        public static void DecorateSpan(Span span)
        {
            if (span == null)
            {
                return;
            }

            span.SetTagIfNotNullOrEmpty(CommonTags.CIProvider, Provider);
            span.SetTagIfNotNullOrEmpty(CommonTags.CIPipelineId, PipelineId);
            span.SetTagIfNotNullOrEmpty(CommonTags.CIPipelineName, PipelineName);
            span.SetTagIfNotNullOrEmpty(CommonTags.CIPipelineNumber, PipelineNumber);
            span.SetTagIfNotNullOrEmpty(CommonTags.CIPipelineUrl, PipelineUrl);
            span.SetTagIfNotNullOrEmpty(CommonTags.CIJobUrl, JobUrl);
            span.SetTagIfNotNullOrEmpty(CommonTags.CIJobName, JobName);
            span.SetTagIfNotNullOrEmpty(CommonTags.StageName, StageName);
            span.SetTagIfNotNullOrEmpty(CommonTags.CIWorkspacePath, WorkspacePath);
            span.SetTagIfNotNullOrEmpty(CommonTags.GitRepository, Repository);
            span.SetTagIfNotNullOrEmpty(CommonTags.GitCommit, Commit);
            span.SetTagIfNotNullOrEmpty(CommonTags.GitBranch, Branch);
            span.SetTagIfNotNullOrEmpty(CommonTags.GitTag, Tag);
            span.SetTagIfNotNullOrEmpty(CommonTags.GitCommitAuthorName, AuthorName);
            span.SetTagIfNotNullOrEmpty(CommonTags.GitCommitAuthorEmail, AuthorEmail);
            span.SetTagIfNotNullOrEmpty(CommonTags.GitCommitAuthorDate, AuthorDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
            span.SetTagIfNotNullOrEmpty(CommonTags.GitCommitCommitterName, CommitterName);
            span.SetTagIfNotNullOrEmpty(CommonTags.GitCommitCommitterEmail, CommitterEmail);
            span.SetTagIfNotNullOrEmpty(CommonTags.GitCommitCommitterDate, CommitterDate?.ToString("yyyy-MM-dd'T'HH:mm:ss.fffK", CultureInfo.InvariantCulture));
            span.SetTagIfNotNullOrEmpty(CommonTags.GitCommitMessage, Message);
            span.SetTagIfNotNullOrEmpty(CommonTags.BuildSourceRoot, SourceRoot);
        }

        internal static void ReloadEnvironmentData()
        {
            // **********
            // Setup variables
            // **********

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

            GitInfo gitInfo = GitInfo.GetCurrent();

            if (EnvironmentHelpers.GetEnvironmentVariable("TRAVIS") != null)
            {
                SetupTravisEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("CIRCLECI") != null)
            {
                SetupCircleCiEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("JENKINS_URL") != null)
            {
                SetupJenkinsEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("GITLAB_CI") != null)
            {
                SetupGitlabEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR") != null)
            {
                SetupAppveyorEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("TF_BUILD") != null)
            {
                SetupAzurePipelinesEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_COMMIT") != null)
            {
                SetupBitbucketEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("GITHUB_SHA") != null)
            {
                SetupGithubActionsEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("TEAMCITY_VERSION") != null)
            {
                SetupTeamcityEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE") != null)
            {
                SetupBuildkiteEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("BITRISE_BUILD_SLUG") != null)
            {
                SetupBitriseEnvironment();
            }
            else
            {
                Branch = gitInfo.Branch;
                Commit = gitInfo.Commit;
                Repository = gitInfo.Repository;
                SourceRoot = gitInfo.SourceRoot;
                WorkspacePath = gitInfo.SourceRoot;
            }

            // **********
            // Add Author, Committer and Message from git
            // **********
            // Merge commits have a different commit hash from the one reported by the CI.
            if (gitInfo.Commit == Commit)
            {
                if (string.IsNullOrEmpty(AuthorName))
                {
                    AuthorName = gitInfo.AuthorName;
                }

                if (string.IsNullOrEmpty(AuthorEmail))
                {
                    AuthorEmail = gitInfo.AuthorEmail;
                }

                if (AuthorDate is null)
                {
                    AuthorDate = gitInfo.AuthorDate;
                }

                if (string.IsNullOrEmpty(CommitterName))
                {
                    CommitterName = gitInfo.CommitterName;
                }

                if (string.IsNullOrEmpty(CommitterEmail))
                {
                    CommitterEmail = gitInfo.CommitterEmail;
                }

                if (CommitterDate is null)
                {
                    CommitterDate = gitInfo.CommitterDate;
                }

                if (string.IsNullOrEmpty(Message))
                {
                    Message = gitInfo.Message;
                }
            }

            // **********
            // Remove sensitive info from repository url
            // **********
            if (Uri.TryCreate(Repository, UriKind.Absolute, out Uri repository))
            {
                if (!string.IsNullOrEmpty(repository.UserInfo))
                {
                    Repository = repository.GetComponents(UriComponents.Fragment | UriComponents.Query | UriComponents.Path | UriComponents.Port | UriComponents.Host | UriComponents.Scheme, UriFormat.SafeUnescaped);
                }
            }

            // **********
            // Expand ~ in Paths
            // **********

            SourceRoot = ExpandPath(SourceRoot);
            WorkspacePath = ExpandPath(WorkspacePath);

            // **********
            // Clean Refs
            // **********

            CleanBranchAndTag();
        }

        private static void SetupTravisEnvironment()
        {
            IsCI = true;
            Provider = "travisci";

            string prSlug = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_PULL_REQUEST_SLUG");
            string repoSlug = !string.IsNullOrEmpty(prSlug) ? prSlug : EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_REPO_SLUG");

            Repository = $"https://github.com/{repoSlug}.git";
            Commit = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_COMMIT");
            Tag = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_TAG");
            if (string.IsNullOrEmpty(Tag))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_PULL_REQUEST_BRANCH");
                if (string.IsNullOrWhiteSpace(Branch))
                {
                    Branch = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BRANCH");
                }
            }

            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_DIR");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_DIR");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_NUMBER");
            PipelineName = repoSlug;
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_WEB_URL");
            JobUrl = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_JOB_WEB_URL");

            Message = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_COMMIT_MESSAGE");
        }

        private static void SetupCircleCiEnvironment()
        {
            IsCI = true;
            Provider = "circleci";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_REPOSITORY_URL");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_SHA1");
            Tag = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_TAG");
            if (string.IsNullOrEmpty(Tag))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_BRANCH");
            }

            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_WORKING_DIRECTORY");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_WORKING_DIRECTORY");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_WORKFLOW_ID");
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_PROJECT_REPONAME");
            PipelineUrl = $"https://app.circleci.com/pipelines/workflows/{PipelineId}";
            JobName = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_JOB");
            JobUrl = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_BUILD_URL");
        }

        private static void SetupJenkinsEnvironment()
        {
            IsCI = true;
            Provider = "jenkins";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("GIT_URL");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("GIT_COMMIT");

            string gitBranch = EnvironmentHelpers.GetEnvironmentVariable("GIT_BRANCH");
            if (gitBranch?.Contains("tags") == true)
            {
                Tag = gitBranch;
            }
            else
            {
                Branch = gitBranch;
            }

            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("WORKSPACE");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("WORKSPACE");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILD_TAG");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILD_NUMBER");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("BUILD_URL");

            // Pipeline Name algorithm from: https://github.com/DataDog/dd-trace-java/blob/master/internal-api/src/main/java/datadog/trace/bootstrap/instrumentation/api/ci/JenkinsInfo.java
            string pipelineName = EnvironmentHelpers.GetEnvironmentVariable("JOB_NAME");
            if (pipelineName != null)
            {
                CleanBranchAndTag();

                // First, the git branch is removed from the raw jobName
                string jobNameNoBranch = Branch != null ? pipelineName.Trim().Replace("/" + Branch, string.Empty) : pipelineName;

                // Once the branch has been removed, we try to extract
                // the configurations from the job name.
                // The configurations have the form like "key1=value1,key2=value2"
                Dictionary<string, string> configurations = new Dictionary<string, string>();
                string[] jobNameParts = jobNameNoBranch.Split('/');
                if (jobNameParts.Length > 1 && jobNameParts[1].Contains("="))
                {
                    string configsStr = jobNameParts[1].ToLowerInvariant().Trim();
                    string[] configsKeyValue = configsStr.Split(',');
                    foreach (string configKeyValue in configsKeyValue)
                    {
                        string[] keyValue = configKeyValue.Trim().Split('=');
                        configurations[keyValue[0]] = keyValue[1];
                    }
                }

                if (configurations.Count == 0)
                {
                    // If there is no configurations,
                    // the jobName is the original one without branch.
                    PipelineName = jobNameNoBranch;
                }
                else
                {
                    // If there are configurations,
                    // the jobName is the first part of the splited raw jobName.
                    PipelineName = jobNameParts[0];
                }
            }
        }

        private static void SetupGitlabEnvironment()
        {
            IsCI = true;
            Provider = "gitlab";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("CI_REPOSITORY_URL");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_SHA");
            Branch = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_BRANCH");
            Tag = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_TAG");
            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_REF_NAME");
            }

            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("CI_PROJECT_DIR");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("CI_PROJECT_DIR");

            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("CI_PIPELINE_ID");
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable("CI_PROJECT_PATH");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("CI_PIPELINE_IID");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("CI_PIPELINE_URL");

            JobUrl = EnvironmentHelpers.GetEnvironmentVariable("CI_JOB_URL");
            JobName = EnvironmentHelpers.GetEnvironmentVariable("CI_JOB_NAME");
            StageName = EnvironmentHelpers.GetEnvironmentVariable("CI_JOB_STAGE");

            Message = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_MESSAGE");

            // Clean pipeline url
            PipelineUrl = PipelineUrl?.Replace("/-/pipelines/", "/pipelines/");
        }

        private static void SetupAppveyorEnvironment()
        {
            IsCI = true;
            Provider = "appveyor";
            string repoProvider = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_PROVIDER");
            if (repoProvider == "github")
            {
                Repository = string.Format("https://github.com/{0}.git", EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_NAME"));
            }
            else
            {
                Repository = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_NAME");
            }

            Commit = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_ID");
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_NAME");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_NUMBER");
            PipelineUrl = string.Format("https://ci.appveyor.com/project/{0}/builds/{1}", EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_NAME"), EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_ID"));
            JobUrl = PipelineUrl;
            Branch = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_PULL_REQUEST_HEAD_REPO_BRANCH");
            Tag = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_TAG_NAME");
            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_BRANCH");
            }

            Message = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT_MESSAGE_EXTENDED");
            AuthorName = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT_AUTHOR");
            AuthorEmail = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT_AUTHOR_EMAIL");
        }

        private static void SetupAzurePipelinesEnvironment()
        {
            IsCI = true;
            Provider = "azurepipelines";
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCESDIRECTORY");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCESDIRECTORY");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDID");
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable("BUILD_DEFINITIONNAME");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDID");
            PipelineUrl = string.Format(
                "{0}{1}/_build/results?buildId={2}",
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONSERVERURI"),
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMPROJECTID"),
                EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDID"));
            JobUrl = string.Format(
                "{0}{1}/_build/results?buildId={2}&view=logs&j={3}&t={4}",
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONSERVERURI"),
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMPROJECTID"),
                EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDID"),
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_JOBID"),
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TASKINSTANCEID"));

            string prRepo = EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_PULLREQUEST_SOURCEREPOSITORYURI");
            Repository = !string.IsNullOrWhiteSpace(prRepo) ? prRepo : EnvironmentHelpers.GetEnvironmentVariable("BUILD_REPOSITORY_URI");

            string prCommit = EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_PULLREQUEST_SOURCECOMMITID");
            Commit = !string.IsNullOrWhiteSpace(prCommit) ? prCommit : EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCEVERSION");

            string prBranch = EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_PULLREQUEST_SOURCEBRANCH");
            Branch = !string.IsNullOrWhiteSpace(prBranch) ? prBranch : EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCEBRANCH");

            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCEBRANCHNAME");
            }

            Message = EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCEVERSIONMESSAGE");
            AuthorName = EnvironmentHelpers.GetEnvironmentVariable("BUILD_REQUESTEDFORID");
            AuthorEmail = EnvironmentHelpers.GetEnvironmentVariable("BUILD_REQUESTEDFOREMAIL");
        }

        private static void SetupBitbucketEnvironment()
        {
            IsCI = true;
            Provider = "bitbucket";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_GIT_SSH_ORIGIN");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_COMMIT");
            Branch = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_BRANCH");
            Tag = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_TAG");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_CLONE_DIR");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_CLONE_DIR");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_PIPELINE_UUID")?.Replace("}", string.Empty).Replace("{", string.Empty);
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_BUILD_NUMBER");
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_REPO_FULL_NAME");
            PipelineUrl = string.Format(
                "https://bitbucket.org/{0}/addon/pipelines/home#!/results/{1}",
                EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_REPO_FULL_NAME"),
                EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_BUILD_NUMBER"));
            JobUrl = PipelineUrl;
        }

        private static void SetupGithubActionsEnvironment()
        {
            IsCI = true;
            Provider = "github";
            Repository = string.Format("https://github.com/{0}.git", EnvironmentHelpers.GetEnvironmentVariable("GITHUB_REPOSITORY"));
            Commit = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_SHA");

            string headRef = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_HEAD_REF");
            string ghRef = !string.IsNullOrEmpty(headRef) ? headRef : EnvironmentHelpers.GetEnvironmentVariable("GITHUB_REF");
            if (ghRef.Contains("tags"))
            {
                Tag = ghRef;
            }
            else
            {
                Branch = ghRef;
            }

            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_WORKSPACE");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_WORKSPACE");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_RUN_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_WORKFLOW");
            PipelineUrl = $"https://github.com/{EnvironmentHelpers.GetEnvironmentVariable("GITHUB_REPOSITORY")}/commit/{Commit}/checks";
            JobUrl = PipelineUrl;
        }

        private static void SetupTeamcityEnvironment()
        {
            IsCI = true;
            Provider = "teamcity";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("BUILD_VCS_URL");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("BUILD_VCS_URL");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("BUILD_VCS_NUMBER");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("BUILD_CHECKOUTDIR");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILD_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILD_NUMBER");
            string serverUrl = EnvironmentHelpers.GetEnvironmentVariable("SERVER_URL");
            if (PipelineId != null && serverUrl != null)
            {
                PipelineUrl = $"{serverUrl}/viewLog.html?buildId={PipelineId}";
            }
            else
            {
                PipelineUrl = null;
            }
        }

        private static void SetupBuildkiteEnvironment()
        {
            IsCI = true;
            Provider = "buildkite";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_REPO");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_COMMIT");
            Branch = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BRANCH");
            Tag = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_TAG");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_CHECKOUT_PATH");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_CHECKOUT_PATH");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_NUMBER");
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_PIPELINE_SLUG");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_URL");
            JobUrl = string.Format("{0}#{1}", EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_URL"), EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_JOB_ID"));

            Message = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_MESSAGE");
            AuthorName = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_AUTHOR");
            AuthorEmail = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_AUTHOR_EMAIL");
        }

        private static void SetupBitriseEnvironment()
        {
            IsCI = true;
            Provider = "bitrise";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("GIT_REPOSITORY_URL");

            string prCommit = EnvironmentHelpers.GetEnvironmentVariable("BITRISE_GIT_COMMIT");
            Commit = !string.IsNullOrWhiteSpace(prCommit) ? prCommit : EnvironmentHelpers.GetEnvironmentVariable("GIT_CLONE_COMMIT_HASH");

            string prBranch = EnvironmentHelpers.GetEnvironmentVariable("BITRISEIO_GIT_BRANCH_DEST");
            Branch = !string.IsNullOrWhiteSpace(prBranch) ? prBranch : EnvironmentHelpers.GetEnvironmentVariable("BITRISE_GIT_BRANCH");

            Tag = EnvironmentHelpers.GetEnvironmentVariable("BITRISE_GIT_TAG");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("BITRISE_SOURCE_DIR");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("BITRISE_SOURCE_DIR");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BITRISE_BUILD_SLUG");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BITRISE_BUILD_NUMBER");
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable("BITRISE_TRIGGERED_WORKFLOW_ID");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("BITRISE_BUILD_URL");

            Message = EnvironmentHelpers.GetEnvironmentVariable("BITRISE_GIT_MESSAGE");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ExpandPath(string path)
        {
            if (path == "~" || path?.StartsWith("~/") == true)
            {
                string homePath = (Environment.OSVersion.Platform == PlatformID.Unix ||
                                   Environment.OSVersion.Platform == PlatformID.MacOSX)
                    ? Environment.GetEnvironmentVariable("HOME")
                    : Environment.GetEnvironmentVariable("USERPROFILE");
                path = homePath + path.Substring(1);
            }

            return path;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetTagIfNotNullOrEmpty(this Span span, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                span.SetTag(key, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void CleanBranchAndTag()
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
                Logger.Warning(ex, "Error fixing tag name: {TagName}", Tag);
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
                Logger.Warning(ex, "Error fixing branch name: {BranchName}", Branch);
            }
        }
    }
}
