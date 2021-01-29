using System;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Ci
{
    internal static class CIEnvironmentValues
    {
        private static readonly ILogger Logger = DatadogLogging.GetLogger(typeof(CIEnvironmentValues));

        static CIEnvironmentValues()
        {
            // **********
            // Setup variables
            // **********

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

            var regex = new Regex(@"^refs\/heads\/(.*)|refs\/tags\/(.*)|refs\/(.*)$");

            try
            {
                // Clean tag name
                if (!string.IsNullOrEmpty(Tag))
                {
                    var match = regex.Match(Tag);
                    if (match.Success && match.Groups.Count == 4)
                    {
                        Tag = !string.IsNullOrWhiteSpace(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[2].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error fixing tag name: {0}", Tag);
            }

            try
            {
                // Clean branch name
                if (!string.IsNullOrEmpty(Branch))
                {
                    var match = regex.Match(Branch);
                    if (match.Success && match.Groups.Count == 4)
                    {
                        Branch = !string.IsNullOrWhiteSpace(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[3].Value;
                        if (string.IsNullOrEmpty(Tag))
                        {
                            Tag = match.Groups[2].Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error fixing branch name: {BranchName}", Branch);
            }
        }

        public static bool IsCI { get; private set; }

        public static string Provider { get; private set; }

        public static string Repository { get; private set; }

        public static string Commit { get; private set; }

        public static string Branch { get; private set; }

        public static string Tag { get; private set; }

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
            if (span == null || !IsCI)
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
            span.SetTagIfNotNullOrEmpty(CommonTags.BuildSourceRoot, SourceRoot);
        }

        private static void SetupTravisEnvironment()
        {
            IsCI = true;
            Provider = "travis";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_REPO_SLUG");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_COMMIT");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_DIR");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_DIR");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_NUMBER");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_WEB_URL");
            JobUrl = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_JOB_WEB_URL");
            Branch = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_PULL_REQUEST_BRANCH");
            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BRANCH");
            }
        }

        private static void SetupCircleCiEnvironment()
        {
            IsCI = true;
            Provider = "circleci";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_REPOSITORY_URL");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_SHA1");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_WORKING_DIRECTORY");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_WORKING_DIRECTORY");
            PipelineId = null;
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_BUILD_NUM");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_BUILD_URL");
            Branch = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_BRANCH");
        }

        private static void SetupJenkinsEnvironment()
        {
            IsCI = true;
            Provider = "jenkins";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("GIT_URL");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("GIT_COMMIT");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("WORKSPACE");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("WORKSPACE");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILD_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILD_NUMBER");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("BUILD_URL");
            Branch = EnvironmentHelpers.GetEnvironmentVariable("GIT_BRANCH");
            if (Branch?.IndexOf("origin/", StringComparison.Ordinal) == 0)
            {
                Branch = Branch.Substring(7);
            }
        }

        private static void SetupGitlabEnvironment()
        {
            IsCI = true;
            Provider = "gitlab";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("CI_REPOSITORY_URL");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_SHA");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("CI_PROJECT_DIR");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("CI_PROJECT_DIR");

            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("CI_PIPELINE_ID");
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable("CI_PROJECT_PATH");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("CI_PIPELINE_IID");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("CI_PIPELINE_URL");

            JobUrl = EnvironmentHelpers.GetEnvironmentVariable("CI_JOB_URL");
            JobName = EnvironmentHelpers.GetEnvironmentVariable("CI_JOB_NAME");
            StageName = EnvironmentHelpers.GetEnvironmentVariable("CI_JOB_STAGE");
            Branch = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_BRANCH");
            Tag = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_TAG");
            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_REF_NAME");
            }

            // Clean pipeline url
            PipelineUrl = PipelineUrl.Replace("/-/pipelines/", "/pipelines/");
        }

        private static void SetupAppveyorEnvironment()
        {
            IsCI = true;
            Provider = "appveyor";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_NAME");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_NUMBER");
            PipelineUrl = string.Format("https://ci.appveyor.com/project/{0}/builds/{1}", EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_PROJECT_SLUG"), EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_ID"));
            Branch = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_PULL_REQUEST_HEAD_REPO_BRANCH");
            Tag = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_TAG_NAME");
            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_BRANCH");
            }
        }

        private static void SetupAzurePipelinesEnvironment()
        {
            IsCI = true;
            Provider = "azurepipelines";
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCESDIRECTORY");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCESDIRECTORY");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDID");
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable("BUILD_DEFINITIONNAME");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDNUMBER");
            PipelineUrl = string.Format(
                "{0}{1}/_build/results?buildId={2}&_a=summary",
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI"),
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMPROJECT"),
                EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDID"));
            JobUrl = string.Format(
                "{0}{1}/_build/results?buildId={2}&view=logs&j={3}&t={4}",
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI"),
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMPROJECT"),
                EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDID"),
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_JOBID"),
                EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TASKINSTANCEID"));
            Repository = EnvironmentHelpers.GetEnvironmentVariable("BUILD_REPOSITORY_URI");

            string prCommit = EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_PULLREQUEST_SOURCECOMMITID");
            Commit = !string.IsNullOrWhiteSpace(prCommit) ? prCommit : EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCEVERSION");

            string prBranch = EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_PULLREQUEST_SOURCEBRANCH");
            Branch = !string.IsNullOrWhiteSpace(prBranch) ? prBranch : EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCEBRANCH");

            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCEBRANCHNAME");
            }
        }

        private static void SetupBitbucketEnvironment()
        {
            IsCI = true;
            Provider = "bitbucketpipelines";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_GIT_SSH_ORIGIN");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_COMMIT");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_CLONE_DIR");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_CLONE_DIR");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_PIPELINE_UUID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_BUILD_NUMBER");
            PipelineUrl = null;
        }

        private static void SetupGithubActionsEnvironment()
        {
            IsCI = true;
            Provider = "github";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_REPOSITORY");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_SHA");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_WORKSPACE");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_WORKSPACE");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_RUN_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
            PipelineUrl = $"{Repository}/commit/{Commit}/checks";
            Branch = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_REF");
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
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_CHECKOUT_PATH");
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_CHECKOUT_PATH");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_NUMBER");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_URL");
            Branch = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BRANCH");
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ExpandPath(string path)
        {
            if (path is null)
            {
                return null;
            }

            if (path.IndexOf('~') != -1)
            {
                string homePath = (Environment.OSVersion.Platform == PlatformID.Unix ||
                                   Environment.OSVersion.Platform == PlatformID.MacOSX)
                    ? Environment.GetEnvironmentVariable("HOME")
                    : Environment.ExpandEnvironmentVariables("%HOMEDRIVE%%HOMEPATH%");
                path = path.Replace("~", homePath);
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
    }
}
