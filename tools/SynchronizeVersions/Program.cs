using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Core.Tools;

namespace SynchronizeVersions
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"Updating version instances to {VersionString()}");

            SynchronizeVersion(
                "integrations.json",
                FullAssemblyNameReplace);

            SynchronizeVersion(
                "docker/package.sh",
                text => Regex.Replace(text, $"VERSION={VersionPattern()}", $"VERSION={VersionString()}"));

            SynchronizeVersion(
                "reproductions/AutomapperTest/Dockerfile",
                text => Regex.Replace(text, $"ARG TRACER_VERSION={VersionPattern()}", $"ARG TRACER_VERSION={VersionString()}"));

            SynchronizeVersion(
                "src/Datadog.Trace.ClrProfiler.Managed.Loader/Datadog.Trace.ClrProfiler.Managed.Loader.csproj",
                NugetVersionReplace);

            SynchronizeVersion(
                "src/Datadog.Trace.ClrProfiler.Managed.Loader/Startup.cs",
                FullAssemblyNameReplace);

            SynchronizeVersion(
                "src/Datadog.Trace.ClrProfiler.Managed/Datadog.Trace.ClrProfiler.Managed.csproj",
                NugetVersionReplace);

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

            SynchronizeVersion(
                "src/Datadog.Trace.OpenTracing/Datadog.Trace.OpenTracing.csproj",
                NugetVersionReplace);

            SynchronizeVersion(
                "src/Datadog.Trace/Datadog.Trace.csproj",
                NugetVersionReplace);

            SynchronizeVersion(
                "deploy/Datadog.Trace.ClrProfiler.WindowsInstaller/Datadog.Trace.ClrProfiler.WindowsInstaller.wixproj",
                WixProjReplace);

            // SynchronizeVersion(
            //    "deploy/Datadog.Trace.ClrProfiler.WindowsInstaller/Product.wxs",
            //    WxsFileReplace);

            Console.WriteLine($"Completed synchronizing versions to {VersionString()}");
        }

        private static string FullVersionReplace(string text, string split)
        {
            return Regex.Replace(text, VersionPattern(split), VersionString(split), RegexOptions.Singleline);
        }

        private static string FullAssemblyNameReplace(string text)
        {
            return Regex.Replace(text, AssemblyString(VersionPattern()), AssemblyString(VersionString()), RegexOptions.Singleline);
        }

        private static string NugetVersionReplace(string text)
        {
            return Regex.Replace(text, $"<Version>{VersionPattern(withPrereleasePostfix: true)}</Version>", $"<Version>{VersionString(withPrereleasePostfix: true)}</Version>", RegexOptions.Singleline);
        }

        private static string WixProjReplace(string text)
        {
            text = Regex.Replace(
                text,
                $"<OutputName>datadog-dotnet-apm-{VersionPattern(withPrereleasePostfix: true)}-\\$\\(Platform\\)-\\$\\(Configuration\\)</OutputName>",
                $"<OutputName>datadog-dotnet-apm-{VersionString(withPrereleasePostfix: true)}-$(Platform)-$(Configuration)</OutputName>",
                RegexOptions.Singleline);

            text = Regex.Replace(
                text,
                $"InstallerVersion={VersionPattern()}",
                $"InstallerVersion={VersionString()}",
                RegexOptions.Singleline);

            text = Regex.Replace(
                text,
                $"<InstallerVersion>{VersionPattern()}</InstallerVersion>",
                $"<InstallerVersion>{VersionString()}</InstallerVersion>",
                RegexOptions.Singleline);

            return text;
        }

        private static string WxsFileReplace(string text)
        {
            text = Regex.Replace(
                text,
                $"Version=\"{VersionPattern()}\"",
                $"Version=\"{VersionString()}\"",
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

        private static string VersionString(string split = ".", bool withPrereleasePostfix = false)
        {
            var newVersion = $"{TracerVersion.Major}{split}{TracerVersion.Minor}{split}{TracerVersion.Patch}";

            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (withPrereleasePostfix && TracerVersion.IsPreRelease)
            {
                newVersion = newVersion + "-prerelease";
            }

            return newVersion;
        }

        private static string VersionPattern(string split = ".", bool withPrereleasePostfix = false)
        {
            if (split == ".")
            {
                split = @"\.";
            }

            var pattern = $@"\d+{split}\d+{split}\d+";

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
