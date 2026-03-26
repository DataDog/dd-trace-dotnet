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

namespace GeneratePackageVersions
{
    public class PackageVersionGenerator
    {
        private readonly Func<string, bool> _shouldQueryNuGet;
        private readonly AbsolutePath _definitionsFilePath;
        private readonly PackageGroup _latestMinors;
        private readonly PackageGroup _latestMajors;
        private readonly PackageGroup _latestSpecific;
        private readonly XunitStrategyFileGenerator _strategyGenerator;
        private readonly DateTimeOffset _cutoffDate;
        private readonly Dictionary<string, Version> _baseline;

        /// <summary>
        /// The version cache, populated during generation. Entries are added for every
        /// package that is queried from NuGet. Pre-populated with cached entries on construction.
        /// </summary>
        public Dictionary<string, List<VersionWithDate>> VersionCache { get; }

        /// <summary>
        /// Report of package versions that were excluded by the cooldown filter.
        /// </summary>
        public CooldownReport CooldownReport { get; }


        public PackageVersionGenerator(
            AbsolutePath tracerDirectory,
            AbsolutePath testProjectDirectory,
            Func<string, bool> shouldQueryNuGet,
            Dictionary<string, List<VersionWithDate>> previousVersionCache,
            int cooldownDays,
            Dictionary<string, Version> baseline)
        {
            _shouldQueryNuGet = shouldQueryNuGet;
            VersionCache = new Dictionary<string, List<VersionWithDate>>(previousVersionCache);
            _cutoffDate = DateTimeOffset.UtcNow.AddDays(-cooldownDays);
            _baseline = baseline;
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
                var project = solution.GetProject(entry.SampleProjectName);
                var supportedTargetFrameworks = project
                                               .GetTargetFrameworks()
                                               .Select(x => (TargetFramework)new TargetFramework.TargetFrameworkTypeConverter().ConvertFrom(x));

                var requiresDockerDependency = project.RequiresDockerDependency().ToString();

                // Get all versions for this package (unfiltered), using cache when possible
                List<VersionWithDate> allPackageVersions;
                if (!_shouldQueryNuGet(entry.NugetPackageSearchName)
                    && VersionCache.TryGetValue(entry.NugetPackageSearchName, out var cached))
                {
                    allPackageVersions = cached;
                }
                else
                {
                    allPackageVersions = await NuGetPackageHelper.GetAllNugetPackageVersions(entry.NugetPackageSearchName);
                    VersionCache[entry.NugetPackageSearchName] = allPackageVersions;
                }

                // Filter to this entry's version range
                var packageVersions = NuGetPackageHelper.FilterVersions(allPackageVersions, entry);

                // Build a publish-date lookup for cooldown filtering
                var publishDateLookup = packageVersions
                    .GroupBy(v => v.Version)
                    .ToDictionary(g => g.Key, g => g.First().Published);

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

                // Apply cooldown: override versions that are too new with older resolved versions
                latestMajors = ApplyCooldown(entry, latestMajors, orderedWithFramework, publishDateLookup, v => v.Major).ToList();
                latestMinors = ApplyCooldown(entry, latestMinors, orderedWithFramework, publishDateLookup, v => $"{v.Major}.{v.Minor}").ToList();
                latestSpecific = ApplyCooldown(entry, latestSpecific, orderedWithFramework, publishDateLookup, v => v.Major).ToList();

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
        /// Applies cooldown filtering to selected versions. For each version that was published
        /// within the cooldown period, checks if it was already accepted (at or below baseline).
        /// If already accepted, keeps it. Otherwise, finds the best resolved version that is
        /// either outside the cooldown or at the baseline. Overridden versions are added to CooldownReport.
        /// </summary>
        private List<(TargetFramework framework, IEnumerable<Version> versions)> ApplyCooldown<T>(
            PackageVersionEntry entry,
            List<(TargetFramework framework, IEnumerable<Version> versions)> selectedVersions,
            List<(Version version, TargetFramework framework)> allOrderedVersions,
            Dictionary<string, DateTimeOffset?> publishDateLookup,
            Func<Version, T> groupBy)
        {
            _baseline.TryGetValue(entry.NugetPackageSearchName, out var baselineVersion);

            var result = new List<(TargetFramework framework, IEnumerable<Version> versions)>();

            foreach (var (framework, versions) in selectedVersions)
            {
                // All versions available for this framework, grouped the same way as selection
                var allForFramework = allOrderedVersions
                    .Where(x => x.framework == framework)
                    .Select(x => x.version)
                    .ToList();

                var filteredVersions = new List<Version>();
                foreach (var version in versions)
                {
                    var versionKey = version.ToString();
                    publishDateLookup.TryGetValue(versionKey, out var publishedDate);

                    if (!IsWithinCooldown(publishedDate))
                    {
                        // Outside cooldown -- always accept
                        filteredVersions.Add(version);
                        continue;
                    }

                    if (baselineVersion is not null && version <= baselineVersion)
                    {
                        // Within cooldown but already accepted in a previous run -- keep it
                        filteredVersions.Add(version);
                        continue;
                    }

                    // Within cooldown and above baseline -- override to the best available:
                    // the baseline version if it exists in this group, otherwise the latest
                    // version outside cooldown
                    var groupKey = groupBy(version);
                    var versionsInGroup = allForFramework
                        .Where(v => EqualityComparer<T>.Default.Equals(groupBy(v), groupKey))
                        .Where(v => v < version)
                        .OrderByDescending(v => v);

                    // Prefer baseline version if it's in this group
                    Version resolved = null;
                    if (baselineVersion is not null)
                    {
                        resolved = versionsInGroup.FirstOrDefault(v => v <= baselineVersion);
                    }

                    // Fall back to latest outside cooldown
                    resolved ??= versionsInGroup.FirstOrDefault(v =>
                    {
                        publishDateLookup.TryGetValue(v.ToString(), out var rDate);
                        return !IsWithinCooldown(rDate);
                    });

                    CooldownReport.Add(new CooldownReport.CooldownEntry(
                        entry.NugetPackageSearchName,
                        entry.IntegrationName,
                        versionKey,
                        publishedDate,
                        resolved?.ToString()));

                    if (resolved is not null)
                    {
                        filteredVersions.Add(resolved);
                    }
                }

                if (filteredVersions.Count > 0)
                {
                    result.Add((framework, filteredVersions.Distinct().OrderBy(v => v).ToList()));
                }
            }

            return result;
        }

        private bool IsWithinCooldown(DateTimeOffset? publishedDate)
        {
            // Null published date means the package predates NuGet tracking -- treat as safe
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

            public PackageGroup(string propsDirectory, string testDirectoryPath, string postfix)
            {
                var className = $"PackageVersions{postfix}";

                var outputPackageVersionsPropsFilename = Path.Combine(propsDirectory, $"PackageVersions{postfix}.g.props");

                var outputPackageVersionsXunitFilename = Path.Combine(testDirectoryPath, $"PackageVersions{postfix}.g.cs");

                _msBuildPropsFileGenerator = new MSBuildPropsFileGenerator(outputPackageVersionsPropsFilename);

                _xUnitFileGenerator = new XUnitFileGenerator(outputPackageVersionsXunitFilename, className);
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
