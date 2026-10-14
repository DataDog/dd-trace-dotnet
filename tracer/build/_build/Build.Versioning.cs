using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using PrepareRelease;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using Logger = Serilog.Log;

partial class Build
{
    AbsolutePath VersionPropsFile => RootDirectory / "version.props";
    AbsolutePath GeneratedVersionDirectory => RootDirectory / "artifacts" / "version";
    AbsolutePath VersionProjectFile => RootDirectory / "build" / "Version.proj";

    Target GenerateVersionFiles => _ => _
       .Unlisted()
       .Description("Resolves the version for this build and writes the generated version files")
       .Executes(() =>
        {
            Ver.WriteGeneratedFiles(RootDirectory);

            Logger.Information("Resolved version {SemVer} (channel: {Channel})", Ver.SemVer, Ver.IsReleaseChannel ? "release" : "dev");

            // build/Version.targets owns the format of dd_version.g.h, so materialise it through
            // MSBuild rather than writing it here - that keeps a single generator. It has to exist
            // before CMake configures, and doing it once here means the parallel C++ fan-out in
            // tracer/Datadog.Trace.proj never races on the write.
            DotNetMSBuild(x => x
                .SetTargetPath(VersionProjectFile)
                .SetTargets("DD_GenerateVersionFiles")
                .SetVerbosity(DotNetVerbosity.Minimal));
        });

    Target PrintVersion => _ => _
       .Description("Prints every version shape used by the build")
       .Executes(() =>
        {
            foreach (var (name, value) in Ver.GetAllShapes())
            {
                Logger.Information("{Name,-22} {Value}", name, value);
            }
        });

    Target VerifyVersionConsistency => _ => _
       .Description("Verifies that every committed version literal matches version.props")
       // Generate first, so the check compares the generated state against the *same* resolution
       // rather than against whatever an earlier build on a different channel happened to leave.
       .DependsOn(GenerateVersionFiles)
       .Executes(() =>
        {
            // Committed literals are always the plain release values, whatever channel the caller
            // happens to be on. Generated files, on the other hand, have to match *this* build -
            // the assembly version revision is the commit height on a dev build.
            var release = DdVersion.Resolve(RootDirectory, buildChannelOverride: "release");
            var released = release.LatestReleasedVersion;

            var three = Ver.ThreePartVersion;
            var four = Ver.FourPartVersion;
            var assemblyName = $"Datadog.Trace, Version={four}, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";

            var checks = new List<(string File, string Expected)>();

            // The native version header is generated rather than committed, so check the generated
            // content instead - it is what the .rc files, the CMake projects and the C++ sources
            // all read.
            if (File.Exists(RootDirectory / "shared" / "src" / "native-src" / "dd_version.g.h"))
            {
                checks.Add(("shared/src/native-src/dd_version.g.h", $"#define DD_THREE_PART_VERSION_STR \"{three}\""));
                checks.Add(("shared/src/native-src/dd_version.g.h", $"#define DD_FOUR_PART_VERSION_STR \"{four}\""));
                checks.Add(("shared/src/native-src/dd_version.g.h", $"#define DD_VERSION_REVISION {Ver.Revision}"));
            }

            // The samples deliberately reference the *previously released* version, because they
            // have to restore a package that actually exists on nuget.org.
            foreach (var sample in new[] { "MicrosoftExtensionsExample", "Log4NetExample", "NLog40Example", "NLog45Example", "NLog46Example", "SerilogExample" })
            {
                checks.Add(($"tracer/samples/AutomaticTraceIdInjection/{sample}/{sample}.csproj",
                            $"<PackageReference Include=\"Datadog.Trace.Bundle\" Version=\"{released}\" />"));
            }

            foreach (var dockerfile in new[] { "ConsoleApp/Alpine3.9.dockerfile", "ConsoleApp/Alpine3.10.dockerfile", "ConsoleApp/Debian.dockerfile", "OpenTelemetry/Debian.dockerfile", "WindowsContainer/Dockerfile" })
            {
                checks.Add(($"tracer/samples/{dockerfile}", $"ARG TRACER_VERSION={released}"));
            }

            var failures = new List<string>();

            foreach (var (file, expected) in checks)
            {
                var path = RootDirectory / file;
                if (!File.Exists(path))
                {
                    failures.Add($"{file}: file not found");
                    continue;
                }

                if (!File.ReadAllText(path).Contains(expected, StringComparison.Ordinal))
                {
                    failures.Add($"{file}: expected to contain '{expected}'");
                }
            }

            // The most important invariant of the lot: MSBuild derives the version shapes
            // independently of DdVersion (build/Version.props does the arithmetic, DdVersion only
            // hands it the resolved suffix and revision). If the two ever disagree, the managed
            // assembly version and the version Nuke puts in artifact names would silently diverge.
            // Checked by evaluating the properties rather than by reading artifacts/obj, which can
            // hold stale output from an earlier build on a different channel.
            foreach (var (property, expected) in new[]
                     {
                         ("DD_ThreePartVersion", three),
                         ("DD_FourPartVersion", four),
                         ("DD_SemVer", Ver.SemVer),
                         ("DD_RpmVersion", Ver.RpmVersion),
                         ("DD_VersionRevision", Ver.Revision.ToString(CultureInfo.InvariantCulture)),
                     })
            {
                var actual = EvaluateMsBuildProperty(property);
                if (actual != expected)
                {
                    failures.Add($"MSBuild $({property}) is '{actual}' but DdVersion resolved '{expected}'");
                }
            }

            if (failures.Count > 0)
            {
                foreach (var failure in failures)
                {
                    Logger.Error("::error::{Failure}", failure);
                }

                throw new Exception(
                    $"{failures.Count} version literal(s) are out of sync with {VersionPropsFile}. " +
                    "Either the literal is stale, or it is no longer committed and should be removed from VerifyVersionConsistency.");
            }

            Logger.Information("Version is consistent across MSBuild and the build ({Version})", Ver.SemVer);
        });

    /// <summary>
    /// Evaluates a single property from build/Version.proj, so the check sees exactly what every
    /// other project in the repository sees.
    /// </summary>
    private string EvaluateMsBuildProperty(string property)
    {
        var output = DotNet($"msbuild \"{VersionProjectFile}\" -getProperty:{property}", logOutput: false)
                    .Where(x => x.Type == OutputType.Std)
                    .Select(x => x.Text.Trim())
                    .LastOrDefault(x => !string.IsNullOrEmpty(x));

        return output ?? string.Empty;
    }
}
