using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.Threading.Tasks;
using Docker.DotNet.Models;
using Nuke.Common.IO;
using Logger = Serilog.Log;
using static SmokeTests.Helpers;

namespace SmokeTests;

public static partial class SmokeTestRunner
{
    static class Builder
    {
        const string LinuxTestAgentImage = "ghcr.io/datadog/dd-apm-test-agent/ddapm-test-agent:latest";
        const string WindowsTestAgentImage = "dd-trace-dotnet/ddapm-test-agent-windows";
        const string WindowsTestAgentDockerfile = "build/_build/docker/test-agent.windows.dockerfile";

        /// <summary>
        /// Builds all Docker images for the given scenario. Returns the list of image tags to test.
        /// Most categories produce a single image; chiseled categories produce two (one per entrypoint style).
        /// Windows categories also build the test agent
        /// </summary>
        public static async Task<string[]> BuildImageAsync(
            SmokeTestScenario scenario,
            AbsolutePath tracerDir,
            AbsolutePath artifactsDir,
            string toolVersion,
            string dotnetSdkVersion)
        {
            LogSection($"Building image: {scenario.ShortName}");
            Logger.Information("Artifacts: {ArtifactsDir}", artifactsDir);

            return scenario switch
            {
                InstallerScenario s => await BuildInstallerImageAsync(s, tracerDir, artifactsDir, dotnetSdkVersion),
                ChiseledScenario s => await BuildChiseledImageAsync(s, tracerDir, artifactsDir, dotnetSdkVersion),
                NuGetScenario s => await BuildNuGetImageAsync(s, tracerDir, artifactsDir, toolVersion, dotnetSdkVersion),
                DotnetToolScenario s => await BuildDotnetToolImageAsync(s, tracerDir, artifactsDir, dotnetSdkVersion),
                DotnetToolNugetScenario s => await BuildDotnetToolNugetImageAsync(s, tracerDir, artifactsDir, toolVersion, dotnetSdkVersion),
                SelfInstrumentScenario s => await BuildSelfInstrumentImageAsync(s, tracerDir, artifactsDir, dotnetSdkVersion),
                TrimmingScenario s => await BuildTrimmingImageAsync(s, tracerDir, artifactsDir, toolVersion, dotnetSdkVersion),
                WindowsMsiScenario s => await BuildWindowsMsiImageAsync(s, tracerDir, artifactsDir, dotnetSdkVersion),
                WindowsNuGetScenario s => await BuildWindowsNuGetImageAsync(s, tracerDir, artifactsDir, toolVersion, dotnetSdkVersion),
                WindowsDotnetToolScenario s => await BuildWindowsDotnetToolImageAsync(s, tracerDir, artifactsDir, dotnetSdkVersion),
                WindowsTracerHomeScenario s => await BuildWindowsTracerHomeImageAsync(s, tracerDir, artifactsDir, dotnetSdkVersion),
                WindowsFleetInstallerIisScenario s => await BuildWindowsFleetInstallerIisImageAsync(s, tracerDir, artifactsDir, dotnetSdkVersion),
                _ => throw new InvalidOperationException($"Unknown smoke test scenario type: {scenario.GetType().Name}"),
            };

            static async Task<string[]> BuildInstallerImageAsync(InstallerScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, string dotnetSdkVersion)
            {
                const string dockerfilePath = "build/_build/docker/smoke.dockerfile";

                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["INSTALL_CMD"] = scenario.InstallCommand,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);

                return new[] {scenario.DockerTag};
            }

            static async Task<string[]> BuildChiseledImageAsync(ChiseledScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, string dotnetSdkVersion)
            {
                const string dockerfilePath = "build/_build/docker/smoke.chiseled.dockerfile";

                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                };

                // Build the "manual" env-var-based entrypoint image
                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir, target: "installer-final");

                // Build the dd-dotnet entrypoint image (reuses cached layers through installer-base)
                var ddDotnetTarget = scenario.IsArm64
                    ? "dd-dotnet-final-linux-arm64"
                    : "dd-dotnet-final-linux-x64";

                var ddDotnetTag = scenario.DockerTag + "-dd-dotnet";
                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, ddDotnetTag, buildArgs, artifactsDir, target: ddDotnetTarget);

                return new[] {scenario.DockerTag, ddDotnetTag};
            }

            static async Task<string[]> BuildNuGetImageAsync(NuGetScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, string toolVersion, string dotnetSdkVersion)
            {
                const string dockerfilePath = "build/_build/docker/smoke.nuget.dockerfile";

                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["TOOL_VERSION"] = toolVersion,
                    ["RELATIVE_PROFILER_PATH"] = scenario.RelativeProfilerPath,
                    ["RELATIVE_APIWRAPPER_PATH"] = scenario.RelativeApiWrapperPath,
                    ["NUGET_PACKAGE"] = scenario.NuGetPackageName,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
                return new[] {scenario.DockerTag};
            }

            static async Task<string[]> BuildDotnetToolImageAsync(DotnetToolScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, string dotnetSdkVersion)
            {
                const string dockerfilePath = "build/_build/docker/smoke.dotnet-tool.dockerfile";

                var installCmd = $"./datadog-dotnet-apm-*/{scenario.RuntimeId}/createLogPath.sh && cp -r ./datadog-dotnet-apm-*/{scenario.RuntimeId} /opt/datadog";

                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["INSTALL_CMD"] = installCmd,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
                return new[] {scenario.DockerTag};
            }

            static async Task<string[]> BuildDotnetToolNugetImageAsync(
                DotnetToolNugetScenario scenario,
                AbsolutePath tracerDir,
                AbsolutePath artifactsDir,
                string toolVersion,
                string dotnetSdkVersion)
            {
                const string dockerfilePath = "build/_build/docker/smoke.dotnet-tool.nuget.dockerfile";

                var installCmd = $"./datadog-dotnet-apm-*/{scenario.RuntimeId}/createLogPath.sh && cp -r ./datadog-dotnet-apm-*/{scenario.RuntimeId} /opt/datadog";

                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["INSTALL_CMD"] = installCmd,
                    ["TOOL_VERSION"] = toolVersion,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
                return new[] {scenario.DockerTag};
            }

            static async Task<string[]> BuildSelfInstrumentImageAsync(SelfInstrumentScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, string dotnetSdkVersion)
            {
                const string dockerfilePath = "build/_build/docker/smoke.dotnet-tool.self-instrument.dockerfile";

                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["INSTALL_CMD"] = scenario.InstallCommand,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
                return new[] {scenario.DockerTag};
            }

            static async Task<string[]> BuildTrimmingImageAsync(
                TrimmingScenario scenario,
                AbsolutePath tracerDir,
                AbsolutePath artifactsDir,
                string toolVersion,
                string dotnetSdkVersion)
            {
                const string dockerfilePath = "build/_build/docker/smoke.trimming.dockerfile";

                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["INSTALL_CMD"] = scenario.InstallCommand,
                    ["TOOL_VERSION"] = toolVersion + (scenario.PackageVersionSuffix ?? ""),
                    ["PACKAGE_NAME"] = scenario.PackageName,
                    ["RUNTIME_IDENTIFIER"] = scenario.RuntimeId,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);

                return new[] {scenario.DockerTag};
            }

            static async Task<string[]> BuildWindowsMsiImageAsync(WindowsMsiScenario scenario, AbsolutePath tracerDir, AbsolutePath artifactsDir, string dotnetSdkVersion)
            {
                // The Dockerfile expects the MSI file to be named "datadog-apm.msi"
                // Rename any *.msi file in the artifacts directory
                RenameArtifact(artifactsDir, "*.msi", "datadog-apm.msi");

                // Build the standard MSI image
                const string dockerfilePath = "build/_build/docker/smoke.windows.dockerfile";
                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["CHANNEL_32_BIT"] = scenario.Channel32Bit,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);

                // Build the dd-dotnet variant (always uses no 32-bit)
                const string ddDotnetDockerfilePath = "build/_build/docker/smoke.windows.dd-dotnet.dockerfile";
                var ddDotnetTag = scenario.DockerTag + "-dd-dotnet";
                var ddDotnetBuildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["CHANNEL_32_BIT"] = "",
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, ddDotnetDockerfilePath, ddDotnetTag, ddDotnetBuildArgs, artifactsDir);

                return new[] {scenario.DockerTag, ddDotnetTag};
            }

            static async Task<string[]> BuildWindowsNuGetImageAsync(
                WindowsNuGetScenario scenario,
                AbsolutePath tracerDir,
                AbsolutePath artifactsDir,
                string toolVersion,
                string dotnetSdkVersion)
            {
                // Build the standard NuGet image
                const string dockerfilePath = "build/_build/docker/smoke.windows.nuget.dockerfile";
                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["TOOL_VERSION"] = toolVersion,
                    ["CHANNEL_32_BIT"] = scenario.Channel32Bit,
                    ["RELATIVE_PROFILER_PATH"] = scenario.RelativeProfilerPath,
                    ["NUGET_PACKAGE"] = scenario.NuGetPackageName,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);

                // Build the dd-dotnet NuGet variant
                const string ddDotnetDockerfilePath = "build/_build/docker/smoke.windows.nuget.dd-dotnet.dockerfile";
                var ddDotnetTag = scenario.DockerTag + "-dd-dotnet";
                var ddDotnetBuildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["TOOL_VERSION"] = toolVersion,
                    ["CHANNEL_32_BIT"] = scenario.Channel32Bit,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, ddDotnetDockerfilePath, ddDotnetTag, ddDotnetBuildArgs, artifactsDir);

                return new[] {scenario.DockerTag, ddDotnetTag};
            }

            static async Task<string[]> BuildWindowsDotnetToolImageAsync(
                WindowsDotnetToolScenario scenario,
                AbsolutePath tracerDir,
                AbsolutePath artifactsDir,
                string dotnetSdkVersion)
            {
                // The Dockerfile expects "dd-trace-win.zip"
                RenameArtifact(artifactsDir, "dd-trace-win-*.zip", "dd-trace-win.zip");

                const string dockerfilePath = "build/_build/docker/smoke.windows.dotnet-tool.dockerfile";

                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["CHANNEL_32_BIT"] = scenario.Channel32Bit,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
                return new[] {scenario.DockerTag};
            }

            static async Task<string[]> BuildWindowsTracerHomeImageAsync(
                WindowsTracerHomeScenario scenario,
                AbsolutePath tracerDir,
                AbsolutePath artifactsDir,
                string dotnetSdkVersion)
            {
                const string dockerfilePath = "build/_build/docker/smoke.windows.tracer-home.dockerfile";

                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["CHANNEL_32_BIT"] = scenario.Channel32Bit,
                    ["RELATIVE_PROFILER_PATH"] = scenario.RelativeProfilerPath,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
                return new[] {scenario.DockerTag};
            }

            static async Task<string[]> BuildWindowsFleetInstallerIisImageAsync(
                WindowsFleetInstallerIisScenario scenario,
                AbsolutePath tracerDir,
                AbsolutePath artifactsDir,
                string dotnetSdkVersion)
            {
                const string dockerfilePath = "build/_build/docker/smoke.windows.iis.fleet-installer.dockerfile";

                var channel = scenario.PublishFramework
                    .Replace("netcoreapp", string.Empty)
                    .Replace("net", string.Empty);

                var buildArgs = new Dictionary<string, string>
                {
                    ["DOTNETSDK_VERSION"] = dotnetSdkVersion,
                    ["RUNTIME_IMAGE"] = scenario.RuntimeImage,
                    ["PUBLISH_FRAMEWORK"] = scenario.PublishFramework,
                    ["CHANNEL"] = channel,
                    ["TARGET_PLATFORM"] = scenario.TargetPlatform,
                    ["INSTALL_COMMAND"] = scenario.FleetInstallerCommand,
                };

                await DockerService.BuildImageFromDockerfileAsync(tracerDir, dockerfilePath, scenario.DockerTag, buildArgs, artifactsDir);
                return new[] {scenario.DockerTag};
            }
        }

        /// <summary>
        /// Builds or pulls the required test agent, and returns the name of the image created.
        /// </summary>
        public static async Task<string> BuildTestAgentImageAsync(SmokeTestScenario scenario, AbsolutePath tracerDir)
        {
            if (scenario.IsWindows)
            {
                await BuildWindowsTestAgentImageAsync(tracerDir);
                return WindowsTestAgentImage;
            }
            else
            {
                await DockerService.PullImageAsync(LinuxTestAgentImage, skipIfImageExists: true);
                return LinuxTestAgentImage;
            }
            static async Task BuildWindowsTestAgentImageAsync(AbsolutePath tracerDir)
            {
                var buildArgs = new Dictionary<string, string>();
                await DockerService.BuildImageFromDockerfileAsync(tracerDir, WindowsTestAgentDockerfile, WindowsTestAgentImage, buildArgs, null);
            }
        }

        /// <summary>
        /// Renames the first file matching <paramref name="searchPattern"/> in <paramref name="directory"/>
        /// to <paramref name="targetName"/>. Used to normalize artifact file names before Docker builds.
        /// </summary>
        static void RenameArtifact(AbsolutePath directory, string searchPattern, string targetName)
        {
            if (directory is null || !Directory.Exists(directory))
            {
                return;
            }

            var targetPath = Path.Combine(directory, targetName);
            if (File.Exists(targetPath))
            {
                Logger.Debug("Artifact {Target} already exists, skipping rename", targetPath);
                return;
            }

            var files = Directory.GetFiles(directory, searchPattern);
            if (files.Length == 0)
            {
                Logger.Warning("No files matching {Pattern} found in {Dir}", searchPattern, directory);
                return;
            }

            Logger.Information("Renaming {Source} -> {Target}", Path.GetFileName(files[0]), targetName);
            File.Move(files[0], targetPath);
        }
    }
}
