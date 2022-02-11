using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using Newtonsoft.Json;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Tools.Git;
using NukeExtensions;
using YamlDotNet.Serialization.NamingConventions;

partial class Build : NukeBuild
{
    Target GenerateVariables
        => _ =>
        {
            return _
                  .Unlisted()
                  .Executes(() =>
                   {
                       GenerateConditionVariables();

                       GenerateIntegrationTestsWindowsMatrices();
                       GenerateIntegrationTestsLinuxMatrix();
                       GenerateExplorationTestMatrices();
                       GenerateSmokeTestsMatrices();
                   });

            void GenerateConditionVariables()
            {
                GenerateConditionVariableBasedOnGitChange("isDebuggerChanged", new[] { "tracer/src/Datadog.Trace.ClrProfiler.Native/Debugger" });
                GenerateConditionVariableBasedOnGitChange("isProfilerChanged", new[] { "profiler/src" });

                void GenerateConditionVariableBasedOnGitChange(string variableName, string[] filters)
                {
                    var changedFiles = GetGitChangedFiles("origin/master");

                    var isChanged = filters.Any(filter => changedFiles.Any(s => s.Contains(filter)));

                    Logger.Info($"{variableName} - {isChanged}");

                    var variableValue = isChanged.ToString();
                    EnvironmentInfo.SetVariable(variableName, variableValue);
                    AzurePipelines.Instance.SetVariable(variableName, variableValue);
                }
            }

            void GenerateIntegrationTestsWindowsMatrices()
            {
                var targetFrameworks = TargetFramework.GetFrameworks(except: new[] { TargetFramework.NETSTANDARD2_0 });

                GenerateIntegrationTestsWindowsMatrix(targetFrameworks);
                GenerateIntegrationTestsWindowsIISMatrix(targetFrameworks);
            }

            void GenerateIntegrationTestsWindowsMatrix(TargetFramework[] targetFrameworks)
            {
                var targetPlatforms = new[] { "x86", "x64" };
                var matrix = new Dictionary<string, object>();

                foreach (var framework in targetFrameworks)
                {
                    foreach (var targetPlatform in targetPlatforms)
                    {
                        matrix.Add($"{targetPlatform}_{framework}", new { framework = framework, targetPlatform = targetPlatform });
                    }
                }

                Logger.Info($"Integration test windows matrix");
                Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("integration_tests_windows_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsWindowsIISMatrix(TargetFramework[] targetFrameworks)
            {
                var targetPlatforms = new[] { "x86", "x64" };

                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var targetPlatform in targetPlatforms)
                    {
                        var enable32bit = targetPlatform == "x86";
                        matrix.Add($"{targetPlatform}_{framework}", new { framework = framework, targetPlatform = targetPlatform, enable32bit = enable32bit });
                    }
                }

                Logger.Info($"Integration test windows IIS matrix");
                Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("integration_tests_windows_iis_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsLinuxMatrix()
            {
                var targetFrameworks = TargetFramework.GetFrameworks(except: new[] { TargetFramework.NET461, TargetFramework.NETSTANDARD2_0, });

                var baseImages = new[] { "debian", "alpine" };

                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var baseImage in baseImages)
                    {
                        matrix.Add($"{baseImage}_{framework}", new { publishTargetFramework = framework, baseImage = baseImage });
                    }
                }

                Logger.Info($"Integration test linux matrix");
                Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("integration_tests_linux_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateExplorationTestMatrices()
            {
                var isDebuggerChanged = bool.Parse(EnvironmentInfo.GetVariable<string>("isDebuggerChanged") ?? "false");
                var isProfilerChanged = bool.Parse(EnvironmentInfo.GetVariable<string>("isProfilerChanged") ?? "false");

                var useCases = new List<string>();
                if (isDebuggerChanged)
                {
                    useCases.Add(global::ExplorationTestUseCase.Debugger.ToString());
                }

                if (isProfilerChanged)
                {
                    useCases.Add(global::ExplorationTestUseCase.ContinuousProfiler.ToString());
                }

                GenerateExplorationTestsWindowsMatrix(useCases);
                GenerateExplorationTestsLinuxMatrix(useCases);
            }

            void GenerateExplorationTestsWindowsMatrix(IEnumerable<string> useCases)
            {
                var testDescriptions = ExplorationTestDescription.GetAllExplorationTestDescriptions();
                var matrix = new Dictionary<string, object>();
                foreach (var explorationTestUseCase in useCases)
                {
                    foreach (var testDescription in testDescriptions)
                    {
                        matrix.Add(
                            $"{explorationTestUseCase}_{testDescription.Name}",
                            new { explorationTestUseCase = explorationTestUseCase, explorationTestName = testDescription.Name });
                    }
                }

                Logger.Info($"Exploration test windows matrix");
                Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("exploration_tests_windows_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateExplorationTestsLinuxMatrix(IEnumerable<string> useCases)
            {
                var testDescriptions = ExplorationTestDescription.GetAllExplorationTestDescriptions();
                var targetFrameworks = TargetFramework.GetFrameworks(except: new[] { TargetFramework.NET461, TargetFramework.NETSTANDARD2_0, });

                var baseImages = new[] { "debian", "alpine" };

                var matrix = new Dictionary<string, object>();

                foreach (var baseImage in baseImages)
                {
                    foreach (var explorationTestUseCase in useCases)
                    {
                        foreach (var targetFramework in targetFrameworks)
                        {
                            foreach (var testDescription in testDescriptions)
                            {
                                if (testDescription.IsFrameworkSupported(targetFramework))
                                {
                                    matrix.Add(
                                        $"{baseImage}_{targetFramework}_{explorationTestUseCase}_{testDescription.Name}",
                                        new { baseImage = baseImage, targetFramework = targetFramework, explorationTestUseCase = explorationTestUseCase, explorationTestName = testDescription.Name });
                                }
                            }
                        }
                    }
                }

                Logger.Info($"Exploration test linux matrix");
                Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("exploration_tests_linux_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateSmokeTestsMatrices()
            {
                GenerateSmokeTestsMatrix();
                GenerateSmokeTestsArm64Matrix();

                void GenerateSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToMatrix(
                        matrix,
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-bullseye-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-focal"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bullseye-slim"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-buster-slim"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bionic"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-bionic"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-stretch-slim"),
                        },
                        installCmd: "dpkg -i ./datadog-dotnet-apm*_amd64.deb",
                        linuxArtifacts: "linux-packages-debian",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToMatrix(
                        matrix,
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "34-6.0"),
                            (publishFramework: TargetFramework.NET5_0, "35-5.0"),
                            (publishFramework: TargetFramework.NET5_0, "34-5.0"),
                            (publishFramework: TargetFramework.NET5_0, "33-5.0"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "35-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "34-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "33-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "29-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "29-2.1"),
                        },
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-debian",
                        dockerName: "andrewlock/dotnet-fedora"
                    );

                    AddToMatrix(
                        matrix,
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.14"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.14"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.13"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.13"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12"),
                        },
                        installCmd: "tar -C /opt/datadog -xzf ./datadog-dotnet-apm*-musl.tar.gz",
                        linuxArtifacts: "linux-packages-alpine",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    Logger.Info($"Installer smoke tests matrix");
                    Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetVariable("installer_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateSmokeTestsArm64Matrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToMatrix(
                        matrix,
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-bullseye-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-focal"),
                        },
                        installCmd: "dpkg -i ./datadog-dotnet-apm_*_arm64.deb",
                        linuxArtifacts: "linux-packages-arm64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    Logger.Info($"Installer smoke tests matrix");
                    Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetVariable("installer_smoke_tests_arm64_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void AddToMatrix(
                    Dictionary<string, object> matrix,
                    (string publishFramework, string runtimeTag)[] images,
                    string installCmd,
                    string linuxArtifacts,
                    string dockerName
                )
                {
                    foreach (var image in images)
                    {
                        var dockerTag = image.runtimeTag.Replace('.', '_');
                        matrix.Add(
                            dockerTag,
                            new
                            {
                                installCmd = installCmd,
                                dockerTag = dockerTag,
                                publishFramework = image.publishFramework,
                                linuxArtifacts = linuxArtifacts,
                                runtimeImage = $"{dockerName}:{image.runtimeTag}"
                            });
                    }
                }
            }
        };

    Target GenerateNoopStages
        => _ => _
       .Unlisted()
       .Executes(() =>
       {
           // This assumes that we're running in a pull request, so we compare against the target branch
           // If it wasn't a pull request, then we should compare against "HEAD~", but the noop pipeline
           // only runs on pull requests
           var baseBranch = $"origin/{TargetBranch}";
           Logger.Info($"Generating variables for base branch: {baseBranch}");

           var tracerConfig = GetTracerPipelineDefinition();

           var tracerExcludePaths = tracerConfig.Pr?.Paths?.Exclude ?? Array.Empty<string>();
           Logger.Info($"Found {tracerExcludePaths.Length} exclude paths for the tracer");

           var profilerConfig = GetProfilerPipelineDefinition();

           var profilerExcludePaths = profilerConfig.On?.PullRequest?.PathsIgnore ?? Array.Empty<string>();
           Matcher profilerPathMatcher = new();
           profilerPathMatcher.AddInclude("**");
           profilerPathMatcher.AddExcludePatterns(profilerExcludePaths);

           Logger.Info($"Found {profilerExcludePaths.Length} exclude paths for the profiler");

           var gitChanges = GetGitChangedFiles(baseBranch);
           Logger.Info($"Found {gitChanges.Length} modified paths");

           var willTracerPipelineRun = gitChanges.Any(
               changed => !tracerExcludePaths.Any(prefix => changed.StartsWith(prefix)));

           var profilerMatchedPaths = profilerPathMatcher.Match(gitChanges);

           StringBuilder message = new();
           message.Append("Based on git changes, ");

           string variableValue;
           if (willTracerPipelineRun)
           {
               if (profilerMatchedPaths.HasMatches)
               {
                   message.Append("tracer consolidated pipeline and profiler pipeline will run. Skipping no-op pipeline.");
                   variableValue = "{}";
                   AzurePipelines.Instance.SetVariable("noop_run_skip_stages", "false");
               }
               else
               {
                   var stages = (from jobName in GenerateProfilerJobsName(profilerConfig)
                                 select new { Name = "skip_profiler_" + jobName, Value = new { StageToSkip = jobName } })
                     .ToDictionary(x => x.Name, x => x.Value);

                   message.Append($"profiler pipeline will not run. Generating github status updates for {stages.Count} stages");
                   variableValue = JsonConvert.SerializeObject(stages, Formatting.Indented);
                   AzurePipelines.Instance.SetVariable("noop_run_skip_stages", "true");
               }
           }
           else
           {
               var stages = (from stage in tracerConfig.Stages
                             select new { Name = "skip_" + stage.Stage, Value = new { StageToSkip = stage.Stage } })
                  .ToDictionary(x => x.Name, x => x.Value);

               message.Append("tracer consolidated pipeline ");
               if (!profilerMatchedPaths.HasMatches)
               {
                   foreach (var jobName in GenerateProfilerJobsName(profilerConfig))
                   {
                       stages["skip_profiler_" + jobName] = new { StageToSkip = jobName };
                   }
                   message.Append("and profiler pipeline ");
               }

               message.Append($"will not run. Generating github status for {stages.Count} stages");
               variableValue = JsonConvert.SerializeObject(stages, Formatting.Indented);
               AzurePipelines.Instance.SetVariable("noop_run_skip_stages", "true");
           }

           Logger.Info(message.ToString());
           Logger.Info("Setting noop_stages: " + variableValue);
           AzurePipelines.Instance.SetVariable("noop_stages", variableValue);

           PipelineDefinition GetTracerPipelineDefinition()
           {
               var consolidatedPipelineYaml = RootDirectory / ".azure-pipelines" / "ultimate-pipeline.yml";
               Logger.Info($"Reading {consolidatedPipelineYaml} YAML file");
               var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                                 .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                 .IgnoreUnmatchedProperties()
                                 .Build();

               using var sr = new StreamReader(consolidatedPipelineYaml);
               return deserializer.Deserialize<PipelineDefinition>(sr);
           }

           ProfilerPipelineDefinition GetProfilerPipelineDefinition()
           {
               var profilerPipelineYaml = RootDirectory / ".github" / "workflows" / "profiler-pipeline.yml";
               Logger.Info($"Reading {profilerPipelineYaml} YAML file");
               var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                                 .WithNamingConvention(CamelCaseNamingConvention.Instance)
                                 .IgnoreUnmatchedProperties()
                                 .Build();

               using var sr = new StreamReader(profilerPipelineYaml);
               return deserializer.Deserialize<ProfilerPipelineDefinition>(sr);
           }
       });

    // taken from https://ericlippert.com/2010/06/28/computing-a-cartesian-product-with-linq/
    static IEnumerable<IEnumerable<T>> CartesianProduct<T>(IEnumerable<IEnumerable<T>> sequences)
    {
        // base case:
        IEnumerable<IEnumerable<T>> result = new[] { Enumerable.Empty<T>() };
        foreach (var sequence in sequences)
        {
            // recursive case: use SelectMany to build
            // the new product out of the old one
            result =
              from seq in result
              from item in sequence
              select seq.Concat(new[] { item });
        }
        return result;
    }

    static IEnumerable<string> GenerateProfilerJobsName(ProfilerPipelineDefinition profiler)
    {
        foreach (var (name, job) in profiler.Jobs)
        {
            var jobName = job?.Name ?? name;
            if (job.Strategy == null || job.Strategy.Matrix == null)
            {
                yield return jobName;
            }
            else
            {
                var matrix = job.Strategy.Matrix;
                foreach (var product in CartesianProduct(matrix.Values))
                {
                    yield return $"{jobName} ({string.Join(", ", product)})";
                }
            }
        }
    }

    static string[] GetGitChangedFiles(string baseBranch)
    {
        var baseCommit = GitTasks.Git($"merge-base {baseBranch} HEAD").First().Text;
        return GitTasks
              .Git($"diff --name-only \"{baseCommit}\"")
              .Select(output => output.Text)
              .ToArray();
    }

    class PipelineDefinition
    {
        public TriggerDefinition Trigger { get; set; }
        public TriggerDefinition Pr { get; set; }
        public StageDefinition[] Stages { get; set; } = Array.Empty<StageDefinition>();

        public class TriggerDefinition
        {
            public PathDefinition Paths { get; set; }
        }

        public class PathDefinition
        {
            public string[] Exclude { get; set; } = Array.Empty<string>();
        }

        public class StageDefinition
        {
            public string Stage { get; set; }
        }
    }

    class ProfilerPipelineDefinition
    {
        public TriggerDefinition On { get; set; }

        public Dictionary<string, JobDefinition> Jobs { get; set; } = new();

        public class TriggerDefinition
        {
            [YamlDotNet.Serialization.YamlMember(Alias = "pull_request", ApplyNamingConventions = false)]
            public PrDefinition PullRequest { get; set; }
        }

        public class PrDefinition
        {
            [YamlDotNet.Serialization.YamlMember(Alias = "paths-ignore", ApplyNamingConventions = false)]
            public string[] PathsIgnore { get; set; } = Array.Empty<string>();
        }

        public class JobDefinition
        {
            public string Name { get; set; }
            public StrategyDefinition Strategy { get; set; }
        }

        public class StrategyDefinition
        {
            public Dictionary<string, List<string>> Matrix { get; set; }
        }
    }
}
