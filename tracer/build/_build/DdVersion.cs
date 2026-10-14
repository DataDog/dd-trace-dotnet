// <copyright file="DdVersion.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace PrepareRelease
{
    /// <summary>
    /// Resolves the version shapes used across the repository from the single committed literal in
    /// <c>version.props</c>, and hands the resolved build channel / dev token to MSBuild.
    /// <para>
    /// Why this lives in C# rather than in MSBuild: the dev token comes from git and from the CI
    /// environment, and MSBuild cannot run git at evaluation time - which is exactly when
    /// <c>$(Version)</c> has to be known for NuGet packaging and <c>GenerateAssemblyInfo</c>. So
    /// Nuke resolves, writes <c>artifacts/version/Version.g.props</c>, and
    /// <c>build/Version.props</c> consumes it.
    /// </para>
    /// </summary>
    public class DdVersion
    {
        /// <summary>
        /// The 4th field of the assembly version is a <see cref="ushort"/> (see
        /// <c>AssemblyProperty::WithVersion</c> in <c>clr_helpers.h</c>), so the revision has to fit
        /// in 16 bits. Commit height is ~7,100 today, which leaves decades of headroom, but assert
        /// it rather than silently wrapping.
        /// </summary>
        private const int MaxRevision = ushort.MaxValue;

        private DdVersion(
            string versionPrefix,
            bool isPrerelease,
            string latestReleasedVersion,
            string versionSuffix,
            int revision,
            bool isReleaseChannel)
        {
            VersionPrefix = versionPrefix;
            IsPrerelease = isPrerelease;
            LatestReleasedVersion = latestReleasedVersion;
            VersionSuffix = versionSuffix;
            Revision = revision;
            IsReleaseChannel = isReleaseChannel;
        }

        /// <summary>Gets the three-part numeric version under development, e.g. <c>3.56.0</c>.</summary>
        public string VersionPrefix { get; }

        /// <summary>Gets a value indicating whether this version publishes as <c>X.Y.Z-prerelease</c>.</summary>
        public bool IsPrerelease { get; }

        /// <summary>Gets the most recent version published to nuget.org, used only by the samples.</summary>
        public string LatestReleasedVersion { get; }

        /// <summary>Gets the suffix, e.g. <c>""</c>, <c>-prerelease</c> or <c>-dev.g1a2b3c4d</c>.</summary>
        public string VersionSuffix { get; }

        /// <summary>Gets the 4th field of the assembly version. Zero on the release channel.</summary>
        public int Revision { get; }

        /// <summary>Gets a value indicating whether this build publishes under the plain version.</summary>
        public bool IsReleaseChannel { get; }

        /// <summary>Gets the three-part version. Never carries a suffix.</summary>
        public string ThreePartVersion => VersionPrefix;

        /// <summary>Gets the assembly/file version. Never carries a suffix.</summary>
        public string FourPartVersion => $"{VersionPrefix}.{Revision}";

        /// <summary>Gets the full semantic version. The only shape that identifies a specific build.</summary>
        public string SemVer => $"{VersionPrefix}{VersionSuffix}";

        /// <summary>
        /// Gets the version for deb/rpm. RPM rejects a '-' in the Version field, and both RPM and
        /// dpkg sort '~' before the unsuffixed version, which is the right order for a prerelease.
        /// </summary>
        public string RpmVersion => $"{VersionPrefix}{VersionSuffix.Replace('-', '~')}";

        /// <summary>
        /// Resolves the version for this build.
        /// </summary>
        /// <param name="repoRoot">The repository root, containing version.props.</param>
        /// <param name="versionPrefixOverride">Overrides the version being built, e.g. <c>3.57.0</c>.</param>
        /// <param name="versionSuffixOverride">Overrides the suffix, e.g. <c>rc1</c> or <c>-rc1</c>. Pass <c>"none"</c> to force an empty suffix.</param>
        /// <param name="buildChannelOverride">Forces the channel - <c>release</c> or <c>dev</c>.</param>
        public static DdVersion Resolve(
            string repoRoot,
            string versionPrefixOverride = null,
            string versionSuffixOverride = null,
            string buildChannelOverride = null)
        {
            var (prefix, isPrerelease, latestReleased) = ReadVersionProps(repoRoot);

            if (!string.IsNullOrEmpty(versionPrefixOverride))
            {
                prefix = versionPrefixOverride;
            }

            var isReleaseChannel = ResolveIsReleaseChannel(buildChannelOverride);

            string suffix;
            var revision = 0;

            if (!string.IsNullOrEmpty(versionSuffixOverride))
            {
                suffix = NormalizeSuffix(versionSuffixOverride);
            }
            else if (isReleaseChannel)
            {
                suffix = isPrerelease ? "-prerelease" : string.Empty;
            }
            else
            {
                suffix = $"-dev.{GetDevToken(repoRoot)}";
                revision = GetCommitHeight(repoRoot);
            }

            return new DdVersion(prefix, isPrerelease, latestReleased, suffix, revision, isReleaseChannel);
        }

        /// <summary>
        /// Writes the generated props consumed by <c>build/Version.props</c>, plus the
        /// <c>version.txt</c> / <c>version.json</c> that the CI shell scripts read.
        /// </summary>
        public void WriteGeneratedFiles(string repoRoot)
        {
            var directory = Path.Combine(repoRoot, "artifacts", "version");
            Directory.CreateDirectory(directory);

            var props = new StringBuilder();
            props.AppendLine("<Project>");
            props.AppendLine();
            props.AppendLine("  <!--");
            props.AppendLine("    <auto-generated/> Written by the GenerateVersionFiles Nuke target. Do not edit or commit.");
            props.AppendLine("    This is how the resolved build channel and dev token reach MSBuild - see");
            props.AppendLine("    tracer/build/_build/DdVersion.cs and build/Version.props.");
            props.AppendLine("  -->");
            props.AppendLine("  <PropertyGroup>");
            props.AppendLine($"    <DD_VersionSuffix>{VersionSuffix}</DD_VersionSuffix>");
            props.AppendLine($"    <DD_VersionRevision>{Revision.ToString(CultureInfo.InvariantCulture)}</DD_VersionRevision>");
            props.AppendLine($"    <DD_BuildChannel>{(IsReleaseChannel ? "release" : "dev")}</DD_BuildChannel>");
            props.AppendLine("  </PropertyGroup>");
            props.AppendLine();
            props.AppendLine("</Project>");

            WriteIfDifferent(Path.Combine(directory, "Version.g.props"), props.ToString());

            // Consumed by .gitlab/prepare-oci-package.sh and the Azure DevOps packaging stages.
            WriteIfDifferent(Path.Combine(directory, "version.txt"), SemVer);

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine($"  \"versionPrefix\": \"{VersionPrefix}\",");
            json.AppendLine($"  \"versionSuffix\": \"{VersionSuffix}\",");
            json.AppendLine($"  \"threePartVersion\": \"{ThreePartVersion}\",");
            json.AppendLine($"  \"fourPartVersion\": \"{FourPartVersion}\",");
            json.AppendLine($"  \"semVer\": \"{SemVer}\",");
            json.AppendLine($"  \"rpmVersion\": \"{RpmVersion}\",");
            json.AppendLine($"  \"revision\": {Revision.ToString(CultureInfo.InvariantCulture)},");
            json.AppendLine($"  \"buildChannel\": \"{(IsReleaseChannel ? "release" : "dev")}\",");
            json.AppendLine($"  \"isPrerelease\": {(IsPrerelease ? "true" : "false")},");
            json.AppendLine($"  \"latestReleasedVersion\": \"{LatestReleasedVersion}\"");
            json.AppendLine("}");

            WriteIfDifferent(Path.Combine(directory, "version.json"), json.ToString());
        }

        /// <summary>
        /// Gets the whole set of derived shapes, for printing and for consistency checks.
        /// </summary>
        public IReadOnlyDictionary<string, string> GetAllShapes() => new Dictionary<string, string>
        {
            ["VersionPrefix"] = VersionPrefix,
            ["VersionSuffix"] = VersionSuffix,
            ["ThreePartVersion"] = ThreePartVersion,
            ["FourPartVersion"] = FourPartVersion,
            ["SemVer"] = SemVer,
            ["RpmVersion"] = RpmVersion,
            ["Revision"] = Revision.ToString(CultureInfo.InvariantCulture),
            ["BuildChannel"] = IsReleaseChannel ? "release" : "dev",
            ["IsPrerelease"] = IsPrerelease ? "true" : "false",
            ["LatestReleasedVersion"] = LatestReleasedVersion,
        };

        /// <summary>
        /// Normalises a caller-supplied suffix. Nuke refuses an argument value that starts with a
        /// '-' (it parses as the next switch), so <c>--VersionSuffix rc1</c> has to work as well as
        /// the literal <c>-rc1</c> that an environment variable can carry. <c>none</c> asks for an
        /// explicitly empty suffix, which an empty string can't express because Nuke treats that
        /// as "not supplied".
        /// </summary>
        private static string NormalizeSuffix(string suffix)
        {
            if (string.Equals(suffix, "none", StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            return suffix.StartsWith('-') ? suffix : "-" + suffix;
        }

        private static (string Prefix, bool IsPrerelease, string LatestReleased) ReadVersionProps(string repoRoot)
        {
            var path = Path.Combine(repoRoot, "version.props");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Could not find {path}. This file holds the SDK version number.", path);
            }

            var content = File.ReadAllText(path);

            var prefix = MatchElement(content, "DD_VersionPrefix", path);
            if (!Regex.IsMatch(prefix, @"^\d+\.\d+\.\d+$"))
            {
                throw new Exception($"DD_VersionPrefix in {path} must be a three-part numeric version, but was '{prefix}'");
            }

            var isPrerelease = string.Equals(MatchElement(content, "DD_IsPrerelease", path), "true", StringComparison.OrdinalIgnoreCase);
            var latestReleased = MatchElement(content, "DD_LatestReleasedVersion", path);

            return (prefix, isPrerelease, latestReleased);
        }

        private static string MatchElement(string content, string element, string path)
        {
            var match = Regex.Match(content, $"<{element}>(?<value>[^<]*)</{element}>");
            if (!match.Success)
            {
                throw new Exception($"Could not find <{element}> in {path}");
            }

            return match.Groups["value"].Value.Trim();
        }

        /// <summary>
        /// A build publishes under the plain version only when CI builds a branch we actually
        /// release from. Everything else - PR branches, feature branches and local builds - gets a
        /// unique dev version. Deliberately conservative about what counts as "CI": a machine with
        /// none of these variables set is a developer's machine.
        /// </summary>
        private static bool ResolveIsReleaseChannel(string buildChannelOverride)
        {
            if (!string.IsNullOrEmpty(buildChannelOverride))
            {
                return buildChannelOverride switch
                {
                    "release" => true,
                    "dev" => false,
                    _ => throw new Exception($"Unknown build channel '{buildChannelOverride}'. Expected 'release' or 'dev'."),
                };
            }

            var reference = GetCiReference();
            if (reference is null)
            {
                return false;
            }

            return reference is "refs/heads/master" or "refs/heads/main" or "master" or "main"
                || reference.StartsWith("refs/heads/release/", StringComparison.Ordinal)
                || reference.StartsWith("refs/heads/hotfix/", StringComparison.Ordinal)
                || reference.StartsWith("release/", StringComparison.Ordinal)
                || reference.StartsWith("hotfix/", StringComparison.Ordinal)
                || reference.StartsWith("refs/tags/v", StringComparison.Ordinal);
        }

        /// <summary>
        /// Gets the branch or tag being built, or null when not on CI. Note that for an Azure
        /// DevOps pull-request build <c>BUILD_SOURCEBRANCH</c> is <c>refs/pull/N/merge</c>, which
        /// correctly resolves to the dev channel.
        /// </summary>
        private static string GetCiReference()
        {
            if (GetEnv("TF_BUILD") is not null)
            {
                return GetEnv("BUILD_SOURCEBRANCH") ?? string.Empty;
            }

            if (GetEnv("GITLAB_CI") is not null)
            {
                return GetEnv("CI_COMMIT_TAG") is { } tag ? $"refs/tags/{tag}" : GetEnv("CI_COMMIT_REF_NAME") ?? string.Empty;
            }

            if (GetEnv("GITHUB_ACTIONS") is not null)
            {
                return GetEnv("GITHUB_REF") ?? string.Empty;
            }

            return null;
        }

        /// <summary>
        /// Gets the token that makes a dev version unique.
        /// <para>
        /// The 'g' prefix is not decoration: a bare hex sha can be all digits with a leading zero
        /// (e.g. 01234567), which is an invalid semver prerelease identifier and is rejected by
        /// NuGet. 'g&lt;sha&gt;' is the git-describe convention and is always alphanumeric.
        /// </para>
        /// </summary>
        private static string GetDevToken(string repoRoot)
        {
            var sha = GetEnv("BUILD_SOURCEVERSION")
                   ?? GetEnv("CI_COMMIT_SHA")
                   ?? GetEnv("GITHUB_SHA")
                   ?? RunGit(repoRoot, "rev-parse HEAD");

            if (string.IsNullOrEmpty(sha))
            {
                throw new Exception(
                    "Could not determine the commit SHA for the dev version. Set DD_VERSION_SUFFIX, " +
                    "or pass --version-suffix, or build with --build-channel release.");
            }

            return "g" + sha.Trim().Substring(0, 8);
        }

        private static int GetCommitHeight(string repoRoot)
        {
            var output = RunGit(repoRoot, "rev-list --count HEAD");
            if (!int.TryParse(output?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var height))
            {
                // A shallow clone can't count commits. Falling back to 0 keeps the assembly version
                // well-formed; the SemVer still carries the sha, which is what identifies the build.
                return 0;
            }

            if (height > MaxRevision)
            {
                throw new Exception(
                    $"The commit height ({height}) no longer fits in the 16-bit assembly version revision field. " +
                    "Pick a different revision scheme - see build/Version.props.");
            }

            return height;
        }

        private static string GetEnv(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? null : value;
        }

        /// <summary>
        /// Shells out to git directly rather than going through Nuke's tooling, so that this class
        /// stays plain logic with no build-framework dependency and can be unit tested. Returns
        /// null rather than throwing when git is unavailable or the directory isn't a repository.
        /// </summary>
        private static string RunGit(string repoRoot, string arguments)
        {
            try
            {
                using var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "git",
                        Arguments = arguments,
                        WorkingDirectory = repoRoot,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    },
                };

                process.Start();
                var output = process.StandardOutput.ReadToEnd();
                process.StandardError.ReadToEnd();
                process.WaitForExit();

                return process.ExitCode == 0 ? output : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static void WriteIfDifferent(string path, string content)
        {
            // Avoid touching mtimes that MSBuild and CMake use for incremental builds.
            if (File.Exists(path) && File.ReadAllText(path) == content)
            {
                return;
            }

            File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
    }
}
