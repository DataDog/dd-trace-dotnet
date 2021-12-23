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

    [Parameter("Indicates strategies target. Match the name of generated variable", List = false)]
    readonly GenerateStrategiesTarget? GenerateStrategiesTarget;

    [Parameter("Indicates condition. Must be in the format '{variableName}|[filter1,filter2];{variableName}|[filter1,filter2]'", List = false)]
    readonly string GenerateConditionVariableFilter;

    Target GenerateStrategies
        => _ =>
        {
            return _
                  .Unlisted()
                  .Requires(() => GenerateStrategiesTarget)
                  .Executes(() =>
                  {
                      if (!CheckChanges())
                      {
                          Logger.Info($"No changes found.");
                          return;
                      }

                      var target = GenerateStrategiesTarget ?? global::GenerateStrategiesTarget.none;
                      if (global::GenerateStrategiesTarget.integration_tests_windows.HasFlag(target))
                      {
                          GenerateIntegrationTestsWindowsStrategies(target);
                      }

                      if (target.HasFlag(global::GenerateStrategiesTarget.integration_tests_linux_strategy))
                      {
                          GenerateIntegrationTestsLinux();
                      }

                      if (global::GenerateStrategiesTarget.exploration_tests.HasFlag(target))
                      {
                          GenerateExplorationTestStrategies(target);
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

            void GenerateIntegrationTestsWindowsStrategies(GenerateStrategiesTarget target)
            {
                var targetFrameworks =
                    typeof(TargetFramework)
                       .GetFields(ReflectionService.Static)
                       .Select(x => x.GetValue(null))
                       .Cast<TargetFramework>()
                       .Except(new[] { TargetFramework.NETSTANDARD2_0 })
                       .ToArray();

                if (target.HasFlag(global::GenerateStrategiesTarget.integration_tests_windows_strategy))
                {
                    GenerateIntegrationTestsWindowsStrategy(targetFrameworks);
                }

                if (target.HasFlag(global::GenerateStrategiesTarget.integration_tests_windows_iis_strategy))
                {
                    GenerateIntegrationTestsWindowsIISStrategy(targetFrameworks);
                }
            }

            void GenerateIntegrationTestsWindowsStrategy(TargetFramework[] targetFrameworks)
            {
                var targetPlatforms = new[] { "x86", "x64" };
                var strategy = new Dictionary<string, object>();

                foreach (var framework in targetFrameworks)
                {
                    foreach (var targetPlatform in targetPlatforms)
                    {
                        strategy.Add($"{targetPlatform}_{framework}", new { framework, targetPlatform });
                    }
                }

                Logger.Info($"Integration test windows strategy");
                Logger.Info(JsonConvert.SerializeObject(strategy, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("integration_tests_windows_strategy", JsonConvert.SerializeObject(strategy, Formatting.None));
            }

            void GenerateIntegrationTestsWindowsIISStrategy(TargetFramework[] targetFrameworks)
            {
                var targetPlatforms = new[] { "x86", "x64" };

                var strategy = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var targetPlatform in targetPlatforms)
                    {
                        var enable32bit = targetPlatform == "x86";
                        strategy.Add($"{targetPlatform}_{framework}", new { framework, targetPlatform, enable32bit });
                    }
                }

                Logger.Info($"Integration test windows IIS strategy");
                Logger.Info(JsonConvert.SerializeObject(strategy, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("integration_tests_windows_iis_strategy", JsonConvert.SerializeObject(strategy, Formatting.None));
            }

            void GenerateIntegrationTestsLinux()
            {
                var targetFrameworks =
                    typeof(TargetFramework)
                       .GetFields(ReflectionService.Static)
                       .Select(x => x.GetValue(null))
                       .Cast<TargetFramework>()
                       .Except(new[] { TargetFramework.NET461, TargetFramework.NETSTANDARD2_0, })
                       .ToArray();

                var baseImages = new[] { "debian", "alpine" };

                var strategy = new Dictionary<string, object>();
                var frameworks = targetFrameworks;
                foreach (var framework in frameworks)
                {
                    if (framework == TargetFramework.NET461 || framework == TargetFramework.NETSTANDARD2_0)
                    {
                        continue;
                    }

                    foreach (var baseImage in baseImages)
                    {
                        strategy.Add($"{baseImage}_{framework}", new { publishTargetFramework = framework, baseImage });
                    }
                }

                Logger.Info($"Integration test linux strategy");
                Logger.Info(JsonConvert.SerializeObject(strategy, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("integration_tests_linux_strategy", JsonConvert.SerializeObject(strategy, Formatting.None));
            }

            void GenerateExplorationTestStrategies(GenerateStrategiesTarget target)
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

                if (target.HasFlag(global::GenerateStrategiesTarget.exploration_tests_windows_strategy))
                {
                    GenerateExplorationTestsWindowsStrategy(useCases);
                }

                if (target.HasFlag(global::GenerateStrategiesTarget.exploration_tests_linux_strategy))
                {
                    GenerateExplorationTestsLinuxStrategy(useCases);
                }
            }

            void GenerateExplorationTestsWindowsStrategy(IEnumerable<string> useCases)
            {
                var testDescriptions = ExplorationTestDescription.GetAllExplorationTestDescriptions();
                var strategy = new Dictionary<string, object>();
                foreach (var explorationTestUseCase in useCases)
                {
                    foreach (var testDescription in testDescriptions)
                    {
                        strategy.Add(
                            $"{explorationTestUseCase}_{testDescription.Name}",
                            new { explorationTestUseCase, explorationTestName = testDescription.Name });
                    }
                }

                Logger.Info($"Exploration test windows strategy");
                Logger.Info(JsonConvert.SerializeObject(strategy, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("exploration_tests_windows_strategy", JsonConvert.SerializeObject(strategy, Formatting.None));
            }

            void GenerateExplorationTestsLinuxStrategy(IEnumerable<string> useCases)
            {
                var testDescriptions = ExplorationTestDescription.GetAllExplorationTestDescriptions();

                var targetFrameworks =
                    typeof(TargetFramework)
                       .GetFields(ReflectionService.Static)
                       .Select(x => x.GetValue(null))
                       .Cast<TargetFramework>()
                       .Except(new[] { TargetFramework.NET461, TargetFramework.NETSTANDARD2_0, })
                       .ToArray();

                var baseImages = new[] { "debian", "alpine" };

                var strategy = new Dictionary<string, object>();

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
                                    strategy.Add(
                                        $"{baseImage}_{targetFramework}_{explorationTestUseCase}_{testDescription.Name}",
                                        new { baseImage, targetFramework, explorationTestUseCase, explorationTestName = testDescription.Name });
                                }
                            }
                        }
                    }
                }


                Logger.Info($"Exploration test linux strategy");
                Logger.Info(JsonConvert.SerializeObject(strategy, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("exploration_tests_linux_strategy", JsonConvert.SerializeObject(strategy, Formatting.None));
            }


        };
}

[Flags]
public enum GenerateStrategiesTarget
{
    none = 0,
    integration_tests_windows_strategy = 1 << 0,
    integration_tests_windows_iis_strategy = 1 << 1,
    integration_tests_linux_strategy = 1 << 2,
    exploration_tests_windows_strategy = 1 << 3,
    exploration_tests_linux_strategy = 1 << 4,

    integration_tests_windows = integration_tests_windows_strategy | integration_tests_windows_iis_strategy,
    exploration_tests = exploration_tests_windows_strategy | exploration_tests_linux_strategy
}
