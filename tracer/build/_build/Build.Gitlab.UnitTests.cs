using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Newtonsoft.Json;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Logger = Serilog.Log;

partial class Build
{
    [Parameter("JSON list of changed files captured from the GitLab runner checkout")]
    readonly AbsolutePath GitlabChangedFiles;

    [Parameter("Require GitLab CI Visibility credentials for managed unit tests. Defaults to false.")]
    readonly bool RequireGitlabCiVisibility;

    string GitlabUnitTestsBaseBranch => string.IsNullOrEmpty(TargetBranch) ? ReleaseBranchForCurrentVersion() : $"origin/{TargetBranch}";

    Target WriteGitlabUnitTestsBaseBranch => _ => _
        .Unlisted()
        .Executes(() =>
        {
            var outputDirectory = RootDirectory / ".gitlab" / "generated";
            Directory.CreateDirectory(outputDirectory);
            File.WriteAllText(outputDirectory / "base-branch.txt", GitlabUnitTestsBaseBranch);
        });

    Target ConfigureGitlabUnitTestCredentials => _ => _
        .Unlisted()
        .After(CompileManagedUnitTests)
        .OnlyWhenDynamic(() => RequireGitlabCiVisibility)
        .Executes(() =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DD_LOGGER_DD_API_KEY")))
                {
                    return;
                }

                var oidcToken = Environment.GetEnvironmentVariable("DD_STS_OIDC_TOKEN");
                if (string.IsNullOrWhiteSpace(oidcToken))
                {
                    throw new InvalidOperationException("DD_STS_OIDC_TOKEN is unavailable");
                }

                using var client = new HttpClient();
                using var request = new HttpRequestMessage(
                    HttpMethod.Get,
                    "https://dd-sts.us1.ddbuild.io/sts/datadog/exchange?policy=apm-sdks-api-key");
                request.Headers.Authorization = new("Bearer", oidcToken);
                using var response = client.SendAsync(request).GetAwaiter().GetResult();
                response.EnsureSuccessStatusCode();
                using var responseStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult();
                using var payload = JsonDocument.Parse(responseStream);
                if (!payload.RootElement.TryGetProperty("api_key", out var apiKeyElement)
                 || string.IsNullOrWhiteSpace(apiKeyElement.GetString()))
                {
                    throw new InvalidOperationException("The dd-sts response did not contain an API key");
                }

                Environment.SetEnvironmentVariable("DD_LOGGER_DD_API_KEY", apiKeyElement.GetString());
                Logger.Information("CI Visibility API key configured using dd-sts");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("CI Visibility credentials are required for GitLab unit tests", ex);
            }
            finally
            {
                // Test processes need the API key, not the token used to obtain it.
                Environment.SetEnvironmentVariable("DD_STS_OIDC_TOKEN", null);
            }
        });

    Target GenerateGitlabUnitTestsPipelines => _ => _
        .Unlisted()
        .Requires(() => GitlabChangedFiles)
        .Executes(() =>
        {
            // The runner's Git alternates may not be visible inside a container.
            // Capture paths on the host, but keep selection decisions in shared NUKE code.
            var changedFiles = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(GitlabChangedFiles))
                            ?? throw new InvalidOperationException("The GitLab changed-file list is missing");
            var baseBranch = GitlabUnitTestsBaseBranch;
            var includeAll = IncludeAllTestFrameworks || IsGitBaseBranch(baseBranch) || HasChangesRequiringThoroughTesting(changedFiles);
            var outputDirectory = RootDirectory / ".gitlab" / "generated";
            Directory.CreateDirectory(outputDirectory);

            WritePipeline("windows", PlatformFamily.Windows, isArm64: false);
            WritePipeline("linux", PlatformFamily.Linux, isArm64: false);
            WritePipeline("linux-arm64", PlatformFamily.Linux, isArm64: true);

            void WritePipeline(string name, PlatformFamily platform, bool isArm64)
            {
                var template = platform == PlatformFamily.Windows ? "windows" : "linux";
                var yaml = new StringBuilder($"include:\n  - local: .gitlab/{template}-unit-tests-child.yml\n\nstages:\n  - test\n\n");
                foreach (var framework in GetTestingFrameworks(platform, isArm64, includeAll))
                {
                    if (platform == PlatformFamily.Windows)
                    {
                        AppendJob($"unit-tests-windows:{framework}", ".windows-unit-test", framework);
                    }
                    else
                    {
                        var architecture = isArm64 ? "arm64" : "x64";
                        AppendJob($"unit-tests-linux-{architecture}:{framework}", $".linux-unit-test-{architecture}-glibc", framework);
                        AppendJob($"unit-tests-linux-musl-{architecture}:{framework}", $".linux-unit-test-{architecture}-musl", framework);
                    }
                }

                var output = outputDirectory / $"{name}-unit-tests.yml";
                File.WriteAllText(output, yaml.ToString());
                Logger.Information("Generated {Path} (all frameworks: {IncludeAll})", output, includeAll);

                void AppendJob(string job, string extends, TargetFramework framework)
                {
                    yaml.AppendLine($"\"{job}\":");
                    yaml.AppendLine($"  extends: {extends}");
                    yaml.AppendLine("  variables:");
                    yaml.AppendLine($"    FRAMEWORK: \"{framework}\"");
                }
            }
        });
}
