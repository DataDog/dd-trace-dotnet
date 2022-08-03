// <copyright file="SetAllVersions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PrepareRelease
{
    public abstract class SetAllVersions
    {
        protected SetAllVersions(string tracerDirectory, string tracerVersion, bool isPrerelease)
        {
            TracerDirectory = tracerDirectory;
            TracerVersion = new Version(tracerVersion);
            IsPrerelease = isPrerelease;
        }

        /// <summary>
        /// Gets the tracer directory, where src/build/samples/test subdirectories can be found.
        /// </summary>
        public string TracerDirectory { get; }

        /// <summary>
        /// Gets the current tracer version.
        /// This is the single source of truth for the current tracer version.
        /// When changing the tracer version, update this value and <see cref="IsPrerelease" />,
        /// then run the "PrepareRelease" tool to update the entire solution.
        /// </summary>
        public Version TracerVersion { get; }

        /// <summary>
        /// Gets a value indicating whether the current tracer version is a prerelease.
        /// </summary>
        public bool IsPrerelease { get; }

        public abstract void Run();

        private string FunctionCallReplace(string text, string functionName)
        {
            const string split = ", ";
            var pattern = @$"{functionName}\({VersionPattern(split, fourPartVersion: true)}\)";
            var replacement = $"{functionName}({FourPartVersionString(split)})";

            return Regex.Replace(text, pattern, replacement, RegexOptions.Singleline);
        }

        private string FourPartVersionReplace(string text)
        {
            return Regex.Replace(text, VersionPattern(fourPartVersion: true), FourPartVersionString(), RegexOptions.Singleline);
        }

        private string FullVersionReplace(string text, string split, string prefix = "")
        {
            return Regex.Replace(text, prefix + VersionPattern(split), prefix + VersionString(split), RegexOptions.Singleline);
        }

        private string FullAssemblyNameReplace(string text)
        {
            return Regex.Replace(text, AssemblyString(VersionPattern()), AssemblyString(VersionString()), RegexOptions.Singleline);
        }

        private string DatadogTraceNugetDependencyVersionReplace(string text)
        {
            return Regex.Replace(text, $"<PackageReference Include=\"Datadog.Trace\" Version=\"{VersionPattern(withPrereleasePostfix: true)}\" />", $"<PackageReference Include=\"Datadog.Trace\" Version=\"{VersionString(withPrereleasePostfix: true)}\" />", RegexOptions.Singleline);
        }

        private string NugetVersionReplace(string text)
        {
            return Regex.Replace(text, $"<Version>{VersionPattern(withPrereleasePostfix: true)}</Version>", $"<Version>{VersionString(withPrereleasePostfix: true)}</Version>", RegexOptions.Singleline);
        }

        private string PropsVersionReplace(string text)
        {
            return Regex.Replace(text, $"<ProductVersion>{VersionPattern(withPrereleasePostfix: false)}</ProductVersion>", $"<ProductVersion>{VersionString(withPrereleasePostfix: false)}</ProductVersion>", RegexOptions.Singleline);
        }

        private string WixProjReplace(string text)
        {
            text = Regex.Replace(
                text,
                $"<OutputName>datadog-dotnet-apm-{VersionPattern(withPrereleasePostfix: true)}-\\$\\(Platform\\)</OutputName>",
                $"<OutputName>datadog-dotnet-apm-{VersionString(withPrereleasePostfix: true)}-$(Platform)</OutputName>",
                RegexOptions.Singleline);

            text = Regex.Replace(
                text,
                $"InstallerVersion={VersionPattern()}",
                $"InstallerVersion={VersionString()}",
                RegexOptions.Singleline);

            return text;
        }

        private void SynchronizeVersion(string path, Func<string, string> transform)
        {
            var fullPath = Path.Combine(TracerDirectory, path);

            Console.WriteLine($"Updating version instances for {path}");

            if (!File.Exists(fullPath))
            {
                throw new Exception($"File not found to version: {path}");
            }

            var fileContent = File.ReadAllText(fullPath);
            var newFileContent = transform(fileContent);

            File.WriteAllText(fullPath, newFileContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private string FourPartVersionString(string split = ".")
        {
            return $"{TracerVersion.Major}{split}{TracerVersion.Minor}{split}{TracerVersion.Build}{split}0";
        }

        private string VersionString(string split = ".", bool withPrereleasePostfix = false)
        {
            var newVersion = $"{TracerVersion.Major}{split}{TracerVersion.Minor}{split}{TracerVersion.Build}";

            // this gets around a compiler warning about unreachable code below
            var isPreRelease = IsPrerelease;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (withPrereleasePostfix && isPreRelease)
            {
                newVersion = newVersion + "-prerelease";
            }

            return newVersion;
        }

        private string VersionPattern(string split = ".", bool withPrereleasePostfix = false, bool fourPartVersion = false)
        {
            if (split == ".")
            {
                split = @"\.";
            }

            var pattern = $@"\d+{split}\d+{split}\d+";

            if (fourPartVersion)
            {
                pattern = pattern + $@"{split}\d+";
            }

            if (withPrereleasePostfix)
            {
                pattern = pattern + "(\\-prerelease)?";
            }

            return pattern;
        }

        private string AssemblyString(string versionText)
        {
            return $"Datadog.Trace, Version={versionText}.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";
        }

        public class Samples : SetAllVersions
        {
            public Samples(string tracerDirectory, string tracerVersion, bool isPrerelease)
                : base(tracerDirectory, tracerVersion, isPrerelease)
            {
            }

            public override void Run()
            {
                Console.WriteLine($"Updating package version instances to {VersionString()}");

                // Sample application package updates
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/MicrosoftExtensionsExample/MicrosoftExtensionsExample.csproj",
                    DatadogTraceNugetDependencyVersionReplace);
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/Log4NetExample/Log4NetExample.csproj",
                    DatadogTraceNugetDependencyVersionReplace);
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/NLog40Example/NLog40Example.csproj",
                    DatadogTraceNugetDependencyVersionReplace);
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/NLog45Example/NLog45Example.csproj",
                    DatadogTraceNugetDependencyVersionReplace);
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/NLog46Example/NLog46Example.csproj",
                    DatadogTraceNugetDependencyVersionReplace);
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/SerilogExample/SerilogExample.csproj",
                    DatadogTraceNugetDependencyVersionReplace);

                // Dockerfile updates
                SynchronizeVersion(
                    "samples/ConsoleApp/Alpine3.9.dockerfile",
                    text => Regex.Replace(text, $"ARG TRACER_VERSION={VersionPattern()}", $"ARG TRACER_VERSION={VersionString()}"));

                SynchronizeVersion(
                    "samples/ConsoleApp/Alpine3.10.dockerfile",
                    text => Regex.Replace(text, $"ARG TRACER_VERSION={VersionPattern()}", $"ARG TRACER_VERSION={VersionString()}"));

                SynchronizeVersion(
                    "samples/ConsoleApp/Debian.dockerfile",
                    text => Regex.Replace(text, $"ARG TRACER_VERSION={VersionPattern()}", $"ARG TRACER_VERSION={VersionString()}"));

                SynchronizeVersion(
                    "samples/WindowsContainer/Dockerfile",
                    text => Regex.Replace(text, $"ARG TRACER_VERSION={VersionPattern()}", $"ARG TRACER_VERSION={VersionString()}"));

                // Pipeline monitor
                SynchronizeVersion(
                    "tools/PipelineMonitor/PipelineMonitor.csproj",
                    DatadogTraceNugetDependencyVersionReplace);

                Console.WriteLine($"Completed synchronizing package versions to {VersionString()}");
            }

        }

        public class Source : SetAllVersions
        {
            public Source(string tracerDirectory, string tracerVersion, bool isPrerelease)
                : base(tracerDirectory, tracerVersion, isPrerelease)
            {
            }

            public override void Run()
            {
                Console.WriteLine($"Updating source version instances to {VersionString()}");

                // Nuke build
                SynchronizeVersion(
                    "build/_build/Build.cs",
                    text => Regex.Replace(text, $"readonly string Version = \"{VersionPattern()}\"", $"readonly string Version = \"{VersionString()}\""));

                SynchronizeVersion(
                    "build/_build/Build.cs",
                    text => Regex.Replace(text, "readonly bool IsPrerelease = (true|false)", $"readonly bool IsPrerelease = {(IsPrerelease ? "true" : "false")}"));

                // Managed project / NuGet package updates
                SynchronizeVersion(
                    "src/Datadog.Monitoring.Distribution/Datadog.Monitoring.Distribution.csproj",
                    NugetVersionReplace);

                SynchronizeVersion(
                    "src/Datadog.Trace/Datadog.Trace.csproj",
                    NugetVersionReplace);

                SynchronizeVersion(
                    "src/Datadog.Trace.AspNet/Datadog.Trace.AspNet.csproj",
                    NugetVersionReplace);

                SynchronizeVersion(
                    "src/Datadog.Trace.ClrProfiler.Managed.Loader/Datadog.Trace.ClrProfiler.Managed.Loader.csproj",
                    NugetVersionReplace);

                SynchronizeVersion(
                    "src/Datadog.Trace.OpenTracing/Datadog.Trace.OpenTracing.csproj",
                    NugetVersionReplace);

                SynchronizeVersion(
                    "src/Datadog.Trace.MSBuild/Datadog.Trace.MSBuild.csproj",
                    NugetVersionReplace);

                SynchronizeVersion(
                    "src/Datadog.Trace.Tools.Runner/Datadog.Trace.Tools.Runner.csproj",
                    NugetVersionReplace);

                // Fully qualified name updates
                SynchronizeVersion(
                    "src/Datadog.Trace.ClrProfiler.Managed.Loader/Startup.cs",
                    FullAssemblyNameReplace);

                SynchronizeVersion(
                    "src/Datadog.Tracer.Native/dd_profiler_constants.h",
                    FullAssemblyNameReplace);

                SynchronizeVersion(
                    "src/Datadog.Tracer.Native/dd_profiler_constants.h",
                    text => FunctionCallReplace(text, "WithVersion"));

                // Four-part AssemblyVersion update
                SynchronizeVersion(
                    "src/Datadog.Trace/TracerConstants.cs",
                    FourPartVersionReplace);

                // Native clr profiler updates
                SynchronizeVersion(
                    "src/Datadog.Tracer.Native/CMakeLists.txt",
                    text => FullVersionReplace(text, ".", prefix: "VERSION "));

                SynchronizeVersion(
                    "src/Datadog.Tracer.Native/Resource.rc",
                    text =>
                    {
                        text = FullVersionReplace(text, ",");
                        text = FullVersionReplace(text, ".");
                        return text;
                    });

                SynchronizeVersion(
                    "src/Datadog.Tracer.Native/version.h",
                    text => FullVersionReplace(text, "."));

                // .NET profiler

                SynchronizeVersion(
                    "../profiler/src/ProfilerEngine/Datadog.Profiler.Native.Windows/Resource.rc",
                    text =>
                    {
                        text = FullVersionReplace(text, ",");
                        text = FullVersionReplace(text, ".");
                        return text;
                    });

                SynchronizeVersion(
                    "../profiler/src/ProfilerEngine/Datadog.Profiler.Native.Linux/CMakeLists.txt",
                    text => FullVersionReplace(text, ".", prefix: "VERSION "));

                SynchronizeVersion(
                    "../profiler/src/ProfilerEngine/Datadog.Profiler.Native/dd_profiler_version.h",
                    text => FullVersionReplace(text, "."));

                SynchronizeVersion(
                    "../.github/scripts/package_and_deploy.sh",
                    text => FullVersionReplace(text, ".", prefix: "current_profiler_version=\""));

                SynchronizeVersion(
                    "../profiler/src/ProfilerEngine/ProductVersion.props",
                    PropsVersionReplace);

                // Deployment updates
                SynchronizeVersion(
                    "../shared/src/msi-installer/WindowsInstaller.wixproj",
                    WixProjReplace);

                Console.WriteLine($"Completed synchronizing source versions to {VersionString()}");
            }
        }
    }
}
