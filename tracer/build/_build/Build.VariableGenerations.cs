using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using CodeOwners;
using Microsoft.Extensions.FileSystemGlobbing;
using Newtonsoft.Json;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.MSBuild;
using NukeExtensions;
using Logger = Serilog.Log;

partial class Build : NukeBuild
{
    private const string TracerArea = "Tracer";
    private const string AsmArea = "ASM";
    private const string TracingDotnet = "@DataDog/tracing-dotnet";
    private const string ASMDotnet = "@DataDog/asm-dotnet";
    private const string DebuggerDotnet = "@DataDog/debugger-dotnet";
    private const string ProfilerDotnet = "@DataDog/profiling-dotnet";

    class ChangedTeamValue
    {
        public string VariableName { get; set; }
        public string TeamName { get; set; }
        public bool IsChanged { get; set; }
    }

    static private ChangedTeamValue[] _changedTeamValue = new ChangedTeamValue[]
    {
        new ChangedTeamValue { VariableName = "isAsmChanged", TeamName = ASMDotnet},
        new ChangedTeamValue { VariableName = "isTracerChanged", TeamName = TracingDotnet},
        new ChangedTeamValue { VariableName = "isDebuggerChanged", TeamName = DebuggerDotnet},
        new ChangedTeamValue { VariableName = "isProfilerChanged", TeamName = ProfilerDotnet},
    };

    Target GenerateVariables
        => _ =>
        {
            return _
                  .Unlisted()
                  .Executes(() =>
                   {
                       GenerateConditionVariables();
                       GenerateUnitTestFrameworkMatrices();

                       GenerateIntegrationTestsWindowsMatrices();
                       GenerateIntegrationTestsLinuxMatrices();
                       GenerateExplorationTestMatrices();
                       GenerateSmokeTestsMatrices();
                       GenerateIntegrationTestsDebuggerArm64Matrices();
                   });

            bool CommonTracerChanges(string[] changedFiles, CodeOwnersParser codeOwners)
            {
                // These folders are owned by @DataDog/tracing-dotnet but changes should not affect ASM functionality
                string[] nonCommonDirectories = new[]
                {
                    "tracer/test/",
                    "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation/",
                    "tracer/src/Datadog.Trace.", // Does not match the main tracer project
                    "tracer/src/Datadog.Trace/Agent/",
                    "tracer/src/Datadog.Trace/ContinuousProfiler/",
                    "tracer/src/Datadog.Trace/Generated/",
                    "tracer/src/Datadog.Trace/Logging/",
                    "tracer/src/Datadog.Trace/OpenTelemetry/",
                    "tracer/src/Datadog.Trace/PDBs/",
                    "tracer/src/Datadog.Trace/LibDatadog/",
                    "tracer/src/Datadog.Trace/FaultTolerant/",
                    "tracer/src/Datadog.Trace/DogStatsd/",
                };

                // Directories that are not explicitelly owned by ASM but are common to both teams
                string[] commonDirectories = new[]
{
                    "tracer/test/Datadog.Trace.TestHelpers/",
                };

                foreach (var file in changedFiles)
                {
                    if ((codeOwners.Match("/" + file)?.Owners.Contains(TracingDotnet) is true) &&
                        (commonDirectories.Any(x => file.StartsWith(x, StringComparison.OrdinalIgnoreCase)) ||
                        !nonCommonDirectories.Any(x => file.StartsWith(x, StringComparison.OrdinalIgnoreCase))
                        ))
                    {
                        Logger.Information($"File {file} was detected as common.");
                        return true;
                    }
                }

                return false;
            }

            void GenerateConditionVariables()
            {
                CodeOwnersParser codeOwners = new(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CodeOwners", "CODEOWNERS"));

                foreach(var changedTeamValue in _changedTeamValue)
                {
                    GenerateConditionVariableBasedOnGitChange(changedTeamValue, codeOwners);
                }

                void GenerateConditionVariableBasedOnGitChange(ChangedTeamValue changedTeamValue, CodeOwnersParser codeOwners)
                {
                    var baseBranch = string.IsNullOrEmpty(TargetBranch) ? ReleaseBranchForCurrentVersion() : $"origin/{TargetBranch}";
                    bool isChanged = false;
                    var forceExplorationTestsWithVariableName = $"force_run_tests_with_{changedTeamValue.VariableName}";

                    if (Environment.GetEnvironmentVariable("BUILD_REASON") == "Schedule" && bool.Parse(Environment.GetEnvironmentVariable("isMainBranch") ?? "false"))
                    {
                        Logger.Information("Running scheduled build on master, forcing all tests to run regardless of whether there has been a change.");
                        isChanged = true;
                    }
                    else if (bool.Parse(Environment.GetEnvironmentVariable(forceExplorationTestsWithVariableName) ?? "false"))
                    {
                        Logger.Information($"{forceExplorationTestsWithVariableName} was set - forcing exploration tests");
                        isChanged = true;
                    }
                    else if(IsGitBaseBranch(baseBranch))
                    {
                        // on master, treat everything as having changed
                        Logger.Information($"All tests will be launched (master branch).");
                        isChanged = true;
                    }
                    else
                    {
                        var changedFiles = GetGitChangedFiles(baseBranch);
                        // Choose changedFiles that meet any of the filters => Choose changedFiles that DON'T meet any of the exclusion filters

                        if (changedTeamValue.TeamName == ASMDotnet && CommonTracerChanges(changedFiles, codeOwners))
                        {
                            isChanged = true;
                            Logger.Information($"ASM tests will be launched based on common changes.");
                        }
                        else
                        {
                            foreach (var changedFile in changedFiles)
                            {
                                if (codeOwners.Match("/" + changedFile)?.Owners.Contains(changedTeamValue.TeamName) == true)
                                {
                                    Logger.Information($"File {changedFile} is owned by {changedTeamValue.TeamName}");
                                    isChanged = true;
                                    break;
                                }
                            }
                        }
                    }

                    Logger.Information($"{changedTeamValue.VariableName} - {isChanged}");
                    var variableValue = isChanged.ToString();
                    EnvironmentInfo.SetVariable(changedTeamValue.VariableName, variableValue);
                    AzurePipelines.Instance.SetOutputVariable(changedTeamValue.VariableName, variableValue);
                    changedTeamValue.IsChanged = isChanged;
                }
            }

            void GenerateUnitTestFrameworkMatrices()
            {
                GenerateTfmsMatrix("unit_tests_windows_matrix", GetTestingFrameworks(PlatformFamily.Windows));
                GenerateTfmsMatrix("unit_tests_macos_matrix", GetTestingFrameworks(PlatformFamily.OSX));
                GenerateLinuxMatrix("x64", GetTestingFrameworks(PlatformFamily.Linux));
                GenerateLinuxMatrix("arm64", GetTestingFrameworks(PlatformFamily.Linux, isArm64: true));

                void GenerateTfmsMatrix(string name, IEnumerable<TargetFramework> frameworks)
                {
                    var matrix = frameworks
                       .ToDictionary(t => t.ToString(), t => new { framework = t, });

                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable(name, JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateLinuxMatrix(string platform, IEnumerable<TargetFramework> frameworks)
                {
                    var matrix = new Dictionary<string, object>();

                    foreach (var framework in frameworks)
                    {
                        matrix.Add($"glibc_{framework}", new { framework = framework, baseImage = "debian", artifactSuffix = $"linux-{platform}"});
                        matrix.Add($"musl_{framework}", new { framework = framework, baseImage = "alpine", artifactSuffix = $"linux-musl-{platform}"});
                    }

                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable($"unit_tests_linux_{platform}_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }
            }

            // We only call this method for the tracer and ASM areas
            bool ShouldBeIncluded(string area)
            {
                if (area == AsmArea)
                {
                    return _changedTeamValue.First(x => x.TeamName == ASMDotnet).IsChanged;
                }

                return true;
            }

            void GenerateIntegrationTestsWindowsMatrices()
            {
                GenerateIntegrationTestsWindowsMatrix();
                GenerateIntegrationTestsDebuggerWindowsMatrix();
                GenerateIntegrationTestsWindowsIISMatrix(TargetFramework.NET48);
                GenerateIntegrationTestsWindowsMsiMatrix(TargetFramework.NET48);
                GenerateIntegrationTestsWindowsAzureFunctionsMatrix();
            }

            void GenerateIntegrationTestsWindowsMatrix()
            {
                var targetFrameworks = GetTestingFrameworks(PlatformFamily.Windows);
                var targetPlatforms = new[] { "x86", "x64" };
                var areas = new[] { TracerArea, AsmArea };
                var matrix = new Dictionary<string, object>();

                foreach (var framework in targetFrameworks)
                {
                    foreach (var targetPlatform in targetPlatforms)
                    {
                        foreach (var area in areas)
                        {
                            if (ShouldBeIncluded(area))
                            {
                                matrix.Add($"{targetPlatform}_{framework}_{area}", new { framework = framework, targetPlatform = targetPlatform, area = area });
                            }
                        }
                    }
                }

                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("integration_tests_windows_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }
            void GenerateIntegrationTestsDebuggerWindowsMatrix()
            {
                var targetFrameworks = GetTestingFrameworks(PlatformFamily.Windows);
                var targetPlatforms = new[] { "x86", "x64" };
                var debugTypes = new[] { "portable", "full" };
                var optimizations = new[] { "true", "false" };
                var matrix = new Dictionary<string, object>();

                foreach (var framework in targetFrameworks)
                {
                    foreach (var targetPlatform in targetPlatforms)
                    {
                        if (targetPlatform == "x86" && (framework.Equals(TargetFramework.NETCOREAPP3_1) || framework.Equals(TargetFramework.NET6_0)))
                        {
                            // fails on CI with error "apphost.exe" not found.
                            continue;
                        }

                        foreach (var debugType in debugTypes)
                        {
                            foreach (var optimize in optimizations)
                            {
                                matrix.Add($"{targetPlatform}_{framework}_{debugType}_{optimize}",
                                           new
                                           {
                                               framework = framework,
                                               targetPlatform = targetPlatform,
                                               debugType = debugType,
                                               optimize = optimize
                                           });
                            }
                        }
                    }
                }

                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("integration_tests_windows_debugger_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }
            void GenerateIntegrationTestsWindowsAzureFunctionsMatrix()
            {
                var combos = new []
                {
                    // new {framework = TargetFramework.NETCOREAPP3_1, runtimeInstall = v3Install, runtimeUninstall = v3Uninstall },
                    new {framework = TargetFramework.NET6_0 },
                    new {framework = TargetFramework.NET7_0 },
                    new {framework = TargetFramework.NET8_0 },
                    new {framework = TargetFramework.NET9_0 },
                    new {framework = TargetFramework.NET10_0 },
                };

                var matrix = new Dictionary<string, object>();
                foreach (var combo in combos)
                {
                    matrix.Add(combo.framework, combo);
                }

                Logger.Information($"Integration test windows azure_functions matrix");
                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("integration_tests_windows_azure_functions_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsWindowsIISMatrix(params TargetFramework[] targetFrameworks)
            {
                var targetPlatforms = new[] { "x86", "x64" };
                var areas = new[] { TracerArea, AsmArea };

                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var targetPlatform in targetPlatforms)
                    {
                        var enable32bit = targetPlatform == "x86";
                        foreach (var area in areas)
                        {
                            if (ShouldBeIncluded(area))
                            {
                                matrix.Add($"{targetPlatform}_{framework}_{area}", new { framework = framework, targetPlatform = targetPlatform, enable32bit = enable32bit, area = area });
                            }
                        }
                    }
                }

                Logger.Information($"Integration test windows IIS matrix");
                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("integration_tests_windows_iis_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsWindowsMsiMatrix(params TargetFramework[] targetFrameworks)
            {
                var targetPlatforms = new[] {
                    (targetPlaform: "x64", enable32Bit: false),
                    (targetPlaform: "x64", enable32Bit: true),
                };

                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var (targetPlatform, enable32Bit) in targetPlatforms)
                    {
                        matrix.Add($"{targetPlatform}_{(enable32Bit ? "32bit" : "64bit")}_{framework}", new { framework = framework, targetPlatform = targetPlatform, enable32bit = enable32Bit });
                    }
                }

                Logger.Information($"Integration test windows MSI matrix");
                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("integration_tests_windows_msi_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsLinuxMatrices()
            {
                GenerateIntegrationTestsLinuxMatrix(true);
                GenerateIntegrationTestsLinuxMatrix(false);
                GenerateIntegrationTestsLinuxArm64Matrix();
                GenerateIntegrationTestsDebuggerLinuxMatrix();
            }

            void GenerateIntegrationTestsLinuxMatrix(bool dockerTest)
            {
                var baseImages = new []
                {
                    (baseImage: "debian", artifactSuffix: "linux-x64"),
                    (baseImage: "alpine", artifactSuffix: "linux-musl-x64"),
                };

                var targetFrameworks = GetTestingFrameworks(PlatformFamily.Linux);

                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var (baseImage, artifactSuffix) in baseImages)
                    {
                        if (dockerTest)
                        {
                            var dockerGroups = new[] { 1, 2 };
                            foreach (var dockerGroup in dockerGroups)
                            {
                                matrix.Add($"{baseImage}_{framework}_group{dockerGroup}", new { publishTargetFramework = framework, baseImage = baseImage, artifactSuffix = artifactSuffix, dockerGroup = dockerGroup });
                            }
                        }
                        else
                        {
                            var areas = new[] { TracerArea, AsmArea };
                            foreach (var area in areas)
                            {
                                if (ShouldBeIncluded(area))
                                {
                                    matrix.Add($"{baseImage}_{framework}_{area}", new { publishTargetFramework = framework, baseImage = baseImage, artifactSuffix = artifactSuffix, area = area });
                                }
                            }
                        }
                    }
                }

                Logger.Information(dockerTest ? "Integration test Linux dockerTest matrix" : "Integration test Linux matrix");
                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                var outputVariableName = dockerTest ? "integration_tests_linux_docker_matrix" : "integration_tests_linux_matrix";
                AzurePipelines.Instance.SetOutputVariable(outputVariableName, JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsLinuxArm64Matrix()
            {
                var baseImages = new []
                {
                    (baseImage: "debian", artifactSuffix: "linux-arm64"),
                    (baseImage: "alpine", artifactSuffix: "linux-musl-arm64"),
                };

                var targetFrameworks = GetTestingFrameworks(PlatformFamily.Linux, isArm64: true);

                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var (baseImage, artifactSuffix) in baseImages)
                    {
                        if (ShouldBeIncluded(AsmArea))
                        {
                            matrix.Add($"{baseImage}_{framework}", new { publishTargetFramework = framework, baseImage = baseImage, artifactSuffix = artifactSuffix });
                        }
                        else
                        {
                            matrix.Add($"{baseImage}_{framework}", new { publishTargetFramework = framework, baseImage = baseImage, artifactSuffix = artifactSuffix, area = TracerArea });
                        }
                    }
                }

                Logger.Information($"Integration test Linux Arm64 matrix");
                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("integration_tests_linux_arm64_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsDebuggerLinuxMatrix()
            {
                var targetFrameworks = GetTestingFrameworks(PlatformFamily.Linux);
                var baseImages = new []
                {
                    (baseImage: "debian", artifactSuffix: "linux-x64"),
                    (baseImage: "alpine", artifactSuffix: "linux-musl-x64"),
                };
                var optimizations = new[] { "true", "false" };

                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var (baseImage, artifactSuffix) in baseImages)
                    {
                        foreach (var optimize in optimizations)
                        {
                            matrix.Add($"{baseImage}_{framework}_{optimize}",
                                       new
                                       {
                                           publishTargetFramework = framework,
                                           baseImage = baseImage,
                                           optimize = optimize,
                                           artifactSuffix = artifactSuffix
                                       });
                        }
                    }
                }

                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("integration_tests_linux_debugger_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateExplorationTestMatrices()
            {
                var isTracerChanged = bool.Parse(EnvironmentInfo.GetVariable<string>("isTracerChanged") ?? "false");
                var isDebuggerChanged = bool.Parse(EnvironmentInfo.GetVariable<string>("isDebuggerChanged") ?? "false");
                var isProfilerChanged = bool.Parse(EnvironmentInfo.GetVariable<string>("isProfilerChanged") ?? "false");

                var useCases = new List<global::ExplorationTestUseCase>();
                if (isTracerChanged)
                {
                    useCases.Add(global::ExplorationTestUseCase.Tracer);
                }

                if (isDebuggerChanged)
                {
                    useCases.Add(global::ExplorationTestUseCase.Debugger);
                }

                if (isProfilerChanged)
                {
                    useCases.Add(global::ExplorationTestUseCase.ContinuousProfiler);
                }

                GenerateExplorationTestsWindowsMatrix(useCases);
                GenerateExplorationTestsLinuxMatrix(useCases);
            }

            void GenerateExplorationTestsWindowsMatrix(IEnumerable<global::ExplorationTestUseCase> useCases)
            {
                var testDescriptions = ExplorationTestDescription.GetAllExplorationTestDescriptions();
                var matrix = new Dictionary<string, object>();
                foreach (var explorationTestUseCase in useCases)
                {
                    foreach (var testDescription in testDescriptions)
                    {
                        if (explorationTestUseCase == global::ExplorationTestUseCase.Debugger
                            && (testDescription.Name is global::ExplorationTestName.cake or global::ExplorationTestName.protobuf))
                        {
                            // Debugger tests are very slow on Windows only on cake and protobuf tests,
                            //  so exclude them for now, pending investigation by debugger team
                            continue;
                        }

                        matrix.Add(
                            $"{explorationTestUseCase}_{testDescription.Name.ToString()}",
                            new { explorationTestUseCase = explorationTestUseCase.ToString(), explorationTestName = testDescription.Name.ToString() });
                    }
                }

                Logger.Information($"Exploration test windows matrix");
                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("exploration_tests_windows_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateExplorationTestsLinuxMatrix(IEnumerable<global::ExplorationTestUseCase> useCases)
            {
                var testDescriptions = ExplorationTestDescription.GetAllExplorationTestDescriptions();
                var targetFrameworks = TargetFramework.GetFrameworks(except: new[] { TargetFramework.NET461, TargetFramework.NET48, TargetFramework.NETSTANDARD2_0, });

                var baseImages = new []
                {
                    (baseImage: "debian", artifactSuffix: "linux-x64"),
                    (baseImage: "alpine", artifactSuffix: "linux-musl-x64"),
                };

                var matrix = new Dictionary<string, object>();

                foreach (var (baseImage, artifactSuffix) in baseImages)
                {
                    foreach (var explorationTestUseCase in useCases)
                    {
                        foreach (var targetFramework in targetFrameworks)
                        {
                            foreach (var testDescription in testDescriptions)
                            {
                                if (testDescription.IsFrameworkSupported(targetFramework) && (testDescription.SupportedOSPlatforms is null || testDescription.SupportedOSPlatforms.Contains(OSPlatform.Linux)))
                                {
                                    matrix.Add(
                                        $"{baseImage}_{targetFramework}_{explorationTestUseCase}_{testDescription.Name}",
                                        new { baseImage = baseImage, publishTargetFramework = targetFramework, explorationTestUseCase = explorationTestUseCase.ToString(), explorationTestName = testDescription.Name, artifactSuffix = artifactSuffix });
                                }
                            }
                        }
                    }
                }

                Logger.Information($"Exploration test linux matrix");
                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("exploration_tests_linux_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateSmokeTestsMatrices()
            {
                GenerateNukeSmokeTestsMatrix();
                GenerateDdDotnetFailureTestsMatrices();
                GenerateMacosSmokeTestsMatrix();

                void GenerateNukeSmokeTestsMatrix()
                {
                    var allScenarios = SmokeTests.SmokeTestScenarios.GetAllScenarios()
                        .SelectMany(pair => pair.Value.Select(kv => (category: pair.Key, scenario: kv.Key, details: kv.Value)))
                        .Where(x => !IsPrerelease || !x.details.ExcludeWhenPrerelease)
                        .Select(x => (x.category, x.scenario, entry: (object)new
                        {
                            category = x.category.ToString(),
                            scenario = x.scenario,
                        }))
                        .ToList();

                    // Emit per-stage matrices grouped by download pattern and pool
                    EmitMatrix("smoke_x64_installer_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxX64Installer
                                              or SmokeTests.SmokeTestCategory.LinuxChiseledInstaller));

                    EmitMatrix("smoke_arm64_installer_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxArm64Installer
                                              or SmokeTests.SmokeTestCategory.LinuxChiseledArm64Installer));

                    EmitMatrix("smoke_x64_nuget_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxNuGet));

                    EmitMatrix("smoke_arm64_nuget_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxNuGetArm64));

                    EmitMatrix("smoke_x64_tool_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxDotnetTool));

                    EmitMatrix("smoke_arm64_tool_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxDotnetToolArm64));

                    EmitMatrix("smoke_x64_tool_nuget_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxDotnetToolNuget));

                    EmitMatrix("smoke_x64_trimming_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxTrimming));

                    EmitMatrix("smoke_musl_x64_installer_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxMuslInstaller));

                    EmitMatrix("smoke_musl_x64_tool_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxMuslDotnetTool));

                    EmitMatrix("smoke_musl_arm64_tool_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxMuslDotnetToolArm64));

                    EmitMatrix("smoke_musl_x64_trimming_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxMuslTrimming));

                    EmitMatrix("smoke_x64_self_instrument_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxSelfInstrument));

                    // Windows categories
                    EmitMatrix("smoke_win_msi_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.WindowsMsi));

                    EmitMatrix("smoke_win_nuget_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.WindowsNuGet));

                    EmitMatrix("smoke_win_tool_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.WindowsDotnetTool));

                    EmitMatrix("smoke_win_tracer_home_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.WindowsTracerHome));

                    EmitMatrix("smoke_win_fleet_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.WindowsFleetInstallerIis));

                    void EmitMatrix(string name, IEnumerable<(SmokeTests.SmokeTestCategory category, string scenario, object entry)> scenarios)
                    {
                        var matrix = scenarios.ToDictionary(x => x.scenario, x => x.entry);
                        Logger.Information($"Nuke smoke tests matrix: {name}");
                        Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                        AzurePipelines.Instance.SetOutputVariable(name, JsonConvert.SerializeObject(matrix, Formatting.None));
                    }
                }

                // Matrix for dd-dotnet.sh error-path tests.
                // Uses a small representative set of images (one per package format)
                // to verify dd-dotnet.sh outputs the correct error message per platform.
                // Pool info is included so a single job can target both x64 and arm64.
                void GenerateDdDotnetFailureTestsMatrices()
                {
                    var matrix = new Dictionary<string, object>
                    {
                        // x64 entries — run on Microsoft-hosted Ubuntu
                        ["debian_x64"] = new { runtimeImage = "mcr.microsoft.com/dotnet/aspnet:9.0-noble", expectedInstaller = "datadog-dotnet-apm*_amd64.deb", expectedPath = "linux-x64", poolName = "Azure Pipelines", poolVmImage = "ubuntu-latest" },
                        ["fedora_x64"] = new { runtimeImage = "andrewlock/dotnet-fedora:40-9.0", expectedInstaller = "datadog-dotnet-apm*-1.x86_64.rpm", expectedPath = "linux-x64", poolName = "Azure Pipelines", poolVmImage = "ubuntu-latest" },
                        ["alpine_musl_x64"] = new { runtimeImage = "mcr.microsoft.com/dotnet/aspnet:9.0-alpine3.20", expectedInstaller = "datadog-dotnet-apm*-musl.tar.gz", expectedPath = "linux-musl-x64", poolName = "Azure Pipelines", poolVmImage = "ubuntu-latest" },
                        // arm64 entries — run on self-hosted arm64 pool
                        ["debian_arm64"] = new { runtimeImage = "mcr.microsoft.com/dotnet/aspnet:9.0-noble", expectedInstaller = "datadog-dotnet-apm_*_arm64.deb", expectedPath = "linux-arm64", poolName = "$(linuxArm64Pool)", poolVmImage = "" },
                        ["fedora_arm64"] = new { runtimeImage = "andrewlock/dotnet-fedora-arm64:40-9.0", expectedInstaller = "datadog-dotnet-apm*-1.aarch64.rpm", expectedPath = "linux-arm64", poolName = "$(linuxArm64Pool)", poolVmImage = "" },
                        ["alpine_musl_arm64"] = new { runtimeImage = "mcr.microsoft.com/dotnet/aspnet:9.0-alpine3.20", expectedInstaller = "datadog-dotnet-apm*.arm64.tar.gz", expectedPath = "linux-musl-arm64", poolName = "$(linuxArm64Pool)", poolVmImage = "" },
                    };

                    Logger.Information("dd_dotnet failure tests matrix");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("smoke_linux_dd_dotnet_failure_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                // macOS smoke tests run natively (no Docker), so the matrix
                // provides the vmImage for pool selection and the TFM to publish.
                void GenerateMacosSmokeTestsMatrix()
                {
                    var images = new (TargetFramework PublishFramework, string VmImage)[]
                    {
                        // macos-11/12/13 environments are no longer available in Azure DevOps
                        (TargetFramework.NETCOREAPP3_1, "macos-14"),
                        (TargetFramework.NET5_0, "macos-14"),
                        (TargetFramework.NET6_0, "macos-14"),
                        (TargetFramework.NET7_0, "macos-14"),
                        (TargetFramework.NET8_0, "macos-14"),
                        (TargetFramework.NET9_0, "macos-14"),
                        (TargetFramework.NET10_0, "macos-14"),
                        (TargetFramework.NET6_0, "macos-15"),
                        (TargetFramework.NET8_0, "macos-15"),
                        (TargetFramework.NET9_0, "macos-15"),
                        (TargetFramework.NET10_0, "macos-15"),
                        (TargetFramework.NET6_0, "macOS-15-arm64"),
                        (TargetFramework.NET10_0, "macOS-15-arm64"),
                    };

                    var matrix = images.ToDictionary(
                        x => $"{x.VmImage}_{x.PublishFramework}",
                        x => (object)new
                        {
                            publishFramework = x.PublishFramework,
                            vmImage = x.VmImage,
                            smokeTestOs = "macos",
                            smokeTestOsVersion = x.VmImage.Replace("macos-", "").Replace("macOS-", "").Replace("-arm64", ""),
                        });

                    Logger.Information("macOS smoke tests matrix");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("smoke_macos_tool_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }
            }

            void GenerateIntegrationTestsDebuggerArm64Matrices()
            {
                var targetFrameworks = GetTestingFrameworks(PlatformFamily.Linux, isArm64: true);
                var baseImages = new []
                {
                    (baseImage: "debian", artifactSuffix: "linux-arm64"),
                    (baseImage: "alpine", artifactSuffix: "linux-musl-arm64"),
                };
                var optimizations = new[] { "true", "false" };

                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var (baseImage, artifactSuffix) in baseImages)
                    {
                        foreach (var optimize in optimizations)
                        {
                            matrix.Add($"{baseImage}_{framework}_{optimize}",
                                       new
                                       {
                                           publishTargetFramework = framework,
                                           baseImage = baseImage,
                                           optimize = optimize,
                                           artifactSuffix = artifactSuffix,
                                       });
                        }
                    }
                }

                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("integration_tests_arm64_debugger_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
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
           Logger.Information($"Generating variables for base branch: {baseBranch}");

           var gitChanges = GetGitChangedFiles(baseBranch);
           Logger.Information($"Found {gitChanges.Length} modified paths");

           var tracerStagesToSkip = GetTracerStagesThatWillNotRun(gitChanges);

           var message = "Based on git changes, " + tracerStagesToSkip switch
           {
               { Count: 0 } => "Azure pipeline will run. Skipping noop pipeline",
               _ => "Tracer pipelines will not run. Generating github status updates",
           };

           var allStages = string.Join(";", tracerStagesToSkip);

           Logger.Information(message);
           Logger.Information("Setting noop_stages: " + allStages);

           AzurePipelines.Instance.SetOutputVariable("noop_run_skip_stages", string.IsNullOrEmpty(allStages) ? "false" : "true");
           AzurePipelines.Instance.SetOutputVariable("noop_stages", allStages);

           static List<string> GetTracerStagesThatWillNotRun(string[] gitChanges)
           {
               var tracerConfig = PipelineParser.GetPipelineDefinition(RootDirectory);

               var tracerExcludePaths = tracerConfig.Pr?.Paths?.Exclude ?? Array.Empty<string>();
               Logger.Information($"Found {tracerExcludePaths.Length} exclude paths for the tracer");

               // Use FileSystemGlobbing to match patterns like Azure DevOps does.
               // Azure DevOps supports wildcards (*, **, ?) and is case-sensitive.
               var matcher = new Matcher(StringComparison.Ordinal);

               foreach (var excludePath in tracerExcludePaths)
               {
                   // Normalize the pattern for the matcher.
                   // Azure DevOps treats trailing slashes as "match this directory and all descendants."
                   var pattern = excludePath.EndsWith("/")
                       ? excludePath + "**"  // Match directory and all descendants
                       : excludePath;        // Match the exact file or use as-is for glob patterns

                   matcher.AddInclude(pattern);
               }

               // if all changed files are excluded, pipelines will not run
               var allFilesAreExcluded = gitChanges.All(changed => matcher.Match(changed).HasMatches);

               return allFilesAreExcluded
                          ? tracerConfig.Stages.Select(x => x.Stage).ToList()
                          : new List<string>();
           }
       });

    Target GenerateUpdateGitHubPipelineStep
        => _ => _
       .Unlisted()
       .Executes(() =>
       {
           var tracerConfig = PipelineParser.GetPipelineDefinition(RootDirectory);

           var dependsOnStages = tracerConfig
                                .Stages
                                .Where(x => !string.IsNullOrEmpty(x.Stage))
                                .Select(x => $"      - {x.Stage}");
           var dependsOn = string.Join(Environment.NewLine, dependsOnStages);

           var conditionStages = tracerConfig
                                .Stages
                                .Where(x => !string.IsNullOrEmpty(x.Stage))
                                .Select(x => $"         in(dependencies.{x.Stage}.result, 'Succeeded','SucceededWithIssues','Skipped')");
           var conditions = string.Join($",{Environment.NewLine}", conditionStages);

           var template =
               $"""
               # This file is auto-generated by Build.VariableGenerations.GenerateUpdateGitHubPipelineStep
               stages:
                 - stage: azure_pipeline_succeeded
                   dependsOn:
               {dependsOn}
                   condition: |
                     and(
               {conditions})
                   jobs:
                     - job: report_success
                       timeoutInMinutes: 60 #default value
                       pool:
                         name: azure-managed-linux-tasks
                       steps:
                         - checkout: none
                         - template: update-github-status.yml
                           parameters:
                             checkName: 'azure_pipeline_complete'
                             status: 'success'
                             description: 'Pipeline succeeded'
                 - stage: azure_pipeline_failed
                   dependsOn:
               {dependsOn}
                   condition: |
                     not(and(
               {conditions}))
                   jobs:
                     - job: report_failure
                       timeoutInMinutes: 60 #default value
                       pool:
                         name: azure-managed-linux-tasks
                       steps:
                         - checkout: none
                         - template: update-github-status.yml
                           parameters:
                             checkName: 'azure_pipeline_complete'
                             status: 'failure'
                             description: 'One or more stages failed. See associated failed Azure DevOps jobs'

               """;

           Logger.Information("Regenerated azure pipeline template {Template}", template);

           var templatePath = RootDirectory / ".azure-pipelines" / "steps" / "update-github-pipeline-status.yml";
           File.WriteAllText(templatePath, template);
           Logger.Information("Update template at {Path}", templatePath);
       });

    static bool IsGitBaseBranch(string baseBranch)
    {
        // *****
        // First we try to find the base branch in the Azure Pipelines environment variables.
        // This code is a simplified version of Datadog.Trace/CI/CIEnvironmentValues.cs file
        // to only extract the branch name from Azure Pipelines
        // *****
        const string AzureSystemPullRequestSourceBranch = "SYSTEM_PULLREQUEST_SOURCEBRANCH";
        const string AzureBuildSourceBranch = "BUILD_SOURCEBRANCH";
        const string AzureBuildSourceBranchName = "BUILD_SOURCEBRANCHNAME";

        var prBranch = Environment.GetEnvironmentVariable(AzureSystemPullRequestSourceBranch);
        var branch = !string.IsNullOrWhiteSpace(prBranch) ? prBranch : Environment.GetEnvironmentVariable(AzureBuildSourceBranch);
        if (string.IsNullOrWhiteSpace(branch))
        {
            branch = Environment.GetEnvironmentVariable(AzureBuildSourceBranchName);
        }

        Console.WriteLine("Base Branch: {0}", baseBranch);
        Console.WriteLine("Current Branch: {0}", branch);

        var cleanBranch = CleanBranchName(branch);
        var cleanBaseBranch = CleanBranchName(baseBranch);
        Console.Write("  {0} == {1}? ", cleanBranch, cleanBaseBranch);

        if (string.Equals(cleanBranch, cleanBaseBranch, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("true");
            return true;
        }

        Console.WriteLine("false");

        // *****
        // In case we cannot find the baseBranch we fallback to the git command
        // *****
        return string.Equals(
            GitTasks.Git("rev-parse --abbrev-ref HEAD").First().Text,
            baseBranch,
            StringComparison.OrdinalIgnoreCase);

        static string CleanBranchName(string branchName)
        {
            try
            {
                // Clean branch name
                var regex = new Regex(@"^refs\/heads\/tags\/(.*)|refs\/heads\/(.*)|refs\/tags\/(.*)|refs\/(.*)|origin\/tags\/(.*)|origin\/(.*)$");
                if (!string.IsNullOrEmpty(branchName))
                {
                    var match = regex.Match(branchName);
                    if (match.Success && match.Groups.Count == 7)
                    {
                        branchName =
                            !string.IsNullOrWhiteSpace(match.Groups[2].Value) ? match.Groups[2].Value :
                            !string.IsNullOrWhiteSpace(match.Groups[4].Value) ? match.Groups[4].Value :
                                                                                match.Groups[6].Value;
                    }
                }
            }
            catch
            {
                // .
            }

            return branchName;
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
}
