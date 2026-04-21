// <copyright file="PackageVersionGenerator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Logger = Serilog.Log;

namespace GeneratePackageVersions
{
    public class PackageVersionGenerator
    {
        private readonly Func<string, CooldownMode> _getCooldownMode;
        private readonly AbsolutePath _definitionsFilePath;
        private readonly PackageGroup _latestMinors;
        private readonly PackageGroup _latestMajors;
        private readonly PackageGroup _latestSpecific;
        private readonly XunitStrategyFileGenerator _strategyGenerator;
        private readonly DateTimeOffset _cutoffDate;
        private readonly Dictionary<string, List<Version>> _previousMaxVersions;

        /// <summary>
        /// NuGet query results from this run, keyed by package name. Not persisted across runs.
        /// Used to dedup queries when multiple entries share a package name (e.g. split version ranges),
        /// and so the caller can look up publish dates when logging version bumps.
        /// </summary>
        public Dictionary<string, List<VersionWithDate>> QueriedVersions { get; } = new();

        /// <summary>
        /// Report of package versions that were excluded by the cooldown filter.
        /// </summary>
        public CooldownReport CooldownReport { get; }


        public PackageVersionGenerator(
            AbsolutePath tracerDirectory,
            AbsolutePath testProjectDirectory,
            Func<string, CooldownMode> getCooldownMode,
            int cooldownDays,
            Dictionary<string, List<Version>> previousMaxVersions)
        {
            _getCooldownMode = getCooldownMode;
            _cutoffDate = DateTimeOffset.UtcNow.AddDays(-cooldownDays);
            _previousMaxVersions = previousMaxVersions;
            CooldownReport = new CooldownReport(cooldownDays);
            var propsDirectory = tracerDirectory / "build";
            _definitionsFilePath = tracerDirectory / "build" / "PackageVersionsGeneratorDefinitions.json";
            _latestMinors = new PackageGroup(propsDirectory, testProjectDirectory, "LatestMinors");
            _latestMajors = new PackageGroup(propsDirectory, testProjectDirectory, "LatestMajors");
            _latestSpecific = new PackageGroup(propsDirectory, testProjectDirectory, "LatestSpecific");
            _strategyGenerator = new XunitStrategyFileGenerator(testProjectDirectory / "PackageVersions.g.cs");

            if (!File.Exists(_definitionsFilePath))
            {
                throw new Exception($"Definitions file {_definitionsFilePath} does not exist. Exiting.");
            }
        }

        public async Task<List<TestedPackage>> GenerateVersions(Solution solution)
        {
            var definitions = File.ReadAllText(_definitionsFilePath);
            var entries = JsonConvert.DeserializeObject<PackageVersionEntry[]>(definitions);
            return await RunFileGeneratorWithPackageEntries(entries, solution);
        }

        private async Task<List<TestedPackage>> RunFileGeneratorWithPackageEntries(IEnumerable<PackageVersionEntry> entries, Solution solution)
        {
            _latestMinors.Start();
            _latestMajors.Start();
            _latestSpecific.Start();
            _strategyGenerator.Start();

            List<TestedPackage> testedVersions = new();

            foreach (var entry in entries)
            {
                var mode = _getCooldownMode(entry.NugetPackageSearchName);
                var project = solution.GetProject(entry.SampleProjectName);

                var supportedTargetFrameworks = project
                                               .GetTargetFrameworks()
                                               .Select(x => (TargetFramework)new TargetFramework.TargetFrameworkTypeConverter().ConvertFrom(x));
                var requiresDockerDependency = project.RequiresDockerDependency().ToString();

                // Freeze: re-emit whatever versions were in the previous output, without re-querying NuGet.
                // We look them up from the xunit files (the narrowest, most regular format) and feed
                // them back through Write() -- the generators are deterministic, so identical inputs
                // reproduce identical output. If the previous output is missing (e.g. a newly-added
                // entry), fall through to Normal so we don't silently drop it.
                if (mode is CooldownMode.Freeze
                    && _latestMinors.TryGetFrozenVersions(entry.IntegrationName, out var frozenMinors)
                    && _latestMajors.TryGetFrozenVersions(entry.IntegrationName, out var frozenMajors)
                    && _latestSpecific.TryGetFrozenVersions(entry.IntegrationName, out var frozenSpecific))
                {
                    _latestMinors.Write(entry, frozenMinors, requiresDockerDependency);
                    _latestMajors.Write(entry, frozenMajors, requiresDockerDependency);
                    _latestSpecific.Write(entry, frozenSpecific, requiresDockerDependency);
                    _strategyGenerator.Write(entry, null, requiresDockerDependency);
                    continue;
                }

                if (mode is CooldownMode.Freeze)
                {
                    Logger.Warning(
                        "Freeze requested for {Package} ({Integration}) but no existing output was found; switching to Normal cooldown mode",
                        entry.NugetPackageSearchName,
                        entry.IntegrationName);
                    mode = CooldownMode.Normal;
                }

                // Get all versions for this package (unfiltered), dedup across entries that share a package name
                if (!QueriedVersions.TryGetValue(entry.NugetPackageSearchName, out var allPackageVersions))
                {
                    allPackageVersions = await NuGetPackageHelper.GetAllNugetPackageVersions(entry.NugetPackageSearchName);
                    QueriedVersions[entry.NugetPackageSearchName] = allPackageVersions;
                }

                // Filter to this entry's version range, then (for Normal) remove recently-published versions.
                // BypassCooldown mode skips the cooldown filter entirely.
                var packageVersions = NuGetPackageHelper.FilterVersions(allPackageVersions, entry);
                if (mode is CooldownMode.Normal)
                {
                    packageVersions = ApplyCooldown(entry, packageVersions);
                }

                var orderedPackageVersions =
                    packageVersions
                       .Select(v => v.Version)
                       .Distinct()
                       .Select(versionText => new Version(versionText));

                var orderedWithFramework = (
                    from version in orderedPackageVersions.OrderBy(x => x)
                    from framework in supportedTargetFrameworks
                    where IsSupported(entry, version.ToString(), framework)
                    select (version, framework))
                   .ToList();

                // Add the last for every minor
                var latestMajors = SelectMax(orderedWithFramework, v => v.Major).ToList();
                var latestMinors = SelectMax(orderedWithFramework, v => $"{v.Major}.{v.Minor}").ToList();
                var latestSpecific = entry.SpecificVersions.Length == 0
                    ? latestMajors
                    : SelectPackagesFromGlobs(orderedWithFramework, entry.SpecificVersions).ToList();

                _latestMinors.Write(entry, latestMinors, requiresDockerDependency);
                _latestMajors.Write(entry, latestMajors, requiresDockerDependency);
                _latestSpecific.Write(entry, latestSpecific, requiresDockerDependency);

                _strategyGenerator.Write(entry, null, requiresDockerDependency);

                // we test the cooldown-filtered latestSpecific versions
                var allVersions = latestSpecific
                    .SelectMany(x => x.versions)
                    .OrderBy(x => x.Major)
                    .ThenBy(x => x.Minor)
                    .ThenBy(x => x.Revision)
                    .ToList();

                if (allVersions.Count > 0)
                {
                    var earliestVersion = allVersions.First();
                    var lastVersion = allVersions.Last();
                    testedVersions.Add(new(entry.NugetPackageSearchName, earliestVersion, lastVersion));
                }
            }

            _latestMinors.Finish();
            _latestMajors.Finish();
            _latestSpecific.Finish();
            _strategyGenerator.Finish();
            return testedVersions;
        }

        /// <summary>
        /// Drops versions that were published too recently to have been vetted, recording each
        /// dropped version in the cooldown report. Versions at or below the previous max (the highest
        /// version we shipped against in a prior run for this entry's range) are kept regardless,
        /// so the cooldown never downgrades an already-tested version.
        /// Only called for CooldownMode.Normal entries; Freeze and BypassCooldown are handled by the caller.
        /// </summary>
        private List<VersionWithDate> ApplyCooldown(PackageVersionEntry entry, List<VersionWithDate> versions)
        {
            var previousMax = FindPreviousMaxForEntry(entry);
            var result = new List<VersionWithDate>();

            foreach (var v in versions)
            {
                var publishedTooRecently = WasPublishedTooRecently(v.Published);
                var atOrBelowPreviousMax = previousMax is not null && new Version(v.Version) <= previousMax;

                if (publishedTooRecently && !atOrBelowPreviousMax)
                {
                    CooldownReport.Add(new CooldownReport.CooldownEntry(
                        entry.NugetPackageSearchName,
                        entry.IntegrationName,
                        v.Version,
                        v.Published));
                }
                else
                {
                    result.Add(v);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the highest previously-tested version for the given entry, restricted to its
        /// [MinVersion, MaxVersionExclusive) range. The range restriction matters for split-range
        /// packages (e.g. GraphQL 4.x-6.x and 7.x-9.x): a high max from the 7.x-9.x entry must not
        /// suppress cooldown checks on the 4.x-6.x entry.
        /// </summary>
        private Version FindPreviousMaxForEntry(PackageVersionEntry entry)
        {
            if (!_previousMaxVersions.TryGetValue(entry.NugetPackageSearchName, out var versions))
            {
                return null;
            }

            var min = new Version(entry.MinVersion);
            var max = new Version(entry.MaxVersionExclusive);

            return versions
                .Where(v => v >= min && v < max)
                .OrderByDescending(v => v)
                .FirstOrDefault();
        }

        private bool WasPublishedTooRecently(DateTimeOffset? publishedDate)
        {
            // Null published date means the package predates NuGet's publish-date tracking -- treat as old.
            return publishedDate.HasValue && publishedDate.Value > _cutoffDate;
        }

        static IEnumerable<(TargetFramework framework, IEnumerable<Version> versions)> SelectMax<T>(
            IEnumerable<(Version version, TargetFramework framework)> orderedPackageVersionsByFramework,
            Func<Version, T> groupBy)
        {
            return orderedPackageVersionsByFramework
                  .GroupBy(x => x.framework)
                  .Select(x =>
                   {
                       IEnumerable<Version> versions = x
                                     .Select(v => v.version)
                                     .GroupBy(groupBy)
                                     .Select(group => group.Max())
                                     .Distinct()
                                     .OrderBy(v => v)
                                     .ToList();
                       return (x.Key, versions);
                   });
        }

        static IEnumerable<(TargetFramework framework, IEnumerable<Version> versions)> SelectPackagesFromGlobs(
            IEnumerable<(Version version, TargetFramework framework)> orderedPackageVersionsByFramework,
            string[] versionGlobs)
        {
            return orderedPackageVersionsByFramework
                  .GroupBy(x => x.framework)
                  .Select(x =>
                   {
                       // going to iterate multiple times
                       var allPackageVersions = x.Select(v => v.version).ToList();

                       IEnumerable<Version> versions = versionGlobs
                            .Select(glob => GetBestVersion(glob, allPackageVersions))
                            .Where(x => x != null)
                            .Distinct()
                            .ToList();
                       return (x.Key, versions);
                   });
        }

        private static Version GetBestVersion(string glob, IEnumerable<Version> packageVersions)
        {
            // assume correct specification, ie. x.y.z/x.y.*/x.*.*/*.*.*
            var wildcardCount = glob.Count(x => x == '*');

            var effectiveMin = new Version(glob.Replace("*", "0"));
            var effectiveMax = new Version(glob.Replace("*", "65535"));

            return packageVersions
                .Where(x => x >= effectiveMin && x <= effectiveMax)
                .OrderByDescending(x => x)
                .FirstOrDefault();
        }

        private bool IsSupported(PackageVersionEntry entry, string packageVersion, TargetFramework targetFramework)
        {
            return entry
                .VersionConditions
                .Where(condition => AppliesToPackageVersion(entry, packageVersion, condition))
                .All(condition => ShouldIncludeFramework(condition, targetFramework));

            static bool ShouldIncludeFramework(
                PackageVersionEntry.PackageVersionConditionEntry condition,
                TargetFramework targetFramework)
            {
                if (condition is null)
                {
                    return true;
                }

                if (condition.ExcludeTargetFrameworks.Any()
                    && condition.ExcludeTargetFrameworks.Contains(targetFramework))
                {
                    return false;
                }

                if (condition.IncludeOnlyTargetFrameworks.Any()
                    && !condition.IncludeOnlyTargetFrameworks.Contains(targetFramework))
                {
                    return false;
                }

                return true;
            }

            static bool AppliesToPackageVersion(
                PackageVersionEntry entry,
                string packageVersionText,
                PackageVersionEntry.PackageVersionConditionEntry condition)
            {
                var packageVersion = new Version(packageVersionText);
                if (!Version.TryParse(condition.MinVersion, out var minSemanticVersion))
                {
                    minSemanticVersion = new Version(entry.MinVersion);
                }

                if (!Version.TryParse(condition.MaxVersionExclusive, out var maxSemanticVersion))
                {
                    maxSemanticVersion = new Version(entry.MaxVersionExclusive);
                }
                return packageVersion >= minSemanticVersion && packageVersion < maxSemanticVersion;
            }
        }

        private class PackageGroup
        {
            private readonly MSBuildPropsFileGenerator _msBuildPropsFileGenerator;
            private readonly XUnitFileGenerator _xUnitFileGenerator;
            private readonly Dictionary<string, List<(TargetFramework Framework, IEnumerable<Version> Versions)>> _frozenVersions;

            public PackageGroup(string propsDirectory, string testDirectoryPath, string postfix)
            {
                var className = $"PackageVersions{postfix}";
                var propsFilename = Path.Combine(propsDirectory, $"PackageVersions{postfix}.g.props");
                var xunitFilename = Path.Combine(testDirectoryPath, $"PackageVersions{postfix}.g.cs");

                _msBuildPropsFileGenerator = new MSBuildPropsFileGenerator(propsFilename);
                _xUnitFileGenerator = new XUnitFileGenerator(xunitFilename, className);

                // Load before Start() overwrites anything, so Freeze can recover the previous versions.
                _frozenVersions = _xUnitFileGenerator.LoadExistingVersions();
            }

            public bool TryGetFrozenVersions(
                string integrationName,
                out IEnumerable<(TargetFramework framework, IEnumerable<Version> versions)> versions)
            {
                if (_frozenVersions.TryGetValue(integrationName, out var loaded))
                {
                    versions = loaded;
                    return true;
                }

                versions = null;
                return false;
            }

            public void Start()
            {
                _msBuildPropsFileGenerator.Start();
                _xUnitFileGenerator.Start();
            }

            public void Write(PackageVersionEntry entry, IEnumerable<(TargetFramework framework, IEnumerable<Version> versions)> versions, string requiresDockerDependency)
            {
                _msBuildPropsFileGenerator.Write(entry, versions, requiresDockerDependency);
                _xUnitFileGenerator.Write(entry, versions, requiresDockerDependency);
            }

            public void Finish()
            {
                _msBuildPropsFileGenerator.Finish();
                _xUnitFileGenerator.Finish();
            }
        }

        public record TestedPackage(string NugetPackageSearchName, Version MinVersion, Version MaxVersion);
    }
}
