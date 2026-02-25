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
                // Temporary nuke-based smoke tests
                GenerateNukeSmokeTestsMatrix();

                void GenerateNukeSmokeTestsMatrix()
                {
                    var allScenarios = SmokeTests.SmokeTestScenarios.GetAllScenarios()
                        .SelectMany(pair => pair.Value.Select(kv => (category: pair.Key, scenario: kv.Key, details: kv.Value)))
                        .Where(x => !IsPrerelease || !x.details.ExcludeWhenPrerelease)
                        .Select(x => (x.category, x.scenario, x.details, entry: (object)new
                        {
                            category = x.category.ToString(),
                            scenario = x.scenario,
                            runtimeId = x.details.RuntimeId,
                            relativeProfilerPath = x.details.RelativeProfilerPath,
                            relativeApiWrapperPath = x.details.RelativeApiWrapperPath,
                            packageName = x.details.PackageName,
                            packageVersionSuffix = x.details.PackageVersionSuffix,
                            runCrashTest = x.details.RunCrashTest ? "true" : "false",
                            publishFramework = x.details.PublishFramework,
                            runtimeImage = x.details.RuntimeImage,
                            smokeTestOs = x.details.Os,
                            smokeTestOsVersion = x.details.OsVersion,
                        }))
                        .ToList();

                    // Emit per-stage matrices grouped by download pattern and pool
                    EmitMatrix("nuke_installer_x64_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxX64Installer
                                              or SmokeTests.SmokeTestCategory.LinuxChiseledInstaller));

                    EmitMatrix("nuke_installer_arm64_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxArm64Installer
                                              or SmokeTests.SmokeTestCategory.LinuxChiseledArm64Installer));

                    EmitMatrix("nuke_nuget_x64_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxNuGet));

                    EmitMatrix("nuke_nuget_arm64_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxNuGetArm64));

                    EmitMatrix("nuke_dotnet_tool_x64_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxDotnetTool));

                    EmitMatrix("nuke_dotnet_tool_arm64_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxDotnetToolArm64));

                    EmitMatrix("nuke_dotnet_tool_nuget_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxDotnetToolNuget));

                    EmitMatrix("nuke_trimming_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxTrimming));

                    EmitMatrix("nuke_installer_musl_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxMuslInstaller));

                    EmitMatrix("nuke_trimming_musl_matrix",
                        allScenarios.Where(x => x.category is SmokeTests.SmokeTestCategory.LinuxMuslTrimming));

                    void EmitMatrix(string name, IEnumerable<(SmokeTests.SmokeTestCategory category, string scenario, SmokeTests.SmokeTestScenario details, object entry)> scenarios)
                    {
                        var matrix = scenarios.ToDictionary(x => x.scenario, x => x.entry);
                        Logger.Information($"Nuke smoke tests matrix: {name}");
                        Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                        AzurePipelines.Instance.SetOutputVariable(name, JsonConvert.SerializeObject(matrix, Formatting.None));
                    }
                }

                // installer smoke tests
                GenerateLinuxInstallerSmokeTestsMatrix();
                GenerateLinuxSmokeTestsArm64Matrix();
                GenerateLinuxChiseledInstallerSmokeTestsMatrix();
                GenerateLinuxChiseledInstallerArm64SmokeTestsMatrix();

                // nuget smoke tests
                GenerateLinuxNuGetSmokeTestsMatrix();
                GenerateLinuxNuGetSmokeTestsArm64Matrix();
                GenerateWindowsNuGetSmokeTestsMatrix();

                // dotnet tool smoke tests
                GenerateWindowsDotnetToolSmokeTestsMatrix();
                GenerateLinuxDotnetToolSmokeTestsMatrix();
                GenerateLinuxDotnetToolSmokeTestsArm64Matrix();

                GenerateLinuxDotnetToolNugetSmokeTestsMatrix();

                // Trimming tests
                GenerateLinuxTrimmingSmokeTestsMatrix();

                // msi smoke tests
                GenerateWindowsMsiSmokeTestsMatrix();

                // tracer home / fleet installer smoke tests
                GenerateWindowsTracerHomeSmokeTestsMatrix();
                GenerateWindowsFleetInstallerIisSmokeTestsMatrix();
                GenerateWindowsFleetInstallerSmokeTestsMatrix();

                // macos smoke tests
                GenerateMacosDotnetToolNugetSmokeTestsMatrix();

                void GenerateLinuxInstallerSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
                        "ubuntu",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-bookworm-slim", "debian", "bookworm"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy", "ubuntu", "jammy"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim", "debian", "buster"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal", "ubuntu", "focal"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bionic", "ubuntu", "bionic"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-stretch-slim", "debian", "stretch"),
                        },
                        installer: "datadog-dotnet-apm*_amd64.deb",
                        installCmd: "dpkg -i ./datadog-dotnet-apm*_amd64.deb",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    // Non-lts versions of ubuntu (official Microsoft versions only provide LTS-based images)
                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "ubuntu_interim",
                        new SmokeTestImage[]
                        {
                            // new (publishFramework: TargetFramework.NET10_0, "25.10-10.0", "ubuntu", "questing"),
                            new (publishFramework: TargetFramework.NET9_0, "25.04-9.0", "ubuntu", "plucky"),
                        },
                        installer: "datadog-dotnet-apm*_amd64.deb",
                        installCmd: "dpkg -i ./datadog-dotnet-apm*_amd64.deb",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "andrewlock/dotnet-ubuntu"
                    );

                    // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "trixie-10.0", "debian", "trixie"),
                            new (publishFramework: TargetFramework.NET9_0, "trixie-9.0", "debian", "trixie"),
                            new (publishFramework: TargetFramework.NET8_0, "trixie-8.0", "debian", "trixie"),
                        },
                        installer: "datadog-dotnet-apm*_amd64.deb",
                        installCmd: "dpkg -i ./datadog-dotnet-apm*_amd64.deb",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "andrewlock/dotnet-debian"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "fedora",
                        new SmokeTestImage[]
                        {
                            // new (publishFramework: TargetFramework.NET10_0, "42-10.0", "fedora", "42"),
                            new (publishFramework: TargetFramework.NET9_0, "40-9.0", "fedora", "40"),
                            new (publishFramework: TargetFramework.NET7_0, "35-7.0", "fedora", "35"),
                            new (publishFramework: TargetFramework.NET6_0, "34-6.0", "fedora", "34"),
                            new (publishFramework: TargetFramework.NET5_0, "35-5.0", "fedora", "35"),
                            new (publishFramework: TargetFramework.NET5_0, "34-5.0", "fedora", "34"),
                            new (publishFramework: TargetFramework.NET5_0, "33-5.0", "fedora", "33"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "35-3.1", "fedora", "35"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "34-3.1", "fedora", "34"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "33-3.1", "fedora", "33"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "29-3.1", "fedora", "29"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "29-2.1", "fedora", "29"),
                        },
                        installer: "datadog-dotnet-apm*-1.x86_64.rpm",
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "andrewlock/dotnet-fedora"
                    );

                    // Alpine tests with the default package
                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22-composite", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20-composite", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18-composite", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.16", "alpine", "3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.14", "alpine", "3.14"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.13", "alpine", "3.13"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14", "alpine", "3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12", "alpine", "3.12"),
                        },
                        // currently we direct customers to the musl-specific package in the command line.
                        // Should we update this to point to the default artifact instead?
                        installer: "datadog-dotnet-apm*-musl.tar.gz", // used by the dd-dotnet checks to direct customers to the right place
                        installCmd: "tar -C /opt/datadog -xzf ./datadog-dotnet-apm*.tar.gz",
                        linuxArtifacts: "linux-packages-linux-x64", // these are what we download
                        runtimeId: "linux-musl-x64", // used by the dd-dotnet checks to direct customers to the right place
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    // Alpine tests with the musl-specific package
                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "alpine_musl",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22-composite", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20-composite", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18-composite", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.16", "alpine", "3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.14", "alpine", "3.14"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.13", "alpine", "3.13"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14", "alpine", "3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12", "alpine", "3.12"),
                        },
                        installer: "datadog-dotnet-apm*-musl.tar.gz",
                        installCmd: "tar -C /opt/datadog -xzf ./datadog-dotnet-apm*-musl.tar.gz",
                        linuxArtifacts: "linux-packages-linux-musl-x64",
                        runtimeId: "linux-musl-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "centos",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET7_0, "7-7.0", "centos", "7"),
                            new (publishFramework: TargetFramework.NET6_0, "7-6.0", "centos", "7"),
                            new (publishFramework: TargetFramework.NET5_0, "7-5.0", "centos", "7"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "7-3.1", "centos", "7"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "7-2.1", "centos", "7"),
                        },
                        installer: "datadog-dotnet-apm*-1.x86_64.rpm",
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "andrewlock/dotnet-centos"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "rhel",
                        new SmokeTestImage[]
                        {
                            // new (publishFramework: TargetFramework.NET10_0, "10-10.0", "rhel", "10"),
                            new (publishFramework: TargetFramework.NET9_0, "9-9.0", "rhel", "9"),
                            new (publishFramework: TargetFramework.NET9_0, "8-9.0", "rhel", "8"),
                            new (publishFramework: TargetFramework.NET7_0, "8-7.0", "rhel", "8"),
                            new (publishFramework: TargetFramework.NET6_0, "8-6.0", "rhel", "8"),
                            new (publishFramework: TargetFramework.NET5_0, "8-5.0", "rhel", "8"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "8-3.1", "rhel", "8"),
                        },
                        installer: "datadog-dotnet-apm*-1.x86_64.rpm",
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "andrewlock/dotnet-rhel"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "centos-stream",
                        new SmokeTestImage[]
                        {
                            // new (publishFramework: TargetFramework.NET10_0, "10-10.0", "centos-stream", "10"),
                            new (publishFramework: TargetFramework.NET9_0, "9-9.0", "centos-stream", "9"),
                            new (publishFramework: TargetFramework.NET6_0, "9-6.0", "centos-stream", "9"),
                            new (publishFramework: TargetFramework.NET6_0, "8-6.0", "centos-stream", "8"),
                            new (publishFramework: TargetFramework.NET5_0, "8-5.0", "centos-stream", "8"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "8-3.1", "centos-stream", "8"),
                        },
                        installer: "datadog-dotnet-apm*-1.x86_64.rpm",
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "andrewlock/dotnet-centos-stream"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "opensuse",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "15-10.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET9_0, "15-9.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET7_0, "15-7.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET6_0, "15-6.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET5_0, "15-5.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "15-3.1", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "15-2.1", "opensuse", "15"),
                        },
                        installer: "datadog-dotnet-apm*-1.x86_64.rpm",
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "andrewlock/dotnet-opensuse"
                    );

                    Logger.Information($"Installer smoke tests matrix");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("installer_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateLinuxChiseledInstallerSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble-chiseled", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble-chiseled-composite", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble-chiseled", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble-chiseled-composite", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled", "ubuntu", "jammy"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled-composite", "ubuntu", "jammy"),
                        },
                        installer: null,
                        installCmd: null,
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    Logger.Information($"Installer chiseled smoke tests matrix");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("installer_chiseled_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateLinuxSmokeTestsArm64Matrix()
                {
                    var matrix = new Dictionary<string, object>();

                    // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "ubuntu",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-bookworm-slim", "debian", "bookworm"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim", "debian", "bullseye"),
                            // https://github.com/dotnet/runtime/issues/66707
                            new (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim", "debian", "buster", runCrashTest: false),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal", "ubuntu", "focal", runCrashTest: false),
                        },
                        installer: "datadog-dotnet-apm_*_arm64.deb",
                        installCmd: "dpkg -i ./datadog-dotnet-apm_*_arm64.deb",
                        linuxArtifacts: "linux-packages-linux-arm64",
                        runtimeId: "linux-arm64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    // Non-lts versions of ubuntu (official Microsoft versions only provide LTS-based images)
                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "ubuntu_interim",
                        new SmokeTestImage[]
                        {
                            // new (publishFramework: TargetFramework.NET10_0, "25.10-10.0", "ubuntu", "questing"),
                            new (publishFramework: TargetFramework.NET9_0, "25.04-9.0", "ubuntu", "plucky"),
                        },
                        installer: "datadog-dotnet-apm_*_arm64.deb",
                        installCmd: "dpkg -i ./datadog-dotnet-apm_*_arm64.deb",
                        linuxArtifacts: "linux-packages-linux-arm64",
                        runtimeId: "linux-arm64",
                        dockerName: "andrewlock/dotnet-ubuntu"
                    );

                    // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new SmokeTestImage[]
                        {
                            // new (publishFramework: TargetFramework.NET10_0, "trixie-10.0", "debian", "trixie"),
                            new (publishFramework: TargetFramework.NET9_0, "trixie-9.0", "debian", "trixie"),
                            new (publishFramework: TargetFramework.NET8_0, "trixie-8.0", "debian", "trixie"),
                        },
                        installer: "datadog-dotnet-apm_*_arm64.deb",
                        installCmd: "dpkg -i ./datadog-dotnet-apm_*_arm64.deb",
                        linuxArtifacts: "linux-packages-linux-arm64",
                        runtimeId: "linux-arm64",
                        dockerName: "andrewlock/dotnet-debian"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "fedora",
                        new SmokeTestImage[]
                        {
                            // new (publishFramework: TargetFramework.NET10_0, "42-10.0", "fedora", "42"),
                            new (publishFramework: TargetFramework.NET9_0, "40-9.0", "fedora", "40"),
                            new (publishFramework: TargetFramework.NET7_0, "35-7.0", "fedora", "35"),
                            new (publishFramework: TargetFramework.NET6_0, "34-6.0", "fedora", "34"),
                            // https://github.com/dotnet/runtime/issues/66707
                            new (publishFramework: TargetFramework.NET5_0, "35-5.0", "fedora", "35", runCrashTest: false),
                        },
                        installer: "datadog-dotnet-apm*-1.aarch64.rpm",
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.aarch64.rpm",
                        linuxArtifacts: "linux-packages-linux-arm64",
                        runtimeId: "linux-arm64",
                        dockerName: "andrewlock/dotnet-fedora-arm64"
                    );

                    // Alpine tests with the default package
                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22-composite", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.20", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.19-composite", "alpine", "3.19"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.18", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.18", "alpine", "3.18"),
                            // runtimes on earlier alpine versions aren't provided by MS
                        },
                        // currently we direct customers to the musl-specific package in the command line.
                        // Should we update this to point to the default artifact instead?
                        installer: "datadog-dotnet-apm*.arm64.tar.gz", // used by the dd-dotnet checks to direct customers to the right place
                        installCmd: "tar -C /opt/datadog -xzf ./datadog-dotnet-apm*.tar.gz",
                        linuxArtifacts: "linux-packages-linux-arm64", // these are what we download
                        runtimeId: "linux-musl-arm64", // used by the dd-dotnet checks to direct customers to the right place
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    Logger.Information($"Installer smoke tests matrix");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("installer_smoke_tests_arm64_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateLinuxChiseledInstallerArm64SmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble-chiseled", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble-chiseled-composite", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble-chiseled", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble-chiseled-composite", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled", "ubuntu", "jammy"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled-composite", "ubuntu", "jammy"),
                        },
                        installer: null,
                        installCmd: null,
                        linuxArtifacts: "linux-packages-linux-arm64",
                        runtimeId: "linux-arm64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    Logger.Information($"Installer chiseled smoke tests arm64 matrix");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("installer_chiseled_smoke_tests_arm64_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void AddToLinuxSmokeTestsMatrix(
                    Dictionary<string, object> matrix,
                    string shortName,
                    SmokeTestImage[] images,
                    string installer,
                    string installCmd,
                    string linuxArtifacts,
                    string runtimeId,
                    string dockerName
                )
                {
                    foreach (var image in images)
                    {
                        var dockerTag = $"{shortName}_{image.RuntimeTag.Replace('.', '_')}";
                        matrix.Add(
                            dockerTag,
                            new
                            {
                                expectedInstaller = installer,
                                expectedPath = runtimeId,
                                installCmd = installCmd,
                                dockerTag = dockerTag,
                                publishFramework = image.PublishFramework,
                                linuxArtifacts = linuxArtifacts,
                                runCrashTest = image.RunCrashTest ? "true" : "false",
                                runtimeImage = $"{dockerName}:{image.RuntimeTag}",
                                smokeTestOs = image.Os,
                                smokeTestOsVersion = image.OsVersion,
                            });
                    }
                }

                void GenerateLinuxNuGetSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-bookworm-slim", "debian", "bookworm"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy", "ubuntu", "jammy"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal", "ubuntu", "focal"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-stretch-slim", "debian", "stretch"),
                        },
                        relativeProfilerPath: "datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                        relativeApiWrapperPath: "datadog/linux-x64/Datadog.Linux.ApiWrapper.x64.so",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "fedora",
                        new SmokeTestImage[]
                        {
                            // new (publishFramework: TargetFramework.NET10_0, "42-10.0", "fedora", "42"),
                            new (publishFramework: TargetFramework.NET9_0, "40-9.0", "fedora", "40"),
                            new (publishFramework: TargetFramework.NET7_0, "35-7.0", "fedora", "35"),
                            new (publishFramework: TargetFramework.NET6_0, "34-6.0", "fedora", "34"),
                            new (publishFramework: TargetFramework.NET5_0, "33-5.0", "fedora", "33"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "35-3.1", "fedora", "35"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "29-2.1", "fedora", "29"),
                        },
                        relativeProfilerPath: "datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                        relativeApiWrapperPath: "datadog/linux-x64/Datadog.Linux.ApiWrapper.x64.so",
                        dockerName: "andrewlock/dotnet-fedora"
                    );

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22-composite", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20-composite", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18-composite", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.16", "alpine", "3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.14", "alpine", "3.14"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.14", "alpine", "3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14", "alpine", "3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12", "alpine", "3.12"),
                        },
                        relativeProfilerPath: "datadog/linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so",
                        relativeApiWrapperPath: "datadog/linux-musl-x64/Datadog.Linux.ApiWrapper.x64.so",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "centos",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET7_0, "7-7.0", "centos", "7"),
                            new (publishFramework: TargetFramework.NET6_0, "7-6.0", "centos", "7"),
                            new (publishFramework: TargetFramework.NET5_0, "7-5.0", "centos", "7"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "7-3.1", "centos", "7"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "7-2.1", "centos", "7"),
                        },
                        relativeProfilerPath: "datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                        relativeApiWrapperPath: "datadog/linux-x64/Datadog.Linux.ApiWrapper.x64.so",
                        dockerName: "andrewlock/dotnet-centos"
                    );

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "opensuse",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "15-10.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET9_0, "15-9.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET7_0, "15-7.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET6_0, "15-6.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET5_0, "15-5.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "15-3.1", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "15-2.1", "opensuse", "15"),
                        },
                        relativeProfilerPath: "datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                        relativeApiWrapperPath: "datadog/linux-x64/Datadog.Linux.ApiWrapper.x64.so",
                        dockerName: "andrewlock/dotnet-opensuse"
                    );

                    Logger.Information($"Installer smoke tests matrix");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("nuget_installer_linux_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateLinuxNuGetSmokeTestsArm64Matrix()
                {
                    var matrix = new Dictionary<string, object>();

                    // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "ubuntu",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-bookworm-slim", "debian", "bookworm"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-bookworm-slim", "debian", "bookworm"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy", "ubuntu", "jammy"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim", "debian", "buster", runCrashTest: false),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal", "ubuntu", "focal", runCrashTest: false),
                        },
                        relativeProfilerPath: "datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so",
                        relativeApiWrapperPath: "datadog/linux-arm64/Datadog.Linux.ApiWrapper.x64.so",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
                    AddToNuGetSmokeTestsMatrix(
                       matrix,
                       "debian",
                       new SmokeTestImage[]
                       {
                        //    new (publishFramework: TargetFramework.NET10_0, "trixie-10.0", "debian", "trixie"),
                       },
                       relativeProfilerPath: "datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so",
                       relativeApiWrapperPath: "datadog/linux-arm64/Datadog.Linux.ApiWrapper.x64.so",
                       dockerName: "andrewlock/dotnet-debian"
                    );

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22-composite", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20-composite", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.20", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.19-composite", "alpine", "3.19"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.18", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.18", "alpine", "3.18"),
                        },
                        relativeProfilerPath: "datadog/linux-musl-arm64/Datadog.Trace.ClrProfiler.Native.so",
                        relativeApiWrapperPath: "datadog/linux-musl-arm64/Datadog.Linux.ApiWrapper.x64.so",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );


                    Logger.Information($"Installer smoke tests nuget matrix ARM64");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("nuget_installer_linux_smoke_tests_arm64_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void AddToNuGetSmokeTestsMatrix(
                    Dictionary<string, object> matrix,
                    string shortName,
                    SmokeTestImage[] images,
                    string relativeProfilerPath,
                    string relativeApiWrapperPath,
                    string dockerName
                )
                {
                    foreach (var image in images)
                    {
                        var dockerTag = $"{shortName}_{image.RuntimeTag.Replace('.', '_')}";
                        matrix.Add(
                            dockerTag,
                            new
                            {
                                dockerTag = dockerTag,
                                publishFramework = image.PublishFramework,
                                relativeProfilerPath = relativeProfilerPath,
                                relativeApiWrapperPath = relativeApiWrapperPath,
                                runCrashTest = image.RunCrashTest ? "true" : "false",
                                runtimeImage = $"{dockerName}:{image.RuntimeTag}",
                                smokeTestOs = image.Os,
                                smokeTestOsVersion = image.OsVersion,
                            });
                    }
                }

                void GenerateLinuxDotnetToolSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "ubuntu",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-bookworm-slim", "debian", "bookworm"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy", "ubuntu", "jammy"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal", "ubuntu", "focal"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-stretch-slim", "debian", "stretch"),
                        },
                        platformSuffix: "linux-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
                    AddToDotNetToolSmokeTestsMatrix(
                       matrix,
                       "debian",
                       new SmokeTestImage[]
                       {
                           new (publishFramework: TargetFramework.NET10_0, "trixie-10.0", "debian", "trixie"),
                       },
                       platformSuffix: "linux-x64",
                       dockerName: "andrewlock/dotnet-debian"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "fedora",
                        new SmokeTestImage[]
                        {
                            // new (publishFramework: TargetFramework.NET10_0, "42-10.0", "fedora", "42"),
                            new (publishFramework: TargetFramework.NET9_0, "40-9.0", "fedora", "40"),
                            new (publishFramework: TargetFramework.NET7_0, "35-7.0", "fedora", "35"),
                            new (publishFramework: TargetFramework.NET6_0, "34-6.0", "fedora", "34"),
                            new (publishFramework: TargetFramework.NET5_0, "33-5.0", "fedora", "33"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "35-3.1", "fedora", "35"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "29-2.1", "fedora", "29"),
                        },
                        platformSuffix: "linux-x64",
                        dockerName: "andrewlock/dotnet-fedora"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22-composite", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20-composite", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18-composite", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.16", "alpine", "3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.14", "alpine", "3.14"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.14", "alpine", "3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14", "alpine", "3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12", "alpine", "3.12"),
                        },
                        platformSuffix: "linux-musl-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "centos",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET7_0, "7-7.0", "centos", "7"),
                            new (publishFramework: TargetFramework.NET6_0, "7-6.0", "centos", "7"),
                            new (publishFramework: TargetFramework.NET5_0, "7-5.0", "centos", "7"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "7-3.1", "centos", "7"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "7-2.1", "centos", "7"),
                        },
                        platformSuffix: "linux-x64",
                        dockerName: "andrewlock/dotnet-centos"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "opensuse",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "15-10.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET9_0, "15-9.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET7_0, "15-7.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET6_0, "15-6.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET5_0, "15-5.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "15-3.1", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "15-2.1", "opensuse", "15"),
                        },
                        platformSuffix: "linux-x64",
                        dockerName: "andrewlock/dotnet-opensuse"
                    );

                    Logger.Information($"Installer smoke tests dotnet-tool matrix Linux");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("dotnet_tool_installer_linux_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateLinuxDotnetToolSmokeTestsArm64Matrix()
                {
                    var matrix = new Dictionary<string, object>();

                    // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "ubuntu",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-bookworm-slim", "debian", "bookworm"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy", "ubuntu", "jammy"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim", "debian", "buster", runCrashTest: false),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal", "ubuntu", "focal", runCrashTest: false),
                        },
                        platformSuffix: "linux-arm64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
                    AddToDotNetToolSmokeTestsMatrix(
                       matrix,
                       "debian",
                       new SmokeTestImage[]
                       {
                        //    new (publishFramework: TargetFramework.NET10_0, "trixie-10.0", "debian", "trixie"),
                       },
                       platformSuffix: "linux-arm64",
                       dockerName: "andrewlock/dotnet-debian"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22-composite", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20-composite", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.19", "alpine", "3.19"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.19-composite", "alpine", "3.19"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.18", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.18", "alpine", "3.18"),
                        },
                        platformSuffix: "linux-musl-arm64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    Logger.Information($"Installer smoke tests dotnet-tool matrix Arm64");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("dotnet_tool_installer_smoke_tests_arm64_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateLinuxDotnetToolNugetSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "ubuntu",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-bookworm-slim", "debian", "bookworm"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy", "ubuntu", "jammy"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim", "debian", "bullseye"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim", "debian", "bullseye"),
                            // We can't install prerelease versions of the dotnet-tool nuget in .NET Core 3.1, because the --prerelease flag isn't available
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bullseye", "debian", "bullseye"),
                        }.Where(x=> !IsPrerelease || x.PublishFramework != TargetFramework.NETCOREAPP3_1).ToArray(),
                        platformSuffix: "linux-x64",
                        dockerName: "mcr.microsoft.com/dotnet/sdk"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.16", "alpine", "3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.16", "alpine", "3.16"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.15", "alpine", "3.15"),
                            // We can't install prerelease versions of the dotnet-tool nuget in .NET Core 3.1, because the --prerelease flag isn't available
                        }.Where(x=> !IsPrerelease || x.PublishFramework != TargetFramework.NETCOREAPP3_1).ToArray(),
                        platformSuffix: "linux-musl-x64",
                        dockerName: "mcr.microsoft.com/dotnet/sdk"
                    );

                    Logger.Information($"Installer smoke tests dotnet-tool NuGet matrix Linux");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("dotnet_tool_nuget_installer_linux_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateLinuxTrimmingSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    // This is actually a mix of ubuntu and debian, but they're all in the same MS repository
                    AddToLinuxTrimmingSmokeTestsMatrix(
                        matrix,
                        "ubuntu",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-noble", "ubuntu", "noble"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-bookworm-slim", "debian", "bookworm"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy", "ubuntu", "jammy"),
                        },
                        installer: "datadog-dotnet-apm*_amd64.deb",
                        installCmd: "dpkg -i ./datadog-dotnet-apm*_amd64.deb",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    // Microsoft stopped pushing debian tags in .NET 10, so using separate repo
                    AddToLinuxTrimmingSmokeTestsMatrix(
                       matrix,
                       "debian",
                       new SmokeTestImage[]
                       {
                           new (publishFramework: TargetFramework.NET10_0, "trixie-10.0", "debian", "trixie"),
                       },
                       installer: "datadog-dotnet-apm*_amd64.deb",
                       installCmd: "dpkg -i ./datadog-dotnet-apm*_amd64.deb",
                       linuxArtifacts: "linux-packages-linux-x64",
                       runtimeId: "linux-x64",
                       dockerName: "andrewlock/dotnet-debian"
                    );

                    // Alpine tests with the musl-specific package
                    AddToLinuxTrimmingSmokeTestsMatrix(
                        matrix,
                        "alpine_musl",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET10_0, "10.0-alpine3.22-composite", "alpine", "3.22"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET9_0, "9.0-alpine3.20-composite", "alpine", "3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18", "alpine", "3.18"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18-composite", "alpine", "3.18"),
                        },
                        installer: "datadog-dotnet-apm*-musl.tar.gz",
                        installCmd: "tar -C /opt/datadog -xzf ./datadog-dotnet-apm*-musl.tar.gz",
                        linuxArtifacts: "linux-packages-linux-musl-x64",
                        runtimeId: "linux-musl-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToLinuxTrimmingSmokeTestsMatrix(
                        matrix,
                        "rhel",
                        new SmokeTestImage[]
                        {
                            // new (publishFramework: TargetFramework.NET10_0, "10-10.0", "rhel", "10"),
                            new (publishFramework: TargetFramework.NET9_0, "9-9.0", "rhel", "9"),
                            new (publishFramework: TargetFramework.NET9_0, "8-9.0", "rhel", "8"),
                        },
                        installer: "datadog-dotnet-apm*-1.x86_64.rpm",
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "andrewlock/dotnet-rhel"
                    );

                    AddToLinuxTrimmingSmokeTestsMatrix(
                        matrix,
                        "opensuse",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET10_0, "15-10.0", "opensuse", "15"),
                            new (publishFramework: TargetFramework.NET9_0, "15-9.0", "opensuse", "15"),
                        },
                        installer: "datadog-dotnet-apm*-1.x86_64.rpm",
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "andrewlock/dotnet-opensuse"
                    );

                    Logger.Information($"Trimming installer smoke tests matrix");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("trimming_installer_linux_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));

                    void AddToLinuxTrimmingSmokeTestsMatrix(
                        Dictionary<string, object> matrix,
                        string shortName,
                        SmokeTestImage[] images,
                        string installer,
                        string installCmd,
                        string linuxArtifacts,
                        string runtimeId,
                        string dockerName
                    )
                    {
                        var packages = new[]
                        {
                            (name: "Datadog.Trace", suffix: string.Empty, shortName: "ddtrace"),
                            (name: "Datadog.Trace.Trimming", suffix: "-prerelease", shortName: "ddtrace_trimming")
                        };
                        var pairs = from image in images
                                    from package in packages
                                    select (image, package);

                        foreach (var pair in pairs)
                        {
                            var image = pair.image;
                            var dockerTag = $"{pair.package.shortName}_{shortName}_{image.RuntimeTag.Replace('.', '_')}";
                            matrix.Add(
                                dockerTag,
                                new
                                {
                                    expectedInstaller = installer,
                                    expectedPath = runtimeId,
                                    installCmd = installCmd,
                                    dockerTag = dockerTag,
                                    publishFramework = image.PublishFramework,
                                    linuxArtifacts = linuxArtifacts,
                                    runCrashTest = "false", // this doesn't work on Linux
                                    runtimeImage = $"{dockerName}:{image.RuntimeTag}",
                                    runtimeId = runtimeId,
                                    packageName = pair.package.name,
                                    packageVersionSuffix = pair.package.suffix,
                                    smokeTestOs = image.Os,
                                    smokeTestOsVersion = image.OsVersion,
                                });
                        }
                    }
                }

                void AddToDotNetToolSmokeTestsMatrix(
                    Dictionary<string, object> matrix,
                    string shortName,
                    SmokeTestImage[] images,
                    string platformSuffix,
                    string dockerName
                )
                {
                    foreach (var image in images)
                    {
                        var dockerTag = $"{shortName}_{image.RuntimeTag.Replace('.', '_')}";
                        matrix.Add(
                            dockerTag,
                            new
                            {
                                dockerTag = dockerTag,
                                publishFramework = image.PublishFramework,
                                platformSuffix = platformSuffix,
                                runCrashTest = image.RunCrashTest ? "true" : "false",
                                runtimeImage = $"{dockerName}:{image.RuntimeTag}",
                                smokeTestOs = image.Os,
                                smokeTestOsVersion = image.OsVersion,
                            });
                    }
                }

                void GenerateWindowsMsiSmokeTestsMatrix()
                {
                    var dockerName = "mcr.microsoft.com/dotnet/aspnet";

                    var platforms = new(MSBuildTargetPlatform platform, bool enable32Bit)[] {
                        (MSBuildTargetPlatform.x64, false),
                        (MSBuildTargetPlatform.x64, true),
                    };
                    var runtimeImages = new SmokeTestImage[]
                    {
                        new (publishFramework: TargetFramework.NET10_0, "10.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET9_0, "9.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET8_0, "8.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET7_0, "7.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                    };

                    var matrix = (
                                     from platform in platforms
                                     from image in runtimeImages
                                     let dockerTag = $"{platform.platform}_{image.RuntimeTag.Replace('.', '_')}_{(platform.enable32Bit ? "32bit" : "64bit")}"
                                     let channel32Bit = platform.enable32Bit
                                                                       ? GetInstallerChannel(image.PublishFramework)
                                                                       : string.Empty
                                     select new
                                     {
                                         dockerTag = dockerTag,
                                         publishFramework = image.PublishFramework,
                                         runtimeImage = $"{dockerName}:{image.RuntimeTag}",
                                         targetPlatform = platform.platform,
                                         channel32Bit = channel32Bit,
                                         smokeTestOs = image.Os,
                                         smokeTestOsVersion = image.OsVersion,
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Information($"Installer smoke tests MSI matrix Windows");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("msi_installer_windows_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateWindowsTracerHomeSmokeTestsMatrix()
                {
                    var dockerName = "mcr.microsoft.com/dotnet/aspnet";

                    var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, };
                    var runtimeImages = new SmokeTestImage[]
                    {
                        new (publishFramework: TargetFramework.NET10_0, "10.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET9_0, "9.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET8_0, "8.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET7_0, "7.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                    };

                    var matrix = (
                                     from platform in platforms
                                     from image in runtimeImages
                                     let dockerTag = $"{platform}_{image.RuntimeTag.Replace('.', '_')}"
                                     let channel32Bit = platform == MSBuildTargetPlatform.x86
                                                                       ? GetInstallerChannel(image.PublishFramework)
                                                                       : string.Empty
                                     select new
                                     {
                                         relativeProfilerPath = $"win-{platform}/Datadog.Trace.ClrProfiler.Native.dll",
                                         dockerTag = dockerTag,
                                         publishFramework = image.PublishFramework,
                                         runtimeImage = $"{dockerName}:{image.RuntimeTag}",
                                         targetPlatform = platform,
                                         channel32Bit = channel32Bit,
                                         smokeTestOs = image.Os,
                                         smokeTestOsVersion = image.OsVersion,
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Information($"Installer smoke tests tracer-home matrix Windows");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("tracer_home_installer_windows_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateWindowsFleetInstallerIisSmokeTestsMatrix()
                {
                    var dockerName = "mcr.microsoft.com/dotnet/framework/aspnet";

                    var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, };
                    var runtimeImages = new SmokeTestImage[]
                    {
                        // We can only test Windows 2022 images currently, due to VM + docker image support
                        new (publishFramework: TargetFramework.NET9_0, "4.8-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET8_0, "4.8-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                    };

                    var matrix = (
                                     from platform in platforms
                                     from image in runtimeImages
                                     from globalInstall in new[] { false } // global install isn't currently supported
                                     let installCommand = globalInstall ? "enable-global-instrumentation" : "enable-iis-instrumentation"
                                     let dockerTag = $"{image.PublishFramework}_{platform}_{image.RuntimeTag}_{(globalInstall ? "global" : "iis")}".Replace('.', '_')
                                     select new
                                     {
                                         dockerTag = dockerTag,
                                         publishFramework = image.PublishFramework,
                                         runtimeImage = $"{dockerName}:{image.RuntimeTag}",
                                         targetPlatform = platform,
                                         channel = GetInstallerChannel(image.PublishFramework),
                                         installCommand = installCommand,
                                         smokeTestOs = image.Os,
                                         smokeTestOsVersion = image.OsVersion,
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Information($"Installer smoke tests fleet-installer iis matrix Windows");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("fleet_installer_windows_iis_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateWindowsFleetInstallerSmokeTestsMatrix()
                {
                    var dockerName = "mcr.microsoft.com/dotnet/aspnet";

                    var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, };
                    var runtimeImages = new SmokeTestImage[]
                    {
                        new (publishFramework: TargetFramework.NET10_0, "10.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET9_0, "9.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET8_0, "8.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                    };

                    var matrix = (
                                     from platform in platforms
                                     from image in runtimeImages
                                     let dockerTag = $"{image.PublishFramework}_{platform}_{image.RuntimeTag}".Replace('.', '_')
                                     let channel32Bit = platform == MSBuildTargetPlatform.x86
                                                            ? GetInstallerChannel(image.PublishFramework)
                                                            : string.Empty
                                     select new
                                     {
                                         dockerTag = dockerTag,
                                         publishFramework = image.PublishFramework,
                                         runtimeImage = $"{dockerName}:{image.RuntimeTag}",
                                         channel32Bit = channel32Bit,
                                         smokeTestOs = image.Os,
                                         smokeTestOsVersion = image.OsVersion,
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Information($"Installer smoke tests fleet-installer matrix Windows");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("fleet_installer_windows_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateWindowsNuGetSmokeTestsMatrix()
                {
                    var dockerName = "mcr.microsoft.com/dotnet/aspnet";

                    var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, };
                    var runtimeImages = new SmokeTestImage[]
                    {
                        new (publishFramework: TargetFramework.NET10_0, "10.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET9_0, "9.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET8_0, "8.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET7_0, "7.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                    };

                    var matrix = (
                                     from platform in platforms
                                     from image in runtimeImages
                                     let dockerTag = $"{platform}_{image.RuntimeTag.Replace('.', '_')}"
                                     let channel32Bit = platform == MSBuildTargetPlatform.x86
                                                                       ? GetInstallerChannel(image.PublishFramework)
                                                                       : string.Empty
                                     select new
                                     {
                                         relativeProfilerPath = $"datadog/win-{platform}/Datadog.Trace.ClrProfiler.Native.dll",
                                         dockerTag = dockerTag,
                                         publishFramework = image.PublishFramework,
                                         runtimeImage = $"{dockerName}:{image.RuntimeTag}",
                                         targetPlatform = platform,
                                         channel32Bit = channel32Bit,
                                         smokeTestOs = image.Os,
                                         smokeTestOsVersion = image.OsVersion,
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Information($"Installer smoke tests NuGet matrix Windows");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("nuget_installer_windows_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateWindowsDotnetToolSmokeTestsMatrix()
                {
                    var dockerName = "mcr.microsoft.com/dotnet/aspnet";

                    var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, };
                    var runtimeImages = new SmokeTestImage[]
                    {
                        new (publishFramework: TargetFramework.NET10_0, "10.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET9_0, "9.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET8_0, "8.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET7_0, "7.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                        new (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2022", "windows", "servercore-2022"),
                    };

                    var matrix = (
                                     from platform in platforms
                                     from image in runtimeImages
                                     let dockerTag = $"{platform}_{image.RuntimeTag.Replace('.', '_')}"
                                     let channel32Bit = platform == MSBuildTargetPlatform.x86
                                                                       ? GetInstallerChannel(image.PublishFramework)
                                                                       : string.Empty
                                     select new
                                     {
                                         dockerTag = dockerTag,
                                         publishFramework = image.PublishFramework,
                                         runtimeImage = $"{dockerName}:{image.RuntimeTag}",
                                         targetPlatform = platform,
                                         channel32Bit = channel32Bit,
                                         smokeTestOs = image.Os,
                                         smokeTestOsVersion = image.OsVersion,
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Information($"Installer smoke tests dotnet-tool matrix Windows");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("dotnet_tool_installer_windows_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateMacosDotnetToolNugetSmokeTestsMatrix()
                {
                    var images = new SmokeTestImage[]
                    {
                        // macos-11/12/13 environments are no longer available in Azure Devops
                        new (publishFramework: TargetFramework.NETCOREAPP3_1, "macos-14", "macos", "14"),
                        new (publishFramework: TargetFramework.NET5_0, "macos-14", "macos", "14"),
                        new (publishFramework: TargetFramework.NET6_0, "macos-14", "macos", "14"),
                        new (publishFramework: TargetFramework.NET7_0, "macos-14", "macos", "14"),
                        new (publishFramework: TargetFramework.NET8_0, "macos-14", "macos", "14"),
                        new (publishFramework: TargetFramework.NET9_0, "macos-14", "macos", "14"),
                        new (publishFramework: TargetFramework.NET10_0, "macos-14", "macos", "14"),
                        new (publishFramework: TargetFramework.NET6_0, "macos-15", "macos", "15"),
                        new (publishFramework: TargetFramework.NET8_0, "macos-15", "macos", "15"),
                        new (publishFramework: TargetFramework.NET9_0, "macos-15", "macos", "15"),
                        new (publishFramework: TargetFramework.NET10_0, "macos-15", "macos", "15"),
                        new (publishFramework: TargetFramework.NET6_0, "macOS-15-arm64", "macos", "15"),
                        new (publishFramework: TargetFramework.NET10_0, "macOS-15-arm64", "macos", "15"),
                    };

                    var matrix = images.ToDictionary(
                        x => $"{x.RuntimeTag}_{x.PublishFramework}",
                        x => new
                    {
                        publishFramework = x.PublishFramework,
                        vmImage = x.RuntimeTag,
                        smokeTestOs = x.Os,
                        smokeTestOsVersion = x.OsVersion,
                    });
                    Logger.Information($"Installer smoke tests dotnet-tool NuGet matrix MacOs");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("dotnet_tool_nuget_installer_macos_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                static string GetInstallerChannel(string publishFramework) =>
                    publishFramework.Replace("netcoreapp", string.Empty)
                                    .Replace("net", string.Empty);
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

    class SmokeTestImage
    {
        public SmokeTestImage(string publishFramework, string runtimeTag, string os, string osVersion, bool runCrashTest = true)
        {
            PublishFramework = publishFramework;
            RuntimeTag = runtimeTag;
            RunCrashTest = runCrashTest;
            Os = os;
            OsVersion = osVersion;
        }

        public string PublishFramework { get; init; }
        public string RuntimeTag { get; init; }
        public bool RunCrashTest { get; init; }
        public string Os { get; init; }
        public string OsVersion { get; init; }
    }
}
