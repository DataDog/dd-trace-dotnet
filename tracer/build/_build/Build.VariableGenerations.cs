using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.FileSystemGlobbing;
using Newtonsoft.Json;
using Nuke.Common;
using Nuke.Common.CI.AzurePipelines;
using Nuke.Common.Tools.Git;
using Nuke.Common.Tools.MSBuild;
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
                GenerateConditionVariableBasedOnGitChange("isTracerChanged", new[] { "tracer/src/Datadog.Trace/ClrProfiler/AutoInstrumentation", "tracer/src/Datadog.Tracer.Native" }, new[] { "tracer/src/Datadog.Tracer.Native/Debugger" });
                GenerateConditionVariableBasedOnGitChange("isDebuggerChanged", new[] { "tracer/src/Datadog.Tracer.Native/Debugger" }, new string[] { });
                GenerateConditionVariableBasedOnGitChange("isProfilerChanged", new[] { "profiler/src" }, new string[] { });

                void GenerateConditionVariableBasedOnGitChange(string variableName, string[] filters, string[] exclusionFilters)
                {
                    bool isChanged;
                    var forceExplorationTestsWithVariableName = $"force_exploration_tests_with_{variableName}";
                    if (bool.Parse(Environment.GetEnvironmentVariable(forceExplorationTestsWithVariableName) ?? "false"))
                    {
                        Logger.Info($"{forceExplorationTestsWithVariableName} was set - forcing exploration tests");
                        isChanged = true;
                    }
                    else
                    {
                        var changedFiles = GetGitChangedFiles("origin/master");

                        // Choose changedFiles that meet any of the filters => Choose changedFiles that DON'T meet any of the exclusion filters
                        isChanged = changedFiles.Any(s => filters.Any(filter => s.Contains(filter)) && !exclusionFilters.Any(filter => s.Contains(filter)));
                    }

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
                GenerateIntegrationTestsWindowsIISMatrix(TargetFramework.NET461);
                GenerateIntegrationTestsWindowsMsiMatrix(TargetFramework.NET461);
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

                Logger.Info($"Integration test windows IIS matrix");
                Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("integration_tests_windows_iis_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsWindowsMsiMatrix(params TargetFramework[] targetFrameworks)
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

                Logger.Info($"Integration test windows MSI matrix");
                Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                AzurePipelines.Instance.SetVariable("integration_tests_windows_msi_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
            }

            void GenerateIntegrationTestsLinuxMatrix()
            {
                var targetFrameworks = TargetFramework.GetFrameworks(except: new[] { TargetFramework.NET461, TargetFramework.NETSTANDARD2_0, });

                var baseImages = new[] { "centos7", "alpine" };

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
                var isTracerChanged = bool.Parse(EnvironmentInfo.GetVariable<string>("isTracerChanged") ?? "false");
                var isDebuggerChanged = bool.Parse(EnvironmentInfo.GetVariable<string>("isDebuggerChanged") ?? "false");
                var isProfilerChanged = bool.Parse(EnvironmentInfo.GetVariable<string>("isProfilerChanged") ?? "false");

                var useCases = new List<string>();
                if (isTracerChanged)
                {
                    useCases.Add(global::ExplorationTestUseCase.Tracer.ToString());
                }

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

                var baseImages = new[] { "centos7", "alpine" };

                var matrix = new Dictionary<string, object>();

                foreach (var baseImage in baseImages)
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
                                        new { baseImage = baseImage, publishTargetFramework = targetFramework, explorationTestUseCase = explorationTestUseCase, explorationTestName = testDescription.Name });
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
                // installer smoke tests
                GenerateLinuxInstallerSmokeTestsMatrix();
                GenerateLinuxSmokeTestsArm64Matrix();

                // nuget smoke tests
                GenerateLinuxNuGetSmokeTestsMatrix();
                GenerateLinuxNuGetSmokeTestsArm64Matrix();
                GenerateWindowsNuGetSmokeTestsMatrix();
                
                // dotnet tool smoke tests
                GenerateWindowsDotnetToolSmokeTestsMatrix();
                GenerateLinuxDotnetToolSmokeTestsMatrix();
                GenerateLinuxDotnetToolSmokeTestsArm64Matrix();

                // msi smoke tests
                GenerateWindowsMsiSmokeTestsMatrix();
                
                // tracer home smoke tests
                GenerateWindowsTracerHomeSmokeTestsMatrix();

                void GenerateLinuxInstallerSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "debian",
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
                        linuxArtifacts: "linux-packages-centos7",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "fedora",
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
                        linuxArtifacts: "linux-packages-centos7",
                        dockerName: "andrewlock/dotnet-fedora"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "alpine",
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

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "centos",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "7-6.0"),
                            (publishFramework: TargetFramework.NET5_0, "7-5.0"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "7-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "7-2.1"),
                        },
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-centos7",
                        dockerName: "andrewlock/dotnet-centos"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "rhel",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "8-6.0"),
                            (publishFramework: TargetFramework.NET5_0, "8-5.0"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "8-3.1"),
                        },
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-centos7",
                        dockerName: "andrewlock/dotnet-rhel"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "centos-stream",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "9-6.0"),
                            (publishFramework: TargetFramework.NET6_0, "8-6.0"),
                            (publishFramework: TargetFramework.NET5_0, "8-5.0"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "8-3.1"),
                        },
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-centos7",
                        dockerName: "andrewlock/dotnet-centos-stream"
                    );

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "opensuse",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "15-6.0"),
                            (publishFramework: TargetFramework.NET5_0, "15-5.0"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "15-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "15-2.1"),
                        },
                        installCmd: "rpm -Uvh ./datadog-dotnet-apm*-1.x86_64.rpm",
                        linuxArtifacts: "linux-packages-centos7",
                        dockerName: "andrewlock/dotnet-opensuse"
                    );

                    Logger.Info($"Installer smoke tests matrix");
                    Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetVariable("installer_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateLinuxSmokeTestsArm64Matrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToLinuxSmokeTestsMatrix(
                        matrix,
                        "debian",
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

                void AddToLinuxSmokeTestsMatrix(
                    Dictionary<string, object> matrix,
                    string shortName,
                    (string publishFramework, string runtimeTag)[] images,
                    string installCmd,
                    string linuxArtifacts,
                    string dockerName
                )
                {
                    foreach (var image in images)
                    {
                        var dockerTag = $"{shortName}_{image.runtimeTag.Replace('.', '_')}";
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

                void GenerateLinuxNuGetSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-focal"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bullseye-slim"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-stretch-slim"),
                        },
                        relativeProfilerPath: "datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "fedora",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "34-6.0"),
                            (publishFramework: TargetFramework.NET5_0, "33-5.0"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "35-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "29-2.1"),
                        },
                        relativeProfilerPath: "datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                        dockerName: "andrewlock/dotnet-fedora"
                    );

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.14"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.14"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12"),
                        },
                        relativeProfilerPath: "datadog/linux-musl-x64/Datadog.Trace.ClrProfiler.Native.so",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "centos",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "7-6.0"),
                            (publishFramework: TargetFramework.NET5_0, "7-5.0"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "7-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "7-2.1"),
                        },
                        relativeProfilerPath: "datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                        dockerName: "andrewlock/dotnet-centos"
                    );

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "opensuse",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "15-6.0"),
                            (publishFramework: TargetFramework.NET5_0, "15-5.0"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "15-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "15-2.1"),
                        },
                        relativeProfilerPath: "datadog/linux-x64/Datadog.Trace.ClrProfiler.Native.so",
                        dockerName: "andrewlock/dotnet-opensuse"
                    );

                    Logger.Info($"Installer smoke tests matrix");
                    Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetVariable("nuget_installer_linux_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateLinuxNuGetSmokeTestsArm64Matrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToNuGetSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-bullseye-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-focal"),
                        },
                        relativeProfilerPath: "datadog/linux-arm64/Datadog.Trace.ClrProfiler.Native.so",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    Logger.Info($"Installer smoke tests nuget matrix ARM64");
                    Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetVariable("nuget_installer_linux_smoke_tests_arm64_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void AddToNuGetSmokeTestsMatrix(
                    Dictionary<string, object> matrix,
                    string shortName,
                    (string publishFramework, string runtimeTag)[] images,
                    string relativeProfilerPath,
                    string dockerName
                )
                {
                    foreach (var image in images)
                    {
                        var dockerTag = $"{shortName}_{image.runtimeTag.Replace('.', '_')}";
                        matrix.Add(
                            dockerTag,
                            new
                            {
                                dockerTag = dockerTag,
                                publishFramework = image.publishFramework,
                                relativeProfilerPath = relativeProfilerPath,
                                runtimeImage = $"{dockerName}:{image.runtimeTag}"
                            });
                    }
                }
                
                void GenerateLinuxDotnetToolSmokeTestsMatrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-focal"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-bullseye-slim"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-stretch-slim"),
                        },
                        platformSuffix: "linux-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "fedora",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "34-6.0"),
                            (publishFramework: TargetFramework.NET5_0, "33-5.0"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "35-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "29-2.1"),
                        },
                        platformSuffix: "linux-x64",
                        dockerName: "andrewlock/dotnet-fedora"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "alpine",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "6.0-alpine3.14"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-alpine3.14"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "3.1-alpine3.14"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "2.1-alpine3.12"),
                        },
                        platformSuffix: "linux-musl-x64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "centos",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "7-6.0"),
                            (publishFramework: TargetFramework.NET5_0, "7-5.0"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "7-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "7-2.1"),
                        },
                        platformSuffix: "linux-x64",
                        dockerName: "andrewlock/dotnet-centos"
                    );

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "opensuse",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "15-6.0"),
                            (publishFramework: TargetFramework.NET5_0, "15-5.0"),
                            (publishFramework: TargetFramework.NETCOREAPP3_1, "15-3.1"),
                            (publishFramework: TargetFramework.NETCOREAPP2_1, "15-2.1"),
                        },
                        platformSuffix: "linux-x64",
                        dockerName: "andrewlock/dotnet-opensuse"
                    );

                    Logger.Info($"Installer smoke tests dotnet-tool matrix Linux");
                    Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetVariable("dotnet_tool_installer_linux_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateLinuxDotnetToolSmokeTestsArm64Matrix()
                {
                    var matrix = new Dictionary<string, object>();

                    AddToDotNetToolSmokeTestsMatrix(
                        matrix,
                        "debian",
                        new (string publishFramework, string runtimeTag)[]
                        {
                            (publishFramework: TargetFramework.NET6_0, "6.0-bullseye-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-bullseye-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-buster-slim"),
                            (publishFramework: TargetFramework.NET5_0, "5.0-focal"),
                        },
                        platformSuffix: "linux-arm64",
                        dockerName: "mcr.microsoft.com/dotnet/aspnet"
                    );

                    Logger.Info($"Installer smoke tests dotnet-tool matrix Arm64");
                    Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetVariable("dotnet_tool_installer_smoke_tests_arm64_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void AddToDotNetToolSmokeTestsMatrix(
                    Dictionary<string, object> matrix,
                    string shortName,
                    (string publishFramework, string runtimeTag)[] images,
                    string platformSuffix,
                    string dockerName
                )
                {
                    foreach (var image in images)
                    {
                        var dockerTag = $"{shortName}_{image.runtimeTag.Replace('.', '_')}";
                        matrix.Add(
                            dockerTag,
                            new
                            {
                                dockerTag = dockerTag,
                                publishFramework = image.publishFramework,
                                platformSuffix = platformSuffix,
                                runtimeImage = $"{dockerName}:{image.runtimeTag}"
                            });
                    }
                }
                
                void GenerateWindowsMsiSmokeTestsMatrix()
                {
                    var dockerName = "mcr.microsoft.com/dotnet/aspnet";

                    var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, };
                    var runtimeImages = new (string publishFramework, string runtimeTag)[]
                    {
                        (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2019"),
                        (publishFramework: TargetFramework.NET5_0, "5.0-windowsservercore-ltsc2019"),
                    };

                    var matrix = (
                                     from platform in platforms
                                     from image in runtimeImages
                                     let dockerTag = $"{platform}_{image.runtimeTag.Replace('.', '_')}"
                                     let channel32Bit = platform == MSBuildTargetPlatform.x86
                                                                       ? (image.publishFramework == TargetFramework.NET6_0 ? "6.0" : "5.0")
                                                                       : string.Empty
                                     select new
                                     {
                                         dockerTag = dockerTag,
                                         publishFramework = image.publishFramework,
                                         runtimeImage = $"{dockerName}:{image.runtimeTag}",
                                         targetPlatform = platform,
                                         channel32Bit = channel32Bit,
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Info($"Installer smoke tests MSI matrix Windows");
                    Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetVariable("msi_installer_windows_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateWindowsTracerHomeSmokeTestsMatrix()
                {
                    var dockerName = "mcr.microsoft.com/dotnet/aspnet";

                    var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, };
                    var runtimeImages = new (string publishFramework, string runtimeTag)[]
                    {
                        (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2019"),
                        (publishFramework: TargetFramework.NET5_0, "5.0-windowsservercore-ltsc2019"),
                    };

                    var matrix = (
                                     from platform in platforms
                                     from image in runtimeImages
                                     let dockerTag = $"{platform}_{image.runtimeTag.Replace('.', '_')}"
                                     let channel32Bit = platform == MSBuildTargetPlatform.x86
                                                                       ? (image.publishFramework == TargetFramework.NET6_0 ? "6.0" : "5.0")
                                                                       : string.Empty
                                     select new
                                     {
                                         relativeProfilerPath = $"win-{platform}/Datadog.Trace.ClrProfiler.Native.dll",
                                         dockerTag = dockerTag,
                                         publishFramework = image.publishFramework,
                                         runtimeImage = $"{dockerName}:{image.runtimeTag}",
                                         targetPlatform = platform,
                                         channel32Bit = channel32Bit,
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Info($"Installer smoke tests tracer-home matrix Windows");
                    Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetVariable("tracer_home_installer_windows_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }
                
                void GenerateWindowsNuGetSmokeTestsMatrix()
                {
                    var dockerName = "mcr.microsoft.com/dotnet/aspnet";

                    var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, };
                    var runtimeImages = new (string publishFramework, string runtimeTag)[]
                    {
                        (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2019"),
                        (publishFramework: TargetFramework.NET5_0, "5.0-windowsservercore-ltsc2019"),
                    };

                    var matrix = (
                                     from platform in platforms
                                     from image in runtimeImages
                                     let dockerTag = $"{platform}_{image.runtimeTag.Replace('.', '_')}"
                                     let channel32Bit = platform == MSBuildTargetPlatform.x86
                                                                       ? (image.publishFramework == TargetFramework.NET6_0 ? "6.0" : "5.0")
                                                                       : string.Empty
                                     select new
                                     {
                                         relativeProfilerPath = $"datadog/win-{platform}/Datadog.Trace.ClrProfiler.Native.dll",
                                         dockerTag = dockerTag,
                                         publishFramework = image.publishFramework,
                                         runtimeImage = $"{dockerName}:{image.runtimeTag}",
                                         targetPlatform = platform,
                                         channel32Bit = channel32Bit,
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Info($"Installer smoke tests NuGet matrix Windows");
                    Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetVariable("nuget_installer_windows_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
                }

                void GenerateWindowsDotnetToolSmokeTestsMatrix()
                {
                    var dockerName = "mcr.microsoft.com/dotnet/aspnet";

                    var platforms = new[] { MSBuildTargetPlatform.x64, MSBuildTargetPlatform.x86, };
                    var runtimeImages = new (string publishFramework, string runtimeTag)[]
                    {
                        (publishFramework: TargetFramework.NET6_0, "6.0-windowsservercore-ltsc2019"),
                        (publishFramework: TargetFramework.NET5_0, "5.0-windowsservercore-ltsc2019"),
                    };

                    var matrix = (
                                     from platform in platforms
                                     from image in runtimeImages
                                     let dockerTag = $"{platform}_{image.runtimeTag.Replace('.', '_')}"
                                     let channel32Bit = platform == MSBuildTargetPlatform.x86
                                                                       ? (image.publishFramework == TargetFramework.NET6_0 ? "6.0" : "5.0")
                                                                       : string.Empty
                                     select new
                                     {
                                         dockerTag = dockerTag,
                                         publishFramework = image.publishFramework,
                                         runtimeImage = $"{dockerName}:{image.runtimeTag}",
                                         targetPlatform = platform,
                                         channel32Bit = channel32Bit,
                                     }).ToDictionary(x=>x.dockerTag, x => x);

                    Logger.Info($"Installer smoke tests dotnet-tool matrix Windows");
                    Logger.Info(JsonConvert.SerializeObject(matrix, Formatting.Indented));
                    AzurePipelines.Instance.SetVariable("dotnet_tool_installer_windows_smoke_tests_matrix", JsonConvert.SerializeObject(matrix, Formatting.None));
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

           var gitChanges = GetGitChangedFiles(baseBranch);
           Logger.Info($"Found {gitChanges.Length} modified paths");

           var profilerStagesToSkip = GetProfilerStagesThatWillNotRun(gitChanges);
           var tracerStagesToSkip = GetTracerStagesThatWillNotRun(gitChanges);

           var message = "Based on git changes, " + (profilerStagesToSkip, tracerStagesToSkip) switch
           {
               ({ Count: 0 }, { Count: 0 }) => "profiler pipeline and tracer pipeline will both run. Skipping noop pipeline",
               ({ Count: > 0 }, { Count: 0 }) => "profiler pipeline will not run. Generating github status updates for for profiler stages",
               ({ Count: 0 }, { Count: > 0 }) => "tracer pipeline will not run. Generating github status updates for tracer stages",
               _ => "neither profiler or tracer pipelines will run. Generating github status updates for both stages",
           };

           var allStages = string.Join(";", profilerStagesToSkip.Concat(tracerStagesToSkip));

           Logger.Info(message);
           Logger.Info("Setting noop_stages: " + allStages);

           AzurePipelines.Instance.SetVariable("noop_run_skip_stages", string.IsNullOrEmpty(allStages) ? "false" : "true");
           AzurePipelines.Instance.SetVariable("noop_stages", allStages);

           List<string> GetTracerStagesThatWillNotRun(string[] gitChanges)
           {
               var tracerConfig = GetTracerPipelineDefinition();

               var tracerExcludePaths = tracerConfig.Pr?.Paths?.Exclude ?? Array.Empty<string>();
               Logger.Info($"Found {tracerExcludePaths.Length} exclude paths for the tracer");

               var willTracerPipelineRun = gitChanges.Any(
                   changed => !tracerExcludePaths.Any(prefix => changed.StartsWith(prefix)));

               return willTracerPipelineRun
                          ? new List<string>()
                          : tracerConfig.Stages.Select(x => x.Stage).ToList();
           }

           List<string> GetProfilerStagesThatWillNotRun(string[] gitChanges)
           {
               var profilerConfig = GetProfilerPipelineDefinition();

               var profilerExcludePaths = profilerConfig.On?.PullRequest?.PathsIgnore ?? Array.Empty<string>();
               Matcher profilerPathMatcher = new();
               profilerPathMatcher.AddInclude("**");
               profilerPathMatcher.AddExcludePatterns(profilerExcludePaths);

               Logger.Info($"Found {profilerExcludePaths.Length} exclude paths for the profiler");

               var willProfilerPipelineRun = profilerPathMatcher.Match(gitChanges).HasMatches;

               return willProfilerPipelineRun
                          ? new List<string>()
                          : GenerateProfilerJobsName(profilerConfig).ToList();
           }

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
       });

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
