using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace GeneratePackageVersions
{
    public class Program
    {
        private static string _solutionDirectory;
        private static string _baseXunitPath;
        private static PackageGroup _latestMinors;
        private static PackageGroup _comprehensive;
        private static XunitStrategyFileGenerator _strategyGenerator;

        public static async Task Main(string[] args)
        {
            if (args.Length < 1 || string.IsNullOrWhiteSpace(args[0]))
            {
                Console.Error.WriteLine("error: You must specify the solution directory. Exiting.");
                return;
            }

            _solutionDirectory = args[0];

            _baseXunitPath = Path.Combine(
                _solutionDirectory,
                "test",
                "Datadog.Trace.ClrProfiler.IntegrationTests");

            _latestMinors = new PackageGroup("LatestMinors");
            _comprehensive = new PackageGroup("Comprehensive");

            _strategyGenerator = new XunitStrategyFileGenerator(
                Path.Combine(
                    _baseXunitPath,
                    "PackageVersions.g.cs"));

            var definitionsFilename = Path.Combine(args[0], "PackageVersionsGeneratorDefinitions.json");

            if (!File.Exists(definitionsFilename))
            {
                Console.Error.WriteLine($"error: Definitions file {definitionsFilename} does not exist. Exiting.");
                return;
            }

            var entries = JsonConvert.DeserializeObject<PackageVersionEntry[]>(File.ReadAllText(definitionsFilename));
            await RunFileGeneratorWithPackageEntries(entries);
        }

        private static async Task RunFileGeneratorWithPackageEntries(IEnumerable<PackageVersionEntry> entries)
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

            public PackageGroup(string postfix)
            {
                var className = $"PackageVersions{postfix}";

                var outputPackageVersionsPropsFilename = Path.Combine(_solutionDirectory, $"PackageVersions{postfix}.g.props");

                var outputPackageVersionsXunitFilename = Path.Combine(
                    _baseXunitPath,
                    $"PackageVersions{postfix}.g.cs");

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
