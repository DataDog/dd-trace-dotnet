using System;
using System.IO;
using System.Text;
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

    [Parameter("Output directory for GitLab unit-test child pipelines")]
    readonly AbsolutePath GitlabUnitTestPipelineDirectory;

    Target GenerateGitlabUnitTestsPipelines => _ => _
        .Unlisted()
        .Requires(() => GitlabChangedFiles)
        .Executes(() =>
        {
            // The runner's Git alternates may not be visible inside a container.
            // Capture paths on the host, but keep selection decisions in shared NUKE code.
            var changedFiles = JsonConvert.DeserializeObject<string[]>(File.ReadAllText(GitlabChangedFiles))
                            ?? throw new InvalidOperationException("The GitLab changed-file list is missing");
            var baseBranch = string.IsNullOrEmpty(TargetBranch) ? ReleaseBranchForCurrentVersion() : $"origin/{TargetBranch}";
            var includeAll = IncludeAllTestFrameworks || IsGitBaseBranch(baseBranch) || HasChangesRequiringThoroughTesting(changedFiles);
            var outputDirectory = GitlabUnitTestPipelineDirectory ?? RootDirectory / ".gitlab" / "generated";
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
