using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Datadog.Trace.TestHelpers;

namespace SynchronizeVersions
{
    public class Program
    {
        private static int major = 1;
        private static int minor = 7;
        private static int patch = 0;

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
                "/deploy/Nuget/Datadog.Trace.nuspec",
                NugetVersionReplace);

            Console.WriteLine($"Completed synchronizing versions to {VersionString()}");
        }

        private static string FullVersionReplace(string text, string split)
        {
            return Regex.Replace(text, VersionPattern(split), VersionString(split));
        }

        private static string FullAssemblyNameReplace(string text)
        {
            return Regex.Replace(text, AssemblyString(VersionPattern()), AssemblyString(VersionString()));
        }

        private static string NugetVersionReplace(string text)
        {
            text =
                Regex.Replace(text, $"<Version>{VersionPattern()}</Version>", $"<Version>{VersionString()}</Version>");
            text =
                Regex.Replace(text, $"<version>{VersionPattern()}</version>", $"<version>{VersionString()}</version>");
            return text;
        }

        private static void SynchronizeVersion(string path, Func<string, string> transform)
        {
            var solutionDirectory = EnvironmentHelper.GetSolutionDirectory();
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

        private static string VersionString(string split = ".")
        {
            return $"{major}{split}{minor}{split}{patch}";
        }

        private static string VersionPattern(string split = ".")
        {
            if (split == ".")
            {
                split = @"\.";
            }

            return $@"\d+{split}\d+{split}\d+";
        }

        private static string AssemblyString(string versionText)
        {
            return $"Datadog.Trace.ClrProfiler.Managed, Version={versionText}.0, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";
        }
    }
}
