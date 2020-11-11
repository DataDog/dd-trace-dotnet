using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Core.Tools;

namespace PrepareRelease
{
    public static class SetAllVersions
    {
        public static void Run()
        {
            Console.WriteLine($"Updating version instances to {VersionString()}");

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
                "test/test-applications/regression/AutomapperTest/Dockerfile",
                text => Regex.Replace(text, $"ARG TRACER_VERSION={VersionPattern()}", $"ARG TRACER_VERSION={VersionString()}"));

            // Managed project / NuGet package updates
            SynchronizeVersion(
                "src/Datadog.Trace/Datadog.Trace.csproj",
                NugetVersionReplace);

            SynchronizeVersion(
                "src/Datadog.Trace.AspNet/Datadog.Trace.AspNet.csproj",
                NugetVersionReplace);

            SynchronizeVersion(
                "src/Datadog.Trace.ClrProfiler.Managed/Datadog.Trace.ClrProfiler.Managed.csproj",
                NugetVersionReplace);

            SynchronizeVersion(
                "src/Datadog.Trace.ClrProfiler.Managed.Core/Datadog.Trace.ClrProfiler.Managed.Core.csproj",
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
                "src/Datadog.Trace.DuckTyping/Datadog.Trace.DuckTyping.csproj",
                NugetVersionReplace);

            SynchronizeVersion(
                "src/Datadog.Trace.Tools.Runner/Datadog.Trace.Tools.Runner.Standalone.csproj",
                NugetVersionReplace);

            SynchronizeVersion(
                "src/Datadog.Trace.Tools.Runner/Datadog.Trace.Tools.Runner.Tool.csproj",
                NugetVersionReplace);

            // Fully qualified name updates
            SynchronizeVersion(
                "src/Datadog.Trace.ClrProfiler.Managed.Loader/Startup.cs",
                FullAssemblyNameReplace);

            SynchronizeVersion(
                "src/Datadog.Trace.ClrProfiler.Native/dd_profiler_constants.h",
                FullAssemblyNameReplace);

            // Four-part AssemblyVersion update
            SynchronizeVersion(
                "src/Datadog.Trace/TracerConstants.cs",
                FourPartVersionReplace);

            // Locked AssemblyVersion #.0.0.0 updates
            SynchronizeVersion(
                "src/Datadog.Trace.AspNet/AssemblyInfo.cs",
                text => MajorAssemblyVersionReplace(text, "."));

            SynchronizeVersion(
                "src/Datadog.Trace.ClrProfiler.Managed.Core/AssemblyInfo.cs",
                text => MajorAssemblyVersionReplace(text, "."));

            SynchronizeVersion(
                "src/Datadog.Trace.DuckTyping/AssemblyInfo.cs",
                text => MajorAssemblyVersionReplace(text, "."));

            // Native profiler updates
            SynchronizeVersion(
                "src/Datadog.Trace.ClrProfiler.Native/CMakeLists.txt",
                text => FullVersionReplace(text, "."));

            SynchronizeVersion(
                "src/Datadog.Trace.ClrProfiler.Native/Resource.rc",
                text =>
                {
                    text = FullVersionReplace(text, ",");
                    text = FullVersionReplace(text, ".");
                    return text;
                });

            SynchronizeVersion(
                "src/Datadog.Trace.ClrProfiler.Native/version.h",
                text => FullVersionReplace(text, "."));

            // Deployment updates
            SynchronizeVersion(
                "integrations.json",
                FullAssemblyNameReplace);

            SynchronizeVersion(
                "build/docker/package.sh",
                text => Regex.Replace(text, $"VERSION={VersionPattern()}", $"VERSION={VersionString()}"));

            SynchronizeVersion(
                "src/WindowsInstaller/WindowsInstaller.wixproj",
                WixProjReplace);

            Console.WriteLine($"Completed synchronizing versions to {VersionString()}");
        }

        private static string FourPartVersionReplace(string text)
        {
            return Regex.Replace(text, VersionPattern(fourPartVersion: true), FourPartVersionString(), RegexOptions.Singleline);
        }

        private static string FullVersionReplace(string text, string split)
        {
            return Regex.Replace(text, VersionPattern(split), VersionString(split), RegexOptions.Singleline);
        }

        private static string FullAssemblyNameReplace(string text)
        {
            return Regex.Replace(text, AssemblyString(VersionPattern()), AssemblyString(VersionString()), RegexOptions.Singleline);
        }

        private static string MajorAssemblyVersionReplace(string text, string split)
        {
            return Regex.Replace(text, VersionPattern(fourPartVersion: true), MajorVersionString(split), RegexOptions.Singleline);
        }

        private static string NugetVersionReplace(string text)
        {
            return Regex.Replace(text, $"<Version>{VersionPattern(withPrereleasePostfix: true)}</Version>", $"<Version>{VersionString(withPrereleasePostfix: true)}</Version>", RegexOptions.Singleline);
        }

        private static string NuspecVersionReplace(string text)
        {
            return Regex.Replace(text, $"<version>{VersionPattern(withPrereleasePostfix: true)}</version>", $"<version>{VersionString(withPrereleasePostfix: true)}</version>", RegexOptions.Singleline);
        }

        private static string WixProjReplace(string text)
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

        private static void SynchronizeVersion(string path, Func<string, string> transform)
        {
            var solutionDirectory = EnvironmentTools.GetSolutionDirectory();
            var fullPath = Path.Combine(solutionDirectory, path);

            Console.WriteLine($"Updating version instances for {path}");

            if (!File.Exists(fullPath))
            {
                throw new Exception($"File not found to version: {path}");
            }

            var fileContent = File.ReadAllText(fullPath);
            var newFileContent = transform(fileContent);

            File.WriteAllText(fullPath, newFileContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private static string FourPartVersionString(string split = ".")
        {
            return $"{TracerVersion.Major}{split}{TracerVersion.Minor}{split}{TracerVersion.Patch}{split}0";
        }

        private static string MajorVersionString(string split = ".")
        {
            return $"{TracerVersion.Major}{split}0{split}0{split}0";
        }

        private static string VersionString(string split = ".", bool withPrereleasePostfix = false)
        {
            var newVersion = $"{TracerVersion.Major}{split}{TracerVersion.Minor}{split}{TracerVersion.Patch}";

            // this gets around a compiler warning about unreachable code below
            var isPreRelease = TracerVersion.IsPreRelease;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (withPrereleasePostfix && isPreRelease)
            {
                newVersion = newVersion + "-prerelease";
            }

            return newVersion;
        }

        private static string VersionPattern(string split = ".", bool withPrereleasePostfix = false, bool fourPartVersion = false)
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

        private static string AssemblyString(string versionText)
        {
            return $"Datadog.Trace.ClrProfiler.Managed, Version={versionText}.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";
        }
    }
}
