using System;
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

            try
            {
                // Clean branch name
                if (!string.IsNullOrEmpty(Branch))
                {
                    var regex = new Regex(@"^refs\/heads\/(.*)|refs\/(.*)$", RegexOptions.Compiled);
                    var match = regex.Match(Branch);
                    if (match.Success && match.Groups.Count == 3)
                    {
                        Branch = !string.IsNullOrWhiteSpace(match.Groups[1].Value) ? match.Groups[1].Value : match.Groups[2].Value;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error fixing branch name: {0}", Branch);
            }
        }

        public static bool IsCI { get; private set; }

        public static string Provider { get; private set; }

        public static string Repository { get; private set; }

        public static string Commit { get; private set; }

        public static string Branch { get; private set; }

        public static string SourceRoot { get; private set; }

        public static string PipelineId { get; private set; }

        public static string PipelineNumber { get; private set; }

        public static string PipelineUrl { get; private set; }

        public static string JobUrl { get; private set; }

        public static void DecorateSpan(Span span)
        {
            if (span == null || !IsCI)
            {
                return;
            }

            span.SetTag(CommonTags.CIProvider, Provider);
            span.SetTag(CommonTags.CIPipelineId, PipelineId);
            span.SetTag(CommonTags.CIPipelineNumber, PipelineNumber);
            span.SetTag(CommonTags.CIPipelineUrl, PipelineUrl);
            span.SetTag(CommonTags.CIJobUrl, JobUrl);

            span.SetTag(CommonTags.GitRepository, Repository);
            span.SetTag(CommonTags.GitCommit, Commit);
            span.SetTag(CommonTags.GitBranch, Branch);

            span.SetTag(CommonTags.BuildSourceRoot, SourceRoot);
        }

        private static void SetupTravisEnvironment()
        {
            IsCI = true;
            Provider = "travis";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_REPO_SLUG");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_COMMIT");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_DIR");
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
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILD_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILD_NUMBER");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("BUILD_URL");
            JobUrl = EnvironmentHelpers.GetEnvironmentVariable("JOB_URL");
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
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("CI_PIPELINE_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("CI_PIPELINE_IID");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("CI_PIPELINE_URL");
            JobUrl = EnvironmentHelpers.GetEnvironmentVariable("CI_JOB_URL");
            Branch = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_BRANCH");
            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_REF_NAME");
            }
        }

        private static void SetupAppveyorEnvironment()
        {
            IsCI = true;
            Provider = "appveyor";
            Repository = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_NAME");
            Commit = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT");
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER");
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_NUMBER");
            PipelineUrl = string.Format("https://ci.appveyor.com/project/{0}/builds/{1}", EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_PROJECT_SLUG"), EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_ID"));
            Branch = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_PULL_REQUEST_HEAD_REPO_BRANCH");
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
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDNUMBER");
            PipelineUrl = string.Format("{0}{1}/_build/results?buildId={2}&_a=summary", EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI"), EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMPROJECT"), EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDID"));
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
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_ID");
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_NUMBER");
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_URL");
            Branch = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BRANCH");
        }
    }
}
