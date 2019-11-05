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
        public static async Task Main(string[] args)
        {
            string definitionsFilename = "PackageVersionsGeneratorDefinitions.json";
            string outputPackageVersionsPropsFilename = "PackageVersions.g.props";
            string outputPackageVersionsXunitFilename = "PackageVersions.g.cs";

            if (args.Length != 3)
            {
                Console.Error.WriteLine("error: Incorrect number of program arguments");
                Console.Error.WriteLine($"error: args = {args}");
                Console.Error.WriteLine($"error: Usage: {nameof(GeneratePackageVersions)} <{definitionsFilename}> <{outputPackageVersionsPropsFilename}> <{outputPackageVersionsXunitFilename}>");
                return;
            }

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                definitionsFilename = args[0];
            }

            if (args.Length > 1 && !string.IsNullOrWhiteSpace(args[1]))
            {
                outputPackageVersionsPropsFilename = args[1];
            }

            if (args.Length > 2 && !string.IsNullOrWhiteSpace(args[2]))
            {
                outputPackageVersionsXunitFilename = args[2];
            }

            if (!File.Exists(definitionsFilename))
            {
                Console.Error.WriteLine($"error: Definitions file {definitionsFilename} does not exist. Exiting.");
                return;
            }

            var entries = JsonConvert.DeserializeObject<PackageVersionEntry[]>(File.ReadAllText(definitionsFilename));
            await RunFileGeneratorWithPackageEntries(new MSBuildPropsFileGenerator(outputPackageVersionsPropsFilename), entries);
            await RunFileGeneratorWithPackageEntries(new XUnitFileGenerator(outputPackageVersionsXunitFilename), entries);
        }

        private static async Task RunFileGeneratorWithPackageEntries(FileGenerator fileGenerator, IEnumerable<PackageVersionEntry> entries)
        {
            fileGenerator.Start();

            foreach (var entry in entries)
            {
                var packageVersions = await NuGetPackageHelper.GetNugetPackageVersions(entry);
                var typedVersions =
                    packageVersions
                       .Select(versionText => new Version(versionText))
                       .OrderBy(v => v.Major)
                       .ThenBy(v => v.Minor)
                       .ThenBy(v => v.Revision)
                       .ThenBy(v => v.Build);

                var versionsToInclude = new HashSet<string>();

                // Add the first for every major
                // Add the last for every minor

                var majorGroups = typedVersions.GroupBy(v => v.Major);

                foreach (var majorGroup in majorGroups)
                {
                    versionsToInclude.Add(majorGroup.First().ToString());

                    var minorGroups = majorGroup.GroupBy(v => v.Minor);
                    foreach (var minorGroup in minorGroups)
                    {
                        versionsToInclude.Add(minorGroup.Last().ToString());
                    }
                }

                fileGenerator.Write(packageVersionEntry: entry, packageVersions: versionsToInclude);
            }

            fileGenerator.Finish();
        }
    }
}
