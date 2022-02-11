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
        private readonly AbsolutePath _definitionsFilePath;
        private readonly PackageGroup _latestMinors;
        private readonly PackageGroup _latestMajors;
        private readonly XunitStrategyFileGenerator _strategyGenerator;

        public PackageVersionGenerator(
            AbsolutePath tracerDirectory,
            AbsolutePath testProjectDirectory)
        {
            var propsDirectory = tracerDirectory / "build";
            _definitionsFilePath = tracerDirectory / "build" / "PackageVersionsGeneratorDefinitions.json";
            _latestMinors = new PackageGroup(propsDirectory, testProjectDirectory, "LatestMinors");
            _latestMajors = new PackageGroup(propsDirectory, testProjectDirectory, "LatestMajors");
            _strategyGenerator = new XunitStrategyFileGenerator(testProjectDirectory / "PackageVersions.g.cs");

            if (!File.Exists(_definitionsFilePath))
            {
                throw new Exception($"Definitions file {_definitionsFilePath} does not exist. Exiting.");
            }
        }

        public async Task GenerateVersions(Solution solution)
        {
            var definitions = File.ReadAllText(_definitionsFilePath);
            var entries = JsonConvert.DeserializeObject<PackageVersionEntry[]>(definitions);
            await RunFileGeneratorWithPackageEntries(entries, solution);
        }

        private async Task RunFileGeneratorWithPackageEntries(IEnumerable<PackageVersionEntry> entries, Solution solution)
        {
            _latestMinors.Start();
            _latestMajors.Start();
            _strategyGenerator.Start();

            foreach (var entry in entries)
            {
                var supportedTargetFrameworks = solution
                                               .GetProject(entry.SampleProjectName)
                                               .GetTargetFrameworks()
                                               .Select(x => (TargetFramework)new TargetFramework.TargetFrameworkTypeConverter().ConvertFrom(x));

                var packageVersions = await NuGetPackageHelper.GetNugetPackageVersions(entry);
                var orderedPackageVersions =
                    packageVersions
                       .Distinct()
                       .Select(versionText => new Version(versionText))
                       .OrderBy(v => v)
                       .ToList();

                var orderedWithFramework = (
                    from version in orderedPackageVersions
                    from framework in supportedTargetFrameworks
                    where IsSupported(entry, version.ToString(), framework)
                    select (version, framework))
                   .ToList();

                // Always include some specific versions, to give us better coverage
                var alwaysIncludeVersions = entry.AlwaysIncludeVersions.Select(x => new Version(x));

                // Add the last for every minor
                var latestMinors = SelectMax(orderedWithFramework, v => $"{v.Major}.{v.Minor}", Array.Empty<Version>());
                var latestMajors = SelectMax(orderedWithFramework, v => v.Major, alwaysIncludeVersions);

                _latestMinors.Write(entry, latestMinors);
                _latestMajors.Write(entry, latestMajors);
                _strategyGenerator.Write(entry, null);
            }

            _latestMinors.Finish();
            _latestMajors.Finish();
            _strategyGenerator.Finish();
        }

        static IEnumerable<(TargetFramework framework, IEnumerable<Version> versions)> SelectMax<T>(
            IEnumerable<(Version version, TargetFramework framework)> orderedPackageVersionsByFramework,
            Func<Version, T> groupBy,
            IEnumerable<Version> alwaysIncludeVersions)
        {
            return orderedPackageVersionsByFramework
                  .GroupBy(x => x.framework)
                  .Select(x =>
                   {
                       var framework = x.Key;
                       var requiredVersions = x
                                             .Where(v => alwaysIncludeVersions.Contains(v.version))
                                             .Select(v => v.version);
                       var versions = x
                                     .Select(v => v.version)
                                     .GroupBy(groupBy)
                                     .Select(group => group.Max());

                       IEnumerable<Version> finalOrderedVersions = requiredVersions
                                                                  .Concat(versions)
                                                                  .Distinct()
                                                                  .OrderBy(v => v);
                       return (framework, finalOrderedVersions);
                   });
        }

        private bool IsSupported(PackageVersionEntry entry, string packageVersion, TargetFramework targetFramework)
        {
            var condition = entry
                  .VersionConditions
                  .FirstOrDefault(condition => AppliesToPackageVersion(packageVersion, condition));

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


            bool AppliesToPackageVersion(
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

            public void Write(PackageVersionEntry entry, IEnumerable<(TargetFramework framework, IEnumerable<Version> versions)> versions)
            {
                _msBuildPropsFileGenerator.Write(entry, versions);
                _xUnitFileGenerator.Write(entry, versions);
            }

            public void Finish()
            {
                _msBuildPropsFileGenerator.Finish();
                _xUnitFileGenerator.Finish();
            }
        }
    }
}
