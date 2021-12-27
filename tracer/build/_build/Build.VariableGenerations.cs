using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Execution;
using Nuke.Common.Tools.Git;
using NukeExtensions;

partial class Build : NukeBuild
{

    [Parameter("Indicates matrices target. Match the name of generated variable", List = false)]
    readonly GenerateMatricesTarget? GenerateMatricesTarget;

    [Parameter("Indicates condition. Must be in the format '{variableName}|[filter1,filter2];{variableName}|[filter1,filter2]'", List = false)]
    readonly string GenerateConditionVariableFilter;

    Target GenerateMatrices
        => _ =>
        {
            return _
                  .Unlisted()
                  .Requires(() => GenerateMatricesTarget)
                  .Executes(() =>
                  {
                      if (!CheckChanges())
                      {
                          Logger.Info($"No changes found.");
                          return;
                      }

                      var target = GenerateMatricesTarget ?? global::GenerateMatricesTarget.none;
                      if (global::GenerateMatricesTarget.integration_tests_windows_matrices.HasFlag(target))
                      {
                          GenerateIntegrationTestsWindowsMatrices(target);
                      }

                      if (target.HasFlag(global::GenerateMatricesTarget.integration_tests_linux_matrix))
                      {
                          GenerateIntegrationTestsLinuxMatrix();
                      }

                      if (global::GenerateMatricesTarget.exploration_tests_matrices.HasFlag(target))
                      {
                          GenerateExplorationTestMatrices(target);
                      }
                  });

            bool CheckChanges()
            {
                if (string.IsNullOrWhiteSpace(GenerateConditionVariableFilter))
                {
                    Logger.Info("Filter condition is not provided, assuming true.");
                    return true;
                }

                var varFilterConditions = GenerateConditionVariableFilter.Split(';');

                var isChanged = false;

                foreach (var varFilterCondition in varFilterConditions)
                {
                    Logger.Info($"Checking changes for {varFilterCondition}");
                    isChanged = CheckChange(varFilterCondition) || isChanged;
                }

                return isChanged;

                bool CheckChange(string varFilterCondition)
                {
                    var arr = varFilterCondition.Split('|');
                    var variableName = arr[0];
                    var filters = arr[1].Split(',');

                    var masterCommit = GitTasks.Git("merge-base origin/master HEAD").First().Text;
                    var changedFiles =
                            GitTasks
                               .Git($"diff --name-only \"{masterCommit}\"")
                               .Select(output => output.Text)
                               .ToArray()
                        ;

                    var isChanged = filters.Any(filter => changedFiles.Any(s => s.Contains(filter)));

                    Logger.Info($"{variableName} - {isChanged}");

                    var variableValue = isChanged.ToString();
                    EnvironmentInfo.SetVariable(variableName, variableValue);
                    AzurePipelines.Instance.SetVariable(variableName, variableValue);

                    return isChanged;
                }
            }

            void GenerateIntegrationTestsWindowsMatrices(GenerateMatricesTarget target)
            {
                var targetFrameworks = TargetFramework.GetFrameworks(new[] { TargetFramework.NETSTANDARD2_0 });

                if (target.HasFlag(global::GenerateMatricesTarget.integration_tests_windows_matrix))
                {
                    GenerateIntegrationTestsWindowsMatrix(targetFrameworks);
                }

                if (target.HasFlag(global::GenerateMatricesTarget.integration_tests_windows_iis_matrix))
                {
                    GenerateIntegrationTestsWindowsIISMatrix(targetFrameworks);
                }
            }

            void GenerateIntegrationTestsWindowsMatrix(TargetFramework[] targetFrameworks)
            {
                var targetPlatforms = new[] { "x86", "x64" };
                var matrix = new Dictionary<string, object>();

                foreach (var framework in targetFrameworks)
                {
                    foreach (var targetPlatform in targetPlatforms)
                    {
                        matrix.Add($"{targetPlatform}_{framework}", new { framework, targetPlatform });
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
                        matrix.Add($"{targetPlatform}_{framework}", new { framework, targetPlatform, enable32bit });
                    }
                }

                Logger.Info($"Integration test windows IIS matrix");
                Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("integration_tests_windows_iis_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsLinuxMatrix()
            {
                var targetFrameworks = TargetFramework.GetFrameworks(new[] { TargetFramework.NET461, TargetFramework.NETSTANDARD2_0, });

                var baseImages = new[] { "debian", "alpine" };

                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var baseImage in baseImages)
                    {
                        matrix.Add($"{baseImage}_{framework}", new { publishTargetFramework = framework, baseImage });
                    }
                }

                Logger.Info($"Integration test linux matrix");
                Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("integration_tests_linux_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateExplorationTestMatrices(GenerateMatricesTarget target)
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

                if (target.HasFlag(global::GenerateMatricesTarget.exploration_tests_windows_matrix))
                {
                    GenerateExplorationTestsWindowsMatrix(useCases);
                }

                if (target.HasFlag(global::GenerateMatricesTarget.exploration_tests_linux_matrix))
                {
                    GenerateExplorationTestsLinuxMatrix(useCases);
                }
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
                            new { explorationTestUseCase, explorationTestName = testDescription.Name });
                    }
                }

                Logger.Info($"Exploration test windows matrix");
                Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("exploration_tests_windows_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateExplorationTestsLinuxMatrix(IEnumerable<string> useCases)
            {
                var testDescriptions = ExplorationTestDescription.GetAllExplorationTestDescriptions();
                var targetFrameworks = TargetFramework.GetFrameworks(new[] { TargetFramework.NET461, TargetFramework.NETSTANDARD2_0, });

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
                                        new { baseImage, targetFramework, explorationTestUseCase, explorationTestName = testDescription.Name });
                                }
                            }
                        }
                    }
                }


                Logger.Info($"Exploration test linux matrix");
                Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("exploration_tests_linux_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }
        };
}

[Flags]
public enum GenerateMatricesTarget
{
    none = 0,
    integration_tests_windows_matrix = 1 << 0,
    integration_tests_windows_iis_matrix = 1 << 1,
    integration_tests_linux_matrix = 1 << 2,
    exploration_tests_windows_matrix = 1 << 3,
    exploration_tests_linux_matrix = 1 << 4,

    integration_tests_windows_matrices = integration_tests_windows_matrix | integration_tests_windows_iis_matrix,
    exploration_tests_matrices = exploration_tests_windows_matrix | exploration_tests_linux_matrix
}
