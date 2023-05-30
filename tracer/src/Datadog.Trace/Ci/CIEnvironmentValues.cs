// <copyright file="CIEnvironmentValues.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Datadog.Trace.Ci.Tags;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;

namespace Datadog.Trace.Ci
{
    internal sealed class CIEnvironmentValues
    {
        internal const string RepositoryUrlPattern = @"((http|git|ssh|http(s)|file|\/?)|(git@[\w\.\-]+))(:(\/\/)?)([\w\.@\:/\-~]+)(\.git)(\/)?";
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(CIEnvironmentValues));

        private static readonly Lazy<CIEnvironmentValues> _instance = new(() => new CIEnvironmentValues());

        private CIEnvironmentValues()
        {
            ReloadEnvironmentData();
        }

        public static CIEnvironmentValues Instance => _instance.Value;

        public bool IsCI { get; private set; }

        public string Provider { get; private set; }

        public string Repository { get; private set; }

        public string Commit { get; private set; }

        public string Branch { get; private set; }

        public string Tag { get; private set; }

        public string AuthorName { get; private set; }

        public string AuthorEmail { get; private set; }

        public DateTimeOffset? AuthorDate { get; private set; }

        public string CommitterName { get; private set; }

        public string CommitterEmail { get; private set; }

        public DateTimeOffset? CommitterDate { get; private set; }

        public string Message { get; private set; }

        public string SourceRoot { get; private set; }

        public string PipelineId { get; private set; }

        public string PipelineName { get; private set; }

        public string PipelineNumber { get; private set; }

        public string PipelineUrl { get; private set; }

        public string JobUrl { get; private set; }

        public string JobName { get; private set; }

        public string StageName { get; private set; }

        public string WorkspacePath { get; private set; }

        public string NodeName { get; private set; }

        public string[] NodeLabels { get; private set; }

        public CodeOwners CodeOwners { get; private set; }

        public Dictionary<string, string> VariablesToBypass { get; private set; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SetTagIfNotNullOrEmpty(Span span, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                span.SetTag(key, value);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string ExpandPath(string path)
        {
            if (path == "~" || path?.StartsWith("~/") == true)
            {
                var homePath = (Environment.OSVersion.Platform == PlatformID.Unix ||
                                Environment.OSVersion.Platform == PlatformID.MacOSX)
                    ? Environment.GetEnvironmentVariable(Constants.Home)
                    : Environment.GetEnvironmentVariable(Constants.UserProfile);
                path = homePath + path.Substring(1);
            }

            return path;
        }

        private static string GetEnvironmentVariableIfIsNotEmpty(string key, string defaultValue, Func<string, string, bool> validator = null)
        {
            var value = EnvironmentHelpers.GetEnvironmentVariable(key, defaultValue);
            if (validator is not null)
            {
                if (!validator.Invoke(value, defaultValue))
                {
                    return defaultValue;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(value))
                {
                    return defaultValue;
                }
            }

            return value;
        }

        private static DateTimeOffset? GetDateTimeOffsetEnvironmentVariableIfIsNotEmpty(string key, DateTimeOffset? defaultValue)
        {
            var value = EnvironmentHelpers.GetEnvironmentVariable(key);
            if (string.IsNullOrEmpty(value))
            {
                return defaultValue;
            }

            if (DateTimeOffset.TryParseExact(value, "yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture, DateTimeStyles.None, out var valueDateTimeOffset))
            {
                return valueDateTimeOffset;
            }

            return defaultValue;
        }

        private static void SetEnvironmentVariablesIfNotEmpty(Dictionary<string, string> dictionary, params string[] keys)
        {
            if (dictionary is null || keys is null)
            {
                return;
            }

            foreach (var key in keys)
            {
                if (key is null)
                {
                    continue;
                }

                var value = EnvironmentHelpers.GetEnvironmentVariable(key);
                if (!string.IsNullOrEmpty(value))
                {
                    dictionary[key] = value;
                }
            }
        }

        private static bool IsHex(IEnumerable<char> chars)
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

        public string MakeRelativePathFromSourceRoot(string absolutePath, bool useOSSeparator = true)
        {
            var pivotFolder = SourceRoot;
            if (string.IsNullOrEmpty(pivotFolder))
            {
                return absolutePath;
            }

            if (string.IsNullOrEmpty(absolutePath))
            {
                return pivotFolder;
            }

            var folderSeparator = Path.DirectorySeparatorChar;
            if (pivotFolder[pivotFolder.Length - 1] != folderSeparator)
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

        internal void ReloadEnvironmentData()
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

            var gitInfo = GitInfo.GetCurrent();

            if (EnvironmentHelpers.GetEnvironmentVariable(Constants.Travis) != null)
            {
                SetupTravisEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.CircleCI) != null)
            {
                SetupCircleCiEnvironment();
                VariablesToBypass = new Dictionary<string, string>();
                SetEnvironmentVariablesIfNotEmpty(
                    VariablesToBypass,
                    Constants.CircleCIWorkflowId,
                    Constants.CircleCIBuildNum);
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsUrl) != null)
            {
                SetupJenkinsEnvironment();
                VariablesToBypass = new Dictionary<string, string>();
                SetEnvironmentVariablesIfNotEmpty(
                    VariablesToBypass,
                    Constants.JenkinsCustomTraceId);
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabCI) != null)
            {
                SetupGitlabEnvironment();
                VariablesToBypass = new Dictionary<string, string>();
                SetEnvironmentVariablesIfNotEmpty(
                    VariablesToBypass,
                    Constants.GitlabProjectUrl,
                    Constants.GitlabPipelineId,
                    Constants.GitlabJobId);
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.Appveyor) != null)
            {
                SetupAppveyorEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureTFBuild) != null)
            {
                SetupAzurePipelinesEnvironment();
                VariablesToBypass = new Dictionary<string, string>();
                SetEnvironmentVariablesIfNotEmpty(
                    VariablesToBypass,
                    Constants.AzureSystemTeamProjectId,
                    Constants.AzureBuildBuildId,
                    Constants.AzureSystemJobId);
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketCommit) != null)
            {
                SetupBitbucketEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubSha) != null)
            {
                SetupGithubActionsEnvironment();
                VariablesToBypass = new Dictionary<string, string>();
                SetEnvironmentVariablesIfNotEmpty(
                    VariablesToBypass,
                    Constants.GitHubServerUrl,
                    Constants.GitHubRepository,
                    Constants.GitHubRunId,
                    Constants.GitHubRunAttempt);
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.TeamCityVersion) != null)
            {
                SetupTeamcityEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKite) != null)
            {
                SetupBuildkiteEnvironment();
                VariablesToBypass = new Dictionary<string, string>();
                SetEnvironmentVariablesIfNotEmpty(
                    VariablesToBypass,
                    Constants.BuildKiteBuildId,
                    Constants.BuildKiteJobId);
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseBuildSlug) != null)
            {
                SetupBitriseEnvironment();
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.Buddy) != null)
            {
                SetupBuddyEnvironment(gitInfo);
            }
            else if (EnvironmentHelpers.GetEnvironmentVariable(Constants.CodefreshBuildId) != null)
            {
                SetupCodefreshEnvironment(gitInfo);
                VariablesToBypass = new Dictionary<string, string>();
                SetEnvironmentVariablesIfNotEmpty(
                    VariablesToBypass,
                    Constants.CodefreshBuildId);
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
                if (string.IsNullOrWhiteSpace(AuthorName) || string.IsNullOrWhiteSpace(AuthorEmail))
                {
                    if (!string.IsNullOrWhiteSpace(gitInfo.AuthorEmail))
                    {
                        AuthorEmail = gitInfo.AuthorEmail;
                    }

                    if (!string.IsNullOrWhiteSpace(gitInfo.AuthorName))
                    {
                        AuthorName = gitInfo.AuthorName;
                    }
                }

                AuthorDate ??= gitInfo.AuthorDate;

                if (string.IsNullOrWhiteSpace(CommitterName) || string.IsNullOrWhiteSpace(CommitterEmail))
                {
                    if (!string.IsNullOrWhiteSpace(gitInfo.CommitterEmail))
                    {
                        CommitterEmail = gitInfo.CommitterEmail;
                    }

                    if (!string.IsNullOrWhiteSpace(gitInfo.CommitterName))
                    {
                        CommitterName = gitInfo.CommitterName;
                    }
                }

                CommitterDate ??= gitInfo.CommitterDate;

                if (!string.IsNullOrWhiteSpace(gitInfo.Message))
                {
                    // Some CI's (eg Azure) adds the `Merge X into Y` message to the Pull Request
                    // If we have the original commit message we use that.
                    if (string.IsNullOrWhiteSpace(Message) ||
                        (Message.StartsWith("Merge", StringComparison.Ordinal) &&
                        !gitInfo.Message.StartsWith("Merge", StringComparison.Ordinal)))
                    {
                        Message = gitInfo.Message;
                    }
                }
            }
            else
            {
                Log.Warning("Git commit in .git folder is different from the one in the environment variables. [{GitCommit} != {EnvVarCommit}]", gitInfo.Commit, Commit);
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
            // Custom environment variables.
            // **********
            Branch = GetEnvironmentVariableIfIsNotEmpty(Constants.DDGitBranch, Branch);
            Tag = GetEnvironmentVariableIfIsNotEmpty(Constants.DDGitTag, Tag);
            Repository = GetEnvironmentVariableIfIsNotEmpty(
                Constants.DDGitRepository,
                Repository,
                (value, defaultValue) =>
                {
                    if (value is not null)
                    {
                        value = value.Trim();
                        if (value.Length == 0)
                        {
                            if (string.IsNullOrEmpty(defaultValue))
                            {
                                Log.Error("DD_GIT_REPOSITORY_URL is set with an empty value, and the Git repository could not be automatically extracted");
                            }
                            else
                            {
                                Log.Error("DD_GIT_REPOSITORY_URL is set with an empty value, defaulting to '{Default}'", defaultValue);
                            }

                            return false;
                        }

                        if (Regex.Match(value, RepositoryUrlPattern).Length != value.Length)
                        {
                            if (string.IsNullOrEmpty(defaultValue))
                            {
                                Log.Error("DD_GIT_REPOSITORY_URL is set with an invalid value ('{Value}'), and the Git repository could not be automatically extracted", value);
                            }
                            else
                            {
                                Log.Error("DD_GIT_REPOSITORY_URL is set with an invalid value ('{Value}'), defaulting to '{Default}'", value, defaultValue);
                            }

                            return false;
                        }

                        // All ok!
                        return true;
                    }

                    if (string.IsNullOrEmpty(defaultValue))
                    {
                        Log.Error("The Git repository couldn't be automatically extracted.");
                    }

                    // If not set use the default value
                    return false;
                });
            Commit = GetEnvironmentVariableIfIsNotEmpty(
                Constants.DDGitCommitSha,
                Commit,
                (value, defaultValue) =>
                {
                    if (value is not null)
                    {
                        value = value.Trim();
                        if (value.Length < 40 || !IsHex(value))
                        {
                            if (string.IsNullOrEmpty(defaultValue))
                            {
                                Log.Error("DD_GIT_COMMIT_SHA must be a full-length git SHA, and the The Git commit sha couldn't be automatically extracted.");
                            }
                            else
                            {
                                Log.Error("DD_GIT_COMMIT_SHA must be a full-length git SHA, defaulting to '{Default}", defaultValue);
                            }

                            return false;
                        }

                        // All ok!
                        return true;
                    }

                    if (string.IsNullOrEmpty(defaultValue))
                    {
                        Log.Error("The Git commit sha couldn't be automatically extracted.");
                    }

                    // If not set use the default value
                    return false;
                });
            Message = GetEnvironmentVariableIfIsNotEmpty(Constants.DDGitCommitMessage, Message);
            AuthorName = GetEnvironmentVariableIfIsNotEmpty(Constants.DDGitCommitAuthorName, AuthorName);
            AuthorEmail = GetEnvironmentVariableIfIsNotEmpty(Constants.DDGitCommitAuthorEmail, AuthorEmail);
            AuthorDate = GetDateTimeOffsetEnvironmentVariableIfIsNotEmpty(Constants.DDGitCommitAuthorDate, AuthorDate);
            CommitterName = GetEnvironmentVariableIfIsNotEmpty(Constants.DDGitCommitCommiterName, CommitterName);
            CommitterEmail = GetEnvironmentVariableIfIsNotEmpty(Constants.DDGitCommitCommiterEmail, CommitterEmail);
            CommitterDate = GetDateTimeOffsetEnvironmentVariableIfIsNotEmpty(Constants.DDGitCommitCommiterDate, CommitterDate);

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
                Repository = Repository.Replace(uriRepository.UserInfo + "@", string.Empty);
                Repository = Repository.Replace(uriRepository.UserInfo, string.Empty);
            }

            // **********
            // Try load CodeOwners
            // **********
            if (!string.IsNullOrEmpty(SourceRoot))
            {
                foreach (var codeOwnersPath in GetCodeOwnersPaths(SourceRoot))
                {
                    Log.Debug("Looking for CODEOWNERS file in: {Path}", codeOwnersPath);
                    if (File.Exists(codeOwnersPath))
                    {
                        Log.Debug("CODEOWNERS file found: {Path}", codeOwnersPath);
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

        private void SetupTravisEnvironment()
        {
            IsCI = true;
            Provider = "travisci";

            var prSlug = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisPullRequestSlug);
            var repoSlug = !string.IsNullOrEmpty(prSlug) ? prSlug : EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisRepoSlug);

            Repository = $"https://github.com/{repoSlug}.git";
            Commit = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisCommit);
            Tag = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisTag);
            if (string.IsNullOrEmpty(Tag))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisPullRequestBranch);
                if (string.IsNullOrWhiteSpace(Branch))
                {
                    Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisBranch);
                }
            }

            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisBuildDir);
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisBuildDir);
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisBuildId);
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisBuildNumber);
            PipelineName = repoSlug;
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisBuildWebUrl);
            JobUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisJobWebUrl);

            Message = EnvironmentHelpers.GetEnvironmentVariable(Constants.TravisCommitMessage);
        }

        private void SetupCircleCiEnvironment()
        {
            IsCI = true;
            Provider = "circleci";
            Repository = EnvironmentHelpers.GetEnvironmentVariable(Constants.CircleCIRepositoryUrl);
            Commit = EnvironmentHelpers.GetEnvironmentVariable(Constants.CircleCISha);
            Tag = EnvironmentHelpers.GetEnvironmentVariable(Constants.CircleCITag);
            if (string.IsNullOrEmpty(Tag))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.CircleCIBranch);
            }

            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable(Constants.CircleCIWorkingDirectory);
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable(Constants.CircleCIWorkingDirectory);
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable(Constants.CircleCIWorkflowId);
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable(Constants.CircleCIProjectRepoName);
            PipelineUrl = $"https://app.circleci.com/pipelines/workflows/{PipelineId}";
            JobName = EnvironmentHelpers.GetEnvironmentVariable(Constants.CircleCIJob);
            JobUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.CircleCIBuildUrl);
        }

        private void SetupJenkinsEnvironment()
        {
            IsCI = true;
            Provider = "jenkins";
            Repository = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsGitUrl);
            if (string.IsNullOrEmpty(Repository))
            {
                Repository = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsGitUrl1);
            }

            Commit = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsGitCommit);

            var gitBranch = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsGitBranch);
            if (gitBranch?.Contains("tags") == true)
            {
                Tag = gitBranch;
            }
            else
            {
                Branch = gitBranch;
            }

            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsWorkspace);
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsWorkspace);
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsBuildTag);
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsBuildNumber);
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsBuildUrl);

            // Pipeline Name algorithm from: https://github.com/DataDog/dd-trace-java/blob/master/internal-api/src/main/java/datadog/trace/bootstrap/instrumentation/api/ci/JenkinsInfo.java
            var pipelineName = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsJobName);
            if (pipelineName != null)
            {
                CleanBranchAndTag();

                // First, the git branch is removed from the raw jobName
                var jobNameNoBranch = Branch != null ? pipelineName.Trim().Replace("/" + Branch, string.Empty) : pipelineName;

                // Once the branch has been removed, we try to extract
                // the configurations from the job name.
                // The configurations have the form like "key1=value1,key2=value2"
                var configurations = new Dictionary<string, string>();
                var jobNameParts = jobNameNoBranch.Split('/');
                if (jobNameParts.Length > 1 && jobNameParts[1].Contains("="))
                {
                    var configsStr = jobNameParts[1].ToLowerInvariant().Trim();
                    var configsKeyValue = configsStr.Split(',');
                    foreach (var configKeyValue in configsKeyValue)
                    {
                        var keyValue = configKeyValue.Trim().Split('=');
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

            // Node
            NodeName = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsNodeName);
            NodeLabels = EnvironmentHelpers.GetEnvironmentVariable(Constants.JenkinsNodeLabels)?.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        }

        private void SetupGitlabEnvironment()
        {
            IsCI = true;
            Provider = "gitlab";
            Repository = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabRepositoryUrl);
            Commit = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabCommitSha);
            Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabCommitBranch);
            Tag = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabCommitTag);
            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabCommitRefName);
            }

            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabProjectDir);
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabProjectDir);

            PipelineId = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabPipelineId);
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabProjectPath);
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabPipelineIId);
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabPipelineUrl);

            JobUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabJobUrl);
            JobName = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabJobName);
            StageName = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabJobStage);

            Message = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabCommitMessage);

            var author = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabCommitAuthor);
            var authorArray = author.Split('<', '>');
            AuthorName = authorArray[0].Trim();
            AuthorEmail = authorArray[1].Trim();

            var authorDate = GetDateTimeOffsetEnvironmentVariableIfIsNotEmpty(Constants.GitlabCommitTimestamp, null);
            if (authorDate is not null)
            {
                AuthorDate = authorDate;
            }

            // Node
            NodeName = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabRunnerId);
            if (EnvironmentHelpers.GetEnvironmentVariable(Constants.GitlabRunnerTags) is { } runnerTags)
            {
                try
                {
                    NodeLabels = Datadog.Trace.Vendors.Newtonsoft.Json.JsonConvert.DeserializeObject<string[]>(runnerTags);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Error deserializing '{GitlabRunnerTags}' environment variable.", Constants.GitlabRunnerTags);
                }
            }
        }

        private void SetupAppveyorEnvironment()
        {
            IsCI = true;
            Provider = "appveyor";
            var repoProvider = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoProvider);
            if (repoProvider == "github")
            {
                Repository = string.Format("https://github.com/{0}.git", EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoName));
            }
            else
            {
                Repository = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoName);
            }

            Commit = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoCommit);
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorBuildFolder);
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorBuildFolder);
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorBuildId);
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoName);
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorBuildNumber);
            PipelineUrl = string.Format("https://ci.appveyor.com/project/{0}/builds/{1}", EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoName), EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorBuildId));
            JobUrl = PipelineUrl;
            Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorPullRequestHeadRepoBranch);
            Tag = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoTagName);
            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoBranch);
            }

            Message = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoCommitMessage);
            var extendedMessage = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoCommitMessageExtended);
            if (!string.IsNullOrWhiteSpace(extendedMessage))
            {
                Message = Message + "\n" + extendedMessage;
            }

            AuthorName = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoCommitAuthor);
            AuthorEmail = EnvironmentHelpers.GetEnvironmentVariable(Constants.AppveyorRepoCommitAuthorEmail);
        }

        private void SetupAzurePipelinesEnvironment()
        {
            IsCI = true;
            Provider = "azurepipelines";
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildSourcesDirectory);
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildSourcesDirectory);
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildBuildId);
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildDefinitionName);
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildBuildId);
            PipelineUrl = string.Format(
                "{0}{1}/_build/results?buildId={2}",
                EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureSystemTeamFoundationServerUri),
                EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureSystemTeamProjectId),
                EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildBuildId));

            StageName = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureSystemStageDisplayName);

            JobName = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureSystemJobDisplayName);
            JobUrl = string.Format(
                "{0}{1}/_build/results?buildId={2}&view=logs&j={3}&t={4}",
                EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureSystemTeamFoundationServerUri),
                EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureSystemTeamProjectId),
                EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildBuildId),
                EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureSystemJobId),
                EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureSystemTaskInstanceId));

            var prRepo = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureSystemPullRequestSourceRepositoryUri);
            Repository = !string.IsNullOrWhiteSpace(prRepo) ? prRepo : EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildRepositoryUri);

            var prCommit = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureSystemPullRequestSourceCommitId);
            Commit = !string.IsNullOrWhiteSpace(prCommit) ? prCommit : EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildSourceVersion);

            var prBranch = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureSystemPullRequestSourceBranch);
            Branch = !string.IsNullOrWhiteSpace(prBranch) ? prBranch : EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildSourceBranch);

            if (string.IsNullOrWhiteSpace(Branch))
            {
                Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildSourceBranchName);
            }

            Message = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildSourceVersionMessage);
            AuthorName = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildRequestedForId);
            AuthorEmail = EnvironmentHelpers.GetEnvironmentVariable(Constants.AzureBuildRequestedForEmail);
        }

        private void SetupBitbucketEnvironment()
        {
            IsCI = true;
            Provider = "bitbucket";
            Repository = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketGitSshOrigin);
            Commit = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketCommit);
            Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketBranch);
            Tag = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketTag);
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketCloneDir);
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketCloneDir);
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketPipelineUuid)?.Replace("}", string.Empty).Replace("{", string.Empty);
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketBuildNumber);
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketRepoFullName);
            PipelineUrl = string.Format(
                "https://bitbucket.org/{0}/addon/pipelines/home#!/results/{1}",
                EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketRepoFullName),
                EnvironmentHelpers.GetEnvironmentVariable(Constants.BitBucketBuildNumber));
            JobUrl = PipelineUrl;
        }

        private void SetupGithubActionsEnvironment()
        {
            IsCI = true;
            Provider = "github";

            var serverUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubServerUrl);
            if (string.IsNullOrWhiteSpace(serverUrl))
            {
                serverUrl = "https://github.com";
            }

            var rawRepository = $"{serverUrl}/{EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubRepository)}";
            Repository = $"{rawRepository}.git";
            Commit = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubSha);

            var headRef = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubHeadRef);
            var ghRef = !string.IsNullOrEmpty(headRef) ? headRef : EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubRef);
            if (ghRef?.Contains("tags") == true)
            {
                Tag = ghRef;
            }
            else
            {
                Branch = ghRef;
            }

            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubWorkspace);
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubWorkspace);
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubRunId);
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubRunNumber);
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubWorkflow);
            var attempts = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubRunAttempt);
            if (string.IsNullOrWhiteSpace(attempts))
            {
                PipelineUrl = $"{rawRepository}/actions/runs/{PipelineId}";
            }
            else
            {
                PipelineUrl = $"{rawRepository}/actions/runs/{PipelineId}/attempts/{attempts}";
            }

            JobUrl = $"{serverUrl}/{EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubRepository)}/commit/{Commit}/checks";
            JobName = EnvironmentHelpers.GetEnvironmentVariable(Constants.GitHubJob);
        }

        private void SetupTeamcityEnvironment()
        {
            IsCI = true;
            Provider = "teamcity";
            JobName = EnvironmentHelpers.GetEnvironmentVariable(Constants.TeamCityBuildConfName);
            JobUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.TeamCityBuildUrl);
        }

        private void SetupBuildkiteEnvironment()
        {
            IsCI = true;
            Provider = "buildkite";
            Repository = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteRepo);
            Commit = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteCommit);
            Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteBranch);
            Tag = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteTag);
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteBuildCheckoutPath);
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteBuildCheckoutPath);
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteBuildId);
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteBuildNumber);
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKitePipelineSlug);
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteBuildUrl);
            JobUrl = string.Format("{0}#{1}", EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteBuildUrl), EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteJobId));

            Message = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteMessage);
            AuthorName = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteBuildAuthor);
            AuthorEmail = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteBuildAuthorEmail);
            CommitterName = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteBuildCreator);
            CommitterEmail = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteBuildCreatorEmail);

            // Node
            NodeName = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuildKiteAgentId);
            var lstNodeLabels = new List<string>();
            foreach (DictionaryEntry envvar in EnvironmentHelpers.GetEnvironmentVariables())
            {
                if (envvar.Key is string key && key.StartsWith(Constants.BuildKiteAgentMetadata, StringComparison.OrdinalIgnoreCase))
                {
                    var name = key.Substring(Constants.BuildKiteAgentMetadata.Length).ToLowerInvariant();
                    var value = envvar.Value?.ToString();
                    lstNodeLabels.Add($"{name}:{value}");
                }
            }

            if (lstNodeLabels.Count > 0)
            {
                NodeLabels = lstNodeLabels.ToArray();
            }
        }

        private void SetupBitriseEnvironment()
        {
            IsCI = true;
            Provider = "bitrise";
            Repository = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseGitRepositoryUrl);

            var prCommit = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseGitCommit);
            Commit = !string.IsNullOrWhiteSpace(prCommit) ? prCommit : EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseGitCloneCommitHash);

            var prBranch = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseGitBranchDest);
            Branch = !string.IsNullOrWhiteSpace(prBranch) ? prBranch : EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseGitBranch);

            Tag = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseGitTag);
            SourceRoot = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseSourceDir);
            WorkspacePath = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseSourceDir);
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseBuildSlug);
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseBuildNumber);
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseTriggeredWorkflowId);
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseBuildUrl);

            Message = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseGitMessage);
            AuthorName = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseCloneCommitAuthorName);
            AuthorEmail = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseCloneCommitAuthorEmail);
            CommitterName = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseCloneCommitCommiterName);
            CommitterEmail = EnvironmentHelpers.GetEnvironmentVariable(Constants.BitriseCloneCommitCommiterEmail);
            if (string.IsNullOrWhiteSpace(CommitterEmail))
            {
                CommitterEmail = CommitterName;
            }
        }

        private void SetupBuddyEnvironment(GitInfo gitInfo)
        {
            IsCI = true;
            Provider = "buddy";
            Repository = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyScmUrl);
            Commit = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyExecutionRevision);
            Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyExecutionBranch);
            Tag = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyExecutionTag);

            PipelineId = string.Format(
                "{0}/{1}",
                EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyPipelineId),
                EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyExecutionId));
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyPipelineName);
            PipelineNumber = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyExecutionId);
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyExecutionUrl);

            Message = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyExecutionRevisionMessage);
            CommitterName = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyExecutionRevisionCommitterName);
            CommitterEmail = EnvironmentHelpers.GetEnvironmentVariable(Constants.BuddyExecutionRevisionCommitterEmail);
            if (string.IsNullOrWhiteSpace(CommitterEmail))
            {
                CommitterEmail = CommitterName;
            }

            SourceRoot = gitInfo.SourceRoot;
            WorkspacePath = gitInfo.SourceRoot;
        }

        private void SetupCodefreshEnvironment(GitInfo gitInfo)
        {
            IsCI = true;
            Provider = "codefresh";
            PipelineId = EnvironmentHelpers.GetEnvironmentVariable(Constants.CodefreshBuildId);
            PipelineName = EnvironmentHelpers.GetEnvironmentVariable(Constants.CodefreshPipelineName);
            PipelineUrl = EnvironmentHelpers.GetEnvironmentVariable(Constants.CodefreshBuildUrl);
            JobName = EnvironmentHelpers.GetEnvironmentVariable(Constants.CodefreshStepName);
            Branch = EnvironmentHelpers.GetEnvironmentVariable(Constants.CodefreshBranch) ?? gitInfo.Branch;

            Commit = gitInfo.Commit;
            Repository = gitInfo.Repository;
            Message = gitInfo.Message;
            AuthorName = gitInfo.AuthorName;
            AuthorEmail = gitInfo.AuthorEmail;
            AuthorDate = gitInfo.AuthorDate;
            CommitterName = gitInfo.CommitterName;
            CommitterEmail = gitInfo.CommitterEmail;
            CommitterDate = gitInfo.CommitterDate;
            SourceRoot = gitInfo.SourceRoot;
            WorkspacePath = gitInfo.SourceRoot;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void CleanBranchAndTag()
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
}
