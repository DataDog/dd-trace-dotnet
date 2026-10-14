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
    /// <summary>
    /// Rewrites the version in the sample projects.
    /// <para>
    /// This used to have a sibling that did the same for nineteen source files. Those all derive
    /// their version from version.props at build time now - see build/Version.props. The samples are
    /// the exception, because they reference the version that is published on nuget.org and
    /// dl.datadoghq.com rather than the one being built, so they lag a release behind and are not
    /// derivable from version.props's prefix.
    /// </para>
    /// </summary>
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
        /// Gets the version to write into the samples - the most recent published release.
        /// </summary>
        public Version TracerVersion { get; }

        /// <summary>
        /// Gets a value indicating whether that version is a prerelease.
        /// </summary>
        public bool IsPrerelease { get; }

        public abstract void Run();

        private string DatadogTraceBundleNugetDependencyVersionReplace(string text)
        {
            return Regex.Replace(text, $"<PackageReference Include=\"Datadog.Trace.Bundle\" Version=\"{VersionPattern(withPrereleasePostfix: true)}\" />", $"<PackageReference Include=\"Datadog.Trace.Bundle\" Version=\"{VersionString(withPrereleasePostfix: true)}\" />", RegexOptions.Singleline);
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

        private string VersionString(string split = ".", bool withPrereleasePostfix = false)
        {
            var newVersion = $"{TracerVersion.Major}{split}{TracerVersion.Minor}{split}{TracerVersion.Build}";

            if (withPrereleasePostfix && IsPrerelease)
            {
                newVersion += "-prerelease";
            }

            return newVersion;
        }

        private string VersionPattern(string split = ".", bool withPrereleasePostfix = false)
        {
            if (split == ".")
            {
                split = @"\.";
            }

            var pattern = $@"\d+{split}\d+{split}\d+";

            if (withPrereleasePostfix)
            {
                pattern += "(\\-prerelease)?";
            }

            return pattern;
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
    }
}
