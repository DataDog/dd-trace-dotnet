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
        // A semver prerelease suffix: '-' then dot-separated identifiers of [0-9A-Za-z-], or empty for a release
        private static readonly Regex VersionSuffixRegex = new(@"^(-[0-9A-Za-z-]+(\.[0-9A-Za-z-]+)*)?$");

        protected SetAllVersions(string tracerDirectory, string tracerVersion, string versionSuffix)
        {
            TracerDirectory = tracerDirectory;

            // new Version() rejects suffixes like -prerelease; the parts check rejects 2- and 4-part versions.
            // Validating here means a bad value fails before anything is written.
            TracerVersion = new Version(tracerVersion);
            if (TracerVersion.Build < 0 || TracerVersion.Revision >= 0)
            {
                throw new ArgumentException($"Version must be a three-part numeric version (e.g. 3.58.0), but was '{tracerVersion}'", nameof(tracerVersion));
            }

            if (!VersionSuffixRegex.IsMatch(versionSuffix))
            {
                throw new ArgumentException($"Version suffix must be empty or a semver prerelease suffix such as -prerelease, but was '{versionSuffix}'", nameof(versionSuffix));
            }

            VersionSuffix = versionSuffix;
        }

        /// <summary>
        /// Gets the tracer directory, where src/build/samples/test subdirectories can be found.
        /// </summary>
        public string TracerDirectory { get; }

        /// <summary>
        /// Gets the version to write. The source of truth is tracer/build/_build/version.props;
        /// change it with the UpdateVersion Nuke target rather than editing files by hand.
        /// </summary>
        public Version TracerVersion { get; }

        /// <summary>
        /// Gets the semver prerelease suffix to write, including its leading '-', or empty for a release.
        /// </summary>
        public string VersionSuffix { get; }

        public abstract void Run();

        private string DatadogTraceBundleNugetDependencyVersionReplace(string text)
        {
            return Regex.Replace(text, $"<PackageReference Include=\"Datadog.Trace.Bundle\" Version=\"{VersionPattern(withSuffix: true)}\" />", $"<PackageReference Include=\"Datadog.Trace.Bundle\" Version=\"{VersionString(withSuffix: true)}\" />", RegexOptions.Singleline);
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

            if (newFileContent == fileContent)
            {
                throw new Exception(
                    $"No version strings were replaced in {path}. Either its format changed and the " +
                    "pattern in SetAllVersions no longer matches, or the file already holds this " +
                    "version - check which before changing the pattern.");
            }

            File.WriteAllText(fullPath, newFileContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        private string VersionString(string split = ".", bool withSuffix = false)
        {
            var newVersion = $"{TracerVersion.Major}{split}{TracerVersion.Minor}{split}{TracerVersion.Build}";
            return withSuffix ? newVersion + VersionSuffix : newVersion;
        }

        private string VersionPattern(string split = ".", bool withSuffix = false)
        {
            if (split == ".")
            {
                split = @"\.";
            }

            var pattern = $@"\d+{split}\d+{split}\d+";

            if (withSuffix)
            {
                pattern = pattern + @"(-[0-9A-Za-z.-]+)?";
            }

            return pattern;
        }

        public class Samples : SetAllVersions
        {
            public Samples(string tracerDirectory, string tracerVersion, string versionSuffix)
                : base(tracerDirectory, tracerVersion, versionSuffix)
            {
            }

            public override void Run()
            {
                Console.WriteLine($"Updating package version instances to {VersionString()}");

                // Sample application package updates
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/MicrosoftExtensionsExample/MicrosoftExtensionsExample.csproj",
                    DatadogTraceBundleNugetDependencyVersionReplace);
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/Log4NetExample/Log4NetExample.csproj",
                    DatadogTraceBundleNugetDependencyVersionReplace);
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/NLog40Example/NLog40Example.csproj",
                    DatadogTraceBundleNugetDependencyVersionReplace);
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/NLog45Example/NLog45Example.csproj",
                    DatadogTraceBundleNugetDependencyVersionReplace);
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/NLog46Example/NLog46Example.csproj",
                    DatadogTraceBundleNugetDependencyVersionReplace);
                SynchronizeVersion(
                    "samples/AutomaticTraceIdInjection/SerilogExample/SerilogExample.csproj",
                    DatadogTraceBundleNugetDependencyVersionReplace);

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
                    "samples/OpenTelemetry/Debian.dockerfile",
                    text => Regex.Replace(text, $"ARG TRACER_VERSION={VersionPattern()}", $"ARG TRACER_VERSION={VersionString()}"));

                SynchronizeVersion(
                    "samples/WindowsContainer/Dockerfile",
                    text => Regex.Replace(text, $"ARG TRACER_VERSION={VersionPattern()}", $"ARG TRACER_VERSION={VersionString()}"));

                Console.WriteLine($"Completed synchronizing package versions to {VersionString()}");
            }

        }

        public class Source : SetAllVersions
        {
            public Source(string tracerDirectory, string tracerVersion, string versionSuffix)
                : base(tracerDirectory, tracerVersion, versionSuffix)
            {
            }

            public override void Run()
            {
                Console.WriteLine($"Updating source version instances to {VersionString()}");

                // The only copy of the version. Managed projects import it directly, native projects
                // through build/DdVersion.props, and CMake reads it in build/cmake/DdVersion.cmake.
                // Always changes, because UpdateVersion rejects a bump that does not change the
                // version number.
                SynchronizeVersion(
                    "build/_build/version.props",
                    text =>
                    {
                        text = ReplaceElement(text, "DD_VersionPrefix", VersionString());
                        return ReplaceElement(text, "DD_VersionSuffix", VersionSuffix);
                    });

                Console.WriteLine($"Completed synchronizing source versions to {VersionString()}");
            }

            private static string ReplaceElement(string text, string element, string value)
            {
                // version.props documents that each element appears exactly once, and is never mentioned in comments
                var pattern = $"<{element}>[^<]*</{element}>";
                if (!Regex.IsMatch(text, pattern))
                {
                    throw new Exception($"Could not find <{element}> in version.props");
                }

                return Regex.Replace(text, pattern, $"<{element}>{value}</{element}>");
            }
        }
    }
}
