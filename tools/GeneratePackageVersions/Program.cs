using System;
using System.Collections.Generic;
using System.IO;
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

            PackageVersionEntry[] entries = JsonConvert.DeserializeObject<PackageVersionEntry[]>(File.ReadAllText(definitionsFilename));
            await RunFileGeneratorWithPackageEntries(new MSBuildPropsFileGenerator(outputPackageVersionsPropsFilename), entries);
            await RunFileGeneratorWithPackageEntries(new XUnitFileGenerator(outputPackageVersionsXunitFilename), entries);
        }

        private static async Task RunFileGeneratorWithPackageEntries(FileGenerator fileGenerator, IEnumerable<PackageVersionEntry> entries)
        {
            fileGenerator.Start();

            foreach (PackageVersionEntry entry in entries)
            {
                var packageVersions = await NuGetPackageHelper.GetNugetPackageVersions(entry);
                fileGenerator.Write(packageVersionEntry: entry, packageVersions: packageVersions);
            }

            fileGenerator.Finish();
        }
    }
}
