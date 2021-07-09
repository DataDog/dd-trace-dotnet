// <copyright file="Program.cs" company="Datadog">
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

namespace GeneratePackageVersions
{
    public class PackageVersionGenerator
    {
        private readonly AbsolutePath _definitionsFilePath;
        private readonly PackageGroup _latestMinors;
        private readonly PackageGroup _comprehensive;
        private readonly XunitStrategyFileGenerator _strategyGenerator;

        public PackageVersionGenerator(
            AbsolutePath solutionDirectory,
            AbsolutePath testProjectDirectory)
        {
            var propsDirectory = solutionDirectory / "build";
            _definitionsFilePath = solutionDirectory / "build" / "PackageVersionsGeneratorDefinitions.json";
            _latestMinors = new PackageGroup(propsDirectory, testProjectDirectory, "LatestMinors");
            _comprehensive = new PackageGroup(propsDirectory, testProjectDirectory, "Comprehensive");
            _strategyGenerator = new XunitStrategyFileGenerator(testProjectDirectory / "PackageVersions.g.cs");

            if (!File.Exists(_definitionsFilePath))
            {
                throw new Exception($"Definitions file {_definitionsFilePath} does not exist. Exiting.");
            }
        }

        public async Task GenerateVersions()
        {
            var definitions = File.ReadAllText(_definitionsFilePath);
            var entries = JsonConvert.DeserializeObject<PackageVersionEntry[]>(definitions);
            await RunFileGeneratorWithPackageEntries(entries);
        }

        private async Task RunFileGeneratorWithPackageEntries(IEnumerable<PackageVersionEntry> entries)
        {
            _latestMinors.Start();
            _comprehensive.Start();
            _strategyGenerator.Start();

            foreach (var entry in entries)
            {
                Version entryNetCoreMinVersion;
                if (!Version.TryParse(entry.MinVersionNetCore, out entryNetCoreMinVersion))
                {
                    entryNetCoreMinVersion = new Version("0.0.0.0");
                }

                var packageVersions = await NuGetPackageHelper.GetNugetPackageVersions(entry);
                var orderedPackageVersions =
                    packageVersions
                       .Distinct()
                       .Select(versionText => new Version(versionText))
                       .OrderBy(v => v.Major)
                       .ThenBy(v => v.Minor)
                       .ThenBy(v => v.Revision)
                       .ThenBy(v => v.Build)
                       .ToList();

                // Add the last for every minor
                var orderedLastMinorPackageVersions = orderedPackageVersions
                    .GroupBy(v => $"{v.Major}.{v.Minor}")
                    .Select(group => group.Max());

                var allNetFrameworkVersions = orderedPackageVersions
                    .Where(v => v.CompareTo(entryNetCoreMinVersion) < 0)
                    .Select(v => v.ToString());

                var allNetCoreVersions = orderedPackageVersions
                    .Where(v => v.CompareTo(entryNetCoreMinVersion) >= 0)
                    .Select(v => v.ToString());

                var lastMinorNetFrameworkVersions = orderedLastMinorPackageVersions
                    .Where(v => v.CompareTo(entryNetCoreMinVersion) < 0)
                    .Select(v => v.ToString());

                var lastMinorNetCoreVersions = orderedLastMinorPackageVersions
                    .Where(v => v.CompareTo(entryNetCoreMinVersion) >= 0)
                    .Select(v => v.ToString());

                _latestMinors.Write(entry, lastMinorNetFrameworkVersions, lastMinorNetCoreVersions);
                _comprehensive.Write(entry, allNetFrameworkVersions, allNetCoreVersions);
                _strategyGenerator.Write(entry, null, null);
            }

            _latestMinors.Finish();
            _comprehensive.Finish();
            _strategyGenerator.Finish();
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

            public void Write(PackageVersionEntry entry, IEnumerable<string> netFrameworkversions, IEnumerable<string> netCoreVersions)
            {
                _msBuildPropsFileGenerator.Write(entry, netFrameworkversions, netCoreVersions);
                _xUnitFileGenerator.Write(entry, netFrameworkversions, netCoreVersions);
            }

            public void Finish()
            {
                _msBuildPropsFileGenerator.Finish();
                _xUnitFileGenerator.Finish();
            }
        }
    }
}
