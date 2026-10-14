using System;
using System.Collections.Generic;
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
       .Executes(() =>
        {
            // Resolved on the release channel so the expected values are the plain ones, whatever
            // channel the caller happens to be on.
            var version = DdVersion.Resolve(RootDirectory, buildChannelOverride: "release");

            var three = version.ThreePartVersion;
            var four = version.FourPartVersion;
            var semver = version.SemVer;
            var released = version.LatestReleasedVersion;
            var assemblyName = $"Datadog.Trace, Version={four}, Culture=neutral, PublicKeyToken=def86d061d0d2eeb";

            var checks = new List<(string File, string Expected)>();

            // The native version header is generated rather than committed, so check the generated
            // content instead - it is what the .rc files, the CMake projects and the C++ sources
            // all read.
            if (File.Exists(RootDirectory / "shared" / "src" / "native-src" / "dd_version.g.h"))
            {
                checks.Add(("shared/src/native-src/dd_version.g.h", $"#define DD_THREE_PART_VERSION_STR \"{three}\""));
                checks.Add(("shared/src/native-src/dd_version.g.h", $"#define DD_FOUR_PART_VERSION_STR \"{four}\""));
                checks.Add(("shared/src/native-src/dd_version.g.h", $"#define DD_VERSION_REVISION {version.Revision}"));
            }

            // Same for the managed constants. The native tracer resolves Datadog.Trace by this
            // exact strong name, so the loader's copy has to agree with the real assembly version.
            foreach (var generated in RootDirectory.GlobFiles("artifacts/obj/*/*/DatadogVersion.g.cs"))
            {
                var content = File.ReadAllText(generated);
                if (content.Contains("class Startup", StringComparison.Ordinal))
                {
                    checks.Add((RootDirectory.GetRelativePathTo(generated), assemblyName));
                }
                else
                {
                    checks.Add((RootDirectory.GetRelativePathTo(generated), $"AssemblyVersion = \"{four}\""));
                    checks.Add((RootDirectory.GetRelativePathTo(generated), $"ThreePartVersion = \"{three}\""));
                }
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

            Logger.Information("All {Count} version literals match version.props ({Version})", checks.Count, semver);
        });
}
