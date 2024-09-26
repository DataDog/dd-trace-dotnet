using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.MSBuild;
using NukeExtensions;
using YamlDotNet.Serialization.NamingConventions;
using Logger = Serilog.Log;

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
                       GenerateIntegrationTestsLinuxMatrices();
                       GenerateExplorationTestMatrices();
                       GenerateSmokeTestsMatrices();
                       GenerateIntegrationTestsDebuggerArm64Matrices();
                   });

            void GenerateConditionVariables()
            {
                GenerateConditionVariableBasedOnGitChange("isAppSecChanged",
                new[] {
                    "tracer/src/Datadog.Trace/Iast",
                    "tracer/src/Datadog.Tracer.Native/iast",
                    "tracer/src/Datadog.Trace/AppSec",
                    "tracer/test/benchmarks/Benchmarks.Trace/Asm",
                    "tracer/test/benchmarks/Benchmarks.Trace/Iast",
                    "tracer/test/Datadog.Trace.Security.IntegrationTests",
                    "tracer/test/Datadog.Trace.Security.Unit.Tests",
                    "tracer/test/test-applications/security",
                }, new string[] { });
                GenerateConditionVariableBasedOnGitChange("isTracerChanged", new[] { "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation", "tracer/src/Datadog.Tracer.Native" }, new string[] {  });
                GenerateConditionVariableBasedOnGitChange("isDebuggerChanged", new[]
                {
                    "tracer/src/Datadog.Trace/Debugger",
                    "tracer/src/Datadog.Tracer.Native",
                    "tracer/test/Datadog.Trace.Debugger.IntegrationTests",
                    "tracer/test/test-applications/debugger",
                    "tracer/build/_build/Build.Steps.Debugger.cs",
                }, new string[] { });
                GenerateConditionVariableBasedOnGitChange("isProfilerChanged", new[]
                {
                    "profiler/",
                    "shared/",
                    "build/",
                    "tracer/build/_build/Build.Shared.Steps.cs",
                    "tracer/build/_build/Build.Profiler.Steps.cs",
                }, new string[] { });

                void GenerateConditionVariableBasedOnGitChange(string variableName, string[] filters, string[] exclusionFilters)
                {
                    var baseBranch = string.IsNullOrEmpty(TargetBranch) ? ReleaseBranchForCurrentVersion() : $"origin/{TargetBranch}";
                    bool isChanged;
                    var forceExplorationTestsWithVariableName = $"force_exploration_tests_with_{variableName}";

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
                        isChanged = true;
                    }
                    else
                    {
                        var changedFiles = GetGitChangedFiles(baseBranch);

                        // Choose changedFiles that meet any of the filters => Choose changedFiles that DON'T meet any of the exclusion filters
                        isChanged = changedFiles.Any(s => filters.Any(filter => s.StartsWith(filter, StringComparison.OrdinalIgnoreCase)) && !exclusionFilters.Any(filter => s.Contains(filter, StringComparison.OrdinalIgnoreCase)));
                    }

                    Logger.Information($"{variableName} - {isChanged}");

                    var variableValue = isChanged.ToString();
                    EnvironmentInfo.SetVariable(variableName, variableValue);
                    AzurePipelines.Instance.SetOutputVariable(variableName, variableValue);
                }
            }

            void GenerateIntegrationTestsWindowsMatrices()
            {
                GenerateIntegrationTestsWindowsMatrix();
                GenerateIntegrationTestsDebuggerWindowsMatrix();
                GenerateIntegrationTestsWindowsIISMatrix(TargetFramework.NET462);
                GenerateIntegrationTestsWindowsMsiMatrix(TargetFramework.NET462);
                GenerateIntegrationTestsWindowsAzureFunctionsMatrix();
            }

            void GenerateIntegrationTestsWindowsMatrix()
            {
                var targetFrameworks = TestingFrameworks;
                var targetPlatforms = new[] { "x86", "x64" };
                var matrix = new Dictionary<string, object>();

                foreach (var framework in targetFrameworks)
                {
                    foreach (var targetPlatform in targetPlatforms)
                    {
                        matrix.Add($"{targetPlatform}_{framework}", new { framework = framework, targetPlatform = targetPlatform, });
                    }
                }

                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("integration_tests_windows_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }
            void GenerateIntegrationTestsDebuggerWindowsMatrix()
            {
                var targetFrameworks = TestingFrameworksDebugger;
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
                                               optimize = optimize,
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

                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var targetPlatform in targetPlatforms)
                    {
                        var enable32bit = targetPlatform == "x86";
                        matrix.Add($"{targetPlatform}_{framework}", new { framework = framework, targetPlatform = targetPlatform, enable32bit = enable32bit });
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
                GenerateIntegrationTestsLinuxMatrix();
                GenerateIntegrationTestsLinuxArm64Matrix();
                GenerateIntegrationTestsDebuggerLinuxMatrix();
            }

            void GenerateIntegrationTestsLinuxMatrix()
            {
                var baseImages = new []
                {
                    (baseImage: "debian", artifactSuffix: "linux-x64"), 
                    (baseImage: "alpine", artifactSuffix: "linux-musl-x64"), 
                };

                var targetFrameworks = TestingFrameworks.Except(new[] { TargetFramework.NET461, TargetFramework.NET462, TargetFramework.NETSTANDARD2_0 });


                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var (baseImage, artifactSuffix) in baseImages)
                    {
                        matrix.Add($"{baseImage}_{framework}", new { publishTargetFramework = framework, baseImage = baseImage, artifactSuffix = artifactSuffix });
                    }
                }

                Logger.Information($"Integration test Linux matrix");
                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("integration_tests_linux_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsLinuxArm64Matrix()
            {
                var baseImages = new []
                {
                    (baseImage: "debian", artifactSuffix: "linux-arm64"),
                    (baseImage: "alpine", artifactSuffix: "linux-musl-arm64"),
                };

                var targetFrameworks = GetTestingFrameworks(isArm64: true).Except(new[] { TargetFramework.NET461, TargetFramework.NET462, TargetFramework.NETSTANDARD2_0 });

                var matrix = new Dictionary<string, object>();
                foreach (var framework in targetFrameworks)
                {
                    foreach (var (baseImage, artifactSuffix) in baseImages)
                    {
                        matrix.Add($"{baseImage}_{framework}", new { publishTargetFramework = framework, baseImage = baseImage, artifactSuffix = artifactSuffix });
                    }
                }

                Logger.Information($"Integration test Linux Arm64 matrix");
                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("integration_tests_linux_arm64_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsDebuggerLinuxMatrix()
            {
                var targetFrameworks = TestingFrameworksDebugger.Except(new[] { TargetFramework.NET462 });
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
                                           artifactSuffix = artifactSuffix,
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

                var useCases = new List<string>();
                if (isTracerChanged)
                {
                    useCases.Add(global::ExplorationTestUseCase.Tracer.ToString());
                }

                // Debugger exploration tests are currently all broken, so disabling
                // if (isDebuggerChanged)
                // {
                //     useCases.Add(global::ExplorationTestUseCase.Debugger.ToString());
                // }

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

                Logger.Information($"Exploration test windows matrix");
                Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetOutputVariable("exploration_tests_windows_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateExplorationTestsLinuxMatrix(IEnumerable<string> useCases)
            {
                var testDescriptions = ExplorationTestDescription.GetAllExplorationTestDescriptions();
                var targetFrameworks = TargetFramework.GetFrameworks(except: new[] { TargetFramework.NET461, TargetFramework.NET462, TargetFramework.NETSTANDARD2_0, });

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
                                        new { baseImage = baseImage, publishTargetFramework = targetFramework, explorationTestUseCase = explorationTestUseCase, explorationTestName = testDescription.Name, artifactSuffix = artifactSuffix });
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

                // msi smoke tests
                GenerateWindowsMsiSmokeTestsMatrix();

                // tracer home smoke tests
                GenerateWindowsTracerHomeSmokeTestsMatrix();
                
                // macos smoke tests
                GenerateMacosDotnetToolNugetSmokeTestsMatrix();

                void GenerateLinuxInstallerSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET8_0, "8.0-bookworm-slim"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bullseye-slim"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-buster-slim"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bionic"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-bionic"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-stretch-slim"),
                        },
                        installer: "datadog-dotnet-apm*_amd64.deb",
                        installCmd: "dpkg -i ./datadog-dotnet-apm*_amd64.deb",
                        linuxArtifacts: "linux-packages-linux-x64",
                        runtimeId: "linux-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "fedora",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET7_0, "35-7.0"),
                            new (publishFramework: TargetFramework.NET6_0, "34-6.0"),
                            new (publishFramework: TargetFramework.NET5_0, "35-5.0"),
                            new (publishFramework: TargetFramework.NET5_0, "34-5.0"),
                            new (publishFramework: TargetFramework.NET5_0, "33-5.0"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "35-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "34-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "33-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "29-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "29-2.1"),
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
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18-composite"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.14"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.14"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.13"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.13"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12"),
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
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18-composite"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.14"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.14"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.13"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.13"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12"),
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
                            new (publishFramework: TargetFramework.NET7_0, "7-7.0"),
                            new (publishFramework: TargetFramework.NET6_0, "7-6.0"),
                            new (publishFramework: TargetFramework.NET5_0, "7-5.0"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "7-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "7-2.1"),
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
                            new (publishFramework: TargetFramework.NET7_0, "8-7.0"),
                            new (publishFramework: TargetFramework.NET6_0, "8-6.0"),
                            new (publishFramework: TargetFramework.NET5_0, "8-5.0"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "8-3.1"),
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
                            // (publishFramework: TargetFramework.NET7_0, "9-7.0"), Not updated from RC1 yet
                            new (publishFramework: TargetFramework.NET6_0, "9-6.0"),
                            new (publishFramework: TargetFramework.NET6_0, "8-6.0"),
                            new (publishFramework: TargetFramework.NET5_0, "8-5.0"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "8-3.1"),
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
                            new (publishFramework: TargetFramework.NET7_0, "15-7.0"),
                            new (publishFramework: TargetFramework.NET6_0, "15-6.0"),
                            new (publishFramework: TargetFramework.NET5_0, "15-5.0"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "15-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "15-2.1"),
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
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled-composite"),
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

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET8_0, "8.0-bookworm-slim"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            // https://github.com/dotnet/runtime/issues/66707
                            new (publishFramework: TargetFramework.NET5_0, "5.0-bullseye-slim", runCrashTest: false),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim", runCrashTest: false),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal", runCrashTest: false),
                        },
                        installer: "datadog-dotnet-apm_*_arm64.deb",
                        installCmd: "dpkg -i ./datadog-dotnet-apm_*_arm64.deb",
                        linuxArtifacts: "linux-packages-linux-arm64",
                        runtimeId: "linux-arm64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "fedora",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET7_0, "35-7.0"),
                            new (publishFramework: TargetFramework.NET6_0, "34-6.0"),
                            // https://github.com/dotnet/runtime/issues/66707
                            new (publishFramework: TargetFramework.NET5_0, "35-5.0", runCrashTest: false),
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
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.19-composite"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.18"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.18"),
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
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled-composite"),
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
                                runtimeImage = $"{dockerName}:{image.RuntimeTag}"
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
                            new (publishFramework: TargetFramework.NET8_0, "8.0-bookworm-slim"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy"),
                            // (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled"), // we can't run scripts in chiseled containers, so need to update the dockerfiles
                            // (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled-composite"), // we can't run scripts in chiseled containers, so need to update the dockerfiles
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bullseye-slim"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-stretch-slim"),
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
                            new (publishFramework: TargetFramework.NET7_0, "35-7.0"),
                            new (publishFramework: TargetFramework.NET6_0, "34-6.0"),
                            new (publishFramework: TargetFramework.NET5_0, "33-5.0"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "35-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "29-2.1"),
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
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18-composite"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.14"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12"),
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
                            new (publishFramework: TargetFramework.NET7_0, "7-7.0"),
                            new (publishFramework: TargetFramework.NET6_0, "7-6.0"),
                            new (publishFramework: TargetFramework.NET5_0, "7-5.0"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "7-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "7-2.1"),
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
                            new (publishFramework: TargetFramework.NET7_0, "15-7.0"),
                            new (publishFramework: TargetFramework.NET6_0, "15-6.0"),
                            new (publishFramework: TargetFramework.NET5_0, "15-5.0"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "15-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "15-2.1"),
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

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET8_0, "8.0-bookworm-slim"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy"),
                            // (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled"), // we can't run scripts in chiseled containers, so need to update the dockerfiles
                            // (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled-composite"), // we can't run scripts in chiseled containers, so need to update the dockerfiles
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-bullseye-slim", runCrashTest: false),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim", runCrashTest: false),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal", runCrashTest: false),
                        },
                        relativeProfilerPath: "datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so",
                        relativeApiWrapperPath: "datadog/linux-arm64/Datadog.Linux.ApiWrapper.x64.so",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.19-composite"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.18"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.18"),
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
                                runtimeImage = $"{dockerName}:{image.RuntimeTag}"
                            });
                    }
                }

                void GenerateLinuxDotnetToolSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET8_0, "8.0-bookworm-slim"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy"),
                            // (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled"), // we can't run scripts in chiseled containers, so need to update the dockerfiles
                            // (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled-composite"), // we can't run scripts in chiseled containers, so need to update the dockerfiles
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bullseye-slim"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-stretch-slim"),
                        },
                        platformSuffix: "linux-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "fedora",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET7_0, "35-7.0"),
                            new (publishFramework: TargetFramework.NET6_0, "34-6.0"),
                            new (publishFramework: TargetFramework.NET5_0, "33-5.0"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "35-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "29-2.1"),
                        },
                        platformSuffix: "linux-x64",
                        dockerName: "andrewlock/dotnet-fedora"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18"), 
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.18-composite"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.14"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12"),
                        },
                        platformSuffix: "linux-musl-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "centos",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET7_0, "7-7.0"),
                            new (publishFramework: TargetFramework.NET6_0, "7-6.0"),
                            new (publishFramework: TargetFramework.NET5_0, "7-5.0"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "7-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "7-2.1"),
                        },
                        platformSuffix: "linux-x64",
                        dockerName: "andrewlock/dotnet-centos"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "opensuse",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET7_0, "15-7.0"),
                            new (publishFramework: TargetFramework.NET6_0, "15-6.0"),
                            new (publishFramework: TargetFramework.NET5_0, "15-5.0"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "15-3.1"),
                            new (publishFramework: TargetFramework.NETCOREAPP2_1, "15-2.1"),
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

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET8_0, "8.0-bookworm-slim"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy"),
                            // (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled"), // we can't run scripts in chiseled containers, so need to update the dockerfiles
                            // (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled-composite"), // we can't run scripts in chiseled containers, so need to update the dockerfiles
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-bullseye-slim", runCrashTest: false),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim", runCrashTest: false),
                            new (publishFramework: TargetFramework.NET5_0, "5.0-focal", runCrashTest: false),
                        },
                        platformSuffix: "linux-arm64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.20"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-alpine3.19-composite"),
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.18"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.18"),
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

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET8_0, "8.0-bookworm-slim"),
                            new (publishFramework: TargetFramework.NET8_0, "8.0-jammy"),
                            // (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled"), // we can't run scripts in chiseled containers, so need to update the dockerfiles
                            // (publishFramework: TargetFramework.NET8_0, "8.0-jammy-chiseled-composite"), // we can't run scripts in chiseled containers, so need to update the dockerfiles
                            new (publishFramework: TargetFramework.NET7_0, "7.0-bullseye-slim"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            // We can't install prerelease versions of the dotnet-tool nuget in .NET Core 3.1, because the --prerelease flag isn't available 
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bullseye"),
                        }.Where(x=> !IsPrerelease || x.PublishFramework != TargetFramework.NETCOREAPP3_1).ToArray(),
                        platformSuffix: "linux-x64",
                        dockerName: "mcr.microsoft.com/dotnet/sdk"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new SmokeTestImage[]
                        {
                            new (publishFramework: TargetFramework.NET7_0, "7.0-alpine3.16"),
                            new (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.16"),
                            new (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.15"),
                            // We can't install prerelease versions of the dotnet-tool nuget in .NET Core 3.1, because the --prerelease flag isn't available 
                        }.Where(x=> !IsPrerelease || x.PublishFramework != TargetFramework.NETCOREAPP3_1).ToArray(),
                        platformSuffix: "linux-musl-x64",
                        dockerName: "mcr.microsoft.com/dotnet/sdk"
                    );

                    Logger.Information($"Installer smoke tests dotnet-tool NuGet matrix Linux");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("dotnet_tool_nuget_installer_linux_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
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
                                runtimeImage = $"{dockerName}:{image.RuntimeTag}"
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
                        new (publishFramework: TargetFramework.NET8_0, "8.0-windowsservercore-ltsc2022"),
                        new (publishFramework: TargetFramework.NET7_0, "7.0-windowsservercore-ltsc2022"),
                        new (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2022"),
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
                        new (publishFramework: TargetFramework.NET8_0, "8.0-windowsservercore-ltsc2022"),
                        new (publishFramework: TargetFramework.NET7_0, "7.0-windowsservercore-ltsc2022"),
                        new (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2022"),
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
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Information($"Installer smoke tests tracer-home matrix Windows");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("tracer_home_installer_windows_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateWindowsNuGetSmokeTestsMatrix()
                {
                    var dockerName = "mcr.microsoft.com/dotnet/aspnet";

                    var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, };
                    var runtimeImages = new SmokeTestImage[]
                    {
                        new (publishFramework: TargetFramework.NET8_0, "8.0-windowsservercore-ltsc2022"),
                        new (publishFramework: TargetFramework.NET7_0, "7.0-windowsservercore-ltsc2022"),
                        new (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2022"),
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
                        new (publishFramework: TargetFramework.NET8_0, "8.0-windowsservercore-ltsc2022"),
                        new (publishFramework: TargetFramework.NET7_0, "7.0-windowsservercore-ltsc2022"),
                        new (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2022"),
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
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Information($"Installer smoke tests dotnet-tool matrix Windows");
                    Logger.Information(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetOutputVariable("dotnet_tool_installer_windows_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateMacosDotnetToolNugetSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>
                    {
                        // macos-11 environments are no longer available in Azure Devops
                        { "macos-12_netcoreapp3.1", new { vmImage = "macos-12", publishFramework = "netcoreapp3.1" } },
                        { "macos-12_net6.0", new { vmImage = "macos-12", publishFramework = "net6.0" } },
                        { "macos-12_net8.0", new { vmImage = "macos-12", publishFramework = "net8.0" } },
                        { "macos-13_netcoreapp3.1", new { vmImage = "macos-13", publishFramework = "netcoreapp3.1" } },
                        { "macos-13_net5.0", new { vmImage = "macos-13", publishFramework = "net5.0" } },
                        { "macos-13_net6.0", new { vmImage = "macos-13", publishFramework = "net6.0" } },
                        { "macos-13_net7.0", new { vmImage = "macos-13", publishFramework = "net7.0" } },
                        { "macos-13_net8.0", new { vmImage = "macos-13", publishFramework = "net8.0" } },
                        { "macos-14_netcoreapp3.1", new { vmImage = "macos-14", publishFramework = "netcoreapp3.1" } },
                        { "macos-14_net6.0", new { vmImage = "macos-14", publishFramework = "net6.0" } },
                        { "macos-14_net8.0", new { vmImage = "macos-14", publishFramework = "net8.0" } },
                    };

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
                var targetFrameworks = TestingFrameworksDebugger.Except(new[] { TargetFramework.NET462, TargetFramework.NETCOREAPP2_1, TargetFramework.NETCOREAPP3_1,  });
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

           List<string> GetTracerStagesThatWillNotRun(string[] gitChanges)
           {
               var tracerConfig = PipelineParser.GetPipelineDefinition(RootDirectory);

               var tracerExcludePaths = tracerConfig.Pr?.Paths?.Exclude ?? Array.Empty<string>();
               Logger.Information($"Found {tracerExcludePaths.Length} exclude paths for the tracer");

               var willTracerPipelineRun = gitChanges.Any(
                   changed => !tracerExcludePaths.Any(prefix => changed.StartsWith(prefix)));

               return willTracerPipelineRun
                          ? new List<string>()
                          : tracerConfig.Stages.Select(x => x.Stage).ToList();
           }
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
        public SmokeTestImage(string publishFramework, string runtimeTag, bool runCrashTest = true)
        {
            PublishFramework = publishFramework;
            RuntimeTag = runtimeTag;
            RunCrashTest = runCrashTest;
        }

        public string PublishFramework { get; init; }
        public string RuntimeTag { get; init; }
        public bool RunCrashTest { get; init; }
    }
}
