using System;
using System.Text.RegularExpressions;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.Ci
{
    internal static class CIEnvironmentValues
    {
        private static readonly Regex BranchRegex = new Regex(@"^refs\/heads\/(.*)|refs\/(.*)$", RegexOptions.Compiled);
        private static readonly ILogger Logger = DatadogLogging.GetLogger(typeof(CIEnvironmentValues));

        static CIEnvironmentValues()
        {
            if (EnvironmentHelpers.GetEnvironmentVariable("TRAVIS") != null)
            {
                IsCI = true;
                Provider = "travis";
                Repository ??= EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_REPO_SLUG");
                Commit ??= EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_COMMIT");
                SourceRoot ??= EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_DIR");
                PipelineId = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_ID");
                PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_NUMBER");
                PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BUILD_WEB_URL");
                Branch = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_PULL_REQUEST_BRANCH");
                if (string.IsNullOrWhiteSpace(Branch))
                {
                    Branch = EnvironmentHelpers.GetEnvironmentVariable("TRAVIS_BRANCH");
                }
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("CIRCLECI") != null)
            {
                IsCI = true;
                Provider = "circleci";
                Repository ??= EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_REPOSITORY_URL");
                Commit ??= EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_SHA1");
                SourceRoot ??= EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_WORKING_DIRECTORY");
                PipelineId = null;
                PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_BUILD_NUM");
                PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_BUILD_URL");
                Branch = EnvironmentHelpers.GetEnvironmentVariable("CIRCLE_BRANCH");
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("JENKINS_URL") != null)
            {
                IsCI = true;
                Provider = "jenkins";
                Repository ??= EnvironmentHelpers.GetEnvironmentVariable("GIT_URL");
                Commit ??= EnvironmentHelpers.GetEnvironmentVariable("GIT_COMMIT");
                SourceRoot ??= EnvironmentHelpers.GetEnvironmentVariable("WORKSPACE");
                PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILD_ID");
                PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILD_NUMBER");
                PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("BUILD_URL");
                Branch = EnvironmentHelpers.GetEnvironmentVariable("GIT_BRANCH");
                if (Branch?.IndexOf("origin/", StringComparison.Ordinal) == 0)
                {
                    Branch = Branch.Substring(7);
                }
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("GITLAB_CI") != null)
            {
                IsCI = true;
                Provider = "gitlab";
                Repository ??= EnvironmentHelpers.GetEnvironmentVariable("CI_REPOSITORY_URL");
                Commit ??= EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_SHA");
                SourceRoot ??= EnvironmentHelpers.GetEnvironmentVariable("CI_PROJECT_DIR");
                PipelineId = EnvironmentHelpers.GetEnvironmentVariable("CI_JOB_ID");
                PipelineNumber = null;
                PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("CI_JOB_URL");
                Branch = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_BRANCH");
                if (string.IsNullOrWhiteSpace(Branch))
                {
                    Branch = EnvironmentHelpers.GetEnvironmentVariable("CI_COMMIT_REF_NAME");
                }
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR") != null)
            {
                IsCI = true;
                Provider = "appveyor";
                Repository ??= EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_NAME");
                Commit ??= EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_COMMIT");
                SourceRoot ??= EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_FOLDER");
                PipelineId = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_ID");
                PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_NUMBER");
                PipelineUrl = string.Format("https://ci.appveyor.com/project/{0}/builds/{1}", EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_PROJECT_SLUG"), EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_BUILD_ID"));
                Branch = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_PULL_REQUEST_HEAD_REPO_BRANCH");
                if (string.IsNullOrWhiteSpace(Branch))
                {
                    Branch = EnvironmentHelpers.GetEnvironmentVariable("APPVEYOR_REPO_BRANCH");
                }
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("TF_BUILD") != null)
            {
                IsCI = true;
                Provider = "azurepipelines";
                Repository ??= EnvironmentHelpers.GetEnvironmentVariable("BUILD_REPOSITORY_URI");
                Commit ??= EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCEVERSION");
                SourceRoot ??= EnvironmentHelpers.GetEnvironmentVariable("BUILD_SOURCESDIRECTORY");
                PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDID");
                PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDNUMBER");
                PipelineUrl = string.Format("{0}/{1}/_build/results?buildId={2}&_a=summary", EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI"), EnvironmentHelpers.GetEnvironmentVariable("SYSTEM_TEAMPROJECT"), EnvironmentHelpers.GetEnvironmentVariable("BUILD_BUILDID"));
                Branch = EnvironmentHelpers.GetEnvironmentVariable("Build.SourceBranchName");
                if (string.IsNullOrWhiteSpace(Branch))
                {
                    Branch = EnvironmentHelpers.GetEnvironmentVariable("Build.SourceBranch");
                }
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_COMMIT") != null)
            {
                IsCI = true;
                Provider = "bitbucketpipelines";
                Repository ??= EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_GIT_SSH_ORIGIN");
                Commit ??= EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_COMMIT");
                SourceRoot ??= EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_CLONE_DIR");
                PipelineId = null;
                PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BITBUCKET_BUILD_NUMBER");
                PipelineUrl = null;
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("GITHUB_SHA") != null)
            {
                IsCI = true;
                Provider = "github";
                Repository ??= EnvironmentHelpers.GetEnvironmentVariable("GITHUB_REPOSITORY");
                Commit ??= EnvironmentHelpers.GetEnvironmentVariable("GITHUB_SHA");
                SourceRoot ??= EnvironmentHelpers.GetEnvironmentVariable("GITHUB_WORKSPACE");
                PipelineId = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_RUN_ID");
                PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_RUN_NUMBER");
                PipelineUrl = $"{Repository}/commit/{Commit}/checks";
                Branch = EnvironmentHelpers.GetEnvironmentVariable("GITHUB_REF");
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable("TEAMCITY_VERSION") != null)
            {
                IsCI = true;
                Provider = "teamcity";
                Repository ??= EnvironmentHelpers.GetEnvironmentVariable("BUILD_VCS_URL");
                Commit ??= EnvironmentHelpers.GetEnvironmentVariable("BUILD_VCS_NUMBER");
                SourceRoot ??= EnvironmentHelpers.GetEnvironmentVariable("BUILD_CHECKOUTDIR");
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
            else if (EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE") != null)
            {
                IsCI = true;
                Provider = "buildkite";
                Repository ??= EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_REPO");
                Commit ??= EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_COMMIT");
                SourceRoot ??= EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_CHECKOUT_PATH");
                PipelineId = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_ID");
                PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_NUMBER");
                PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BUILD_URL");
                Branch = EnvironmentHelpers.GetEnvironmentVariable("BUILDKITE_BRANCH");
            }

            try
            {
                // Clean branch name
                if (!string.IsNullOrEmpty(Branch))
                {
                    var match = BranchRegex.Match(Branch);
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

        public static bool IsCI { get; }

        public static string Provider { get; }

        public static string Repository { get; }

        public static string Commit { get; }

        public static string Branch { get; }

        public static string SourceRoot { get; }

        public static string PipelineId { get; }

        public static string PipelineNumber { get; }

        public static string PipelineUrl { get; }

        public static void DecorateSpan(Span span)
        {
            if (span == null || !IsCI)
            {
                return;
            }

            span.SetTag(TestTags.CIProvider, Provider);
            span.SetTag(TestTags.CIPipelineId, PipelineId);
            span.SetTag(TestTags.CIPipelineNumber, PipelineNumber);
            span.SetTag(TestTags.CIPipelineUrl, PipelineUrl);

            span.SetTag(TestTags.GitRepository, Repository);
            span.SetTag(TestTags.GitCommit, Commit);
            span.SetTag(TestTags.GitBranch, Branch);

            span.SetTag(TestTags.BuildSourceRoot, SourceRoot);
        }
    }
}
