using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeneratePackageVersions
{
    internal class GenerateIntegrationMarkdownTable
    {
        private class Package
        {
            [JsonPropertyName("name")]
            public string Name { get; set; }

            [JsonPropertyName("minVersionTestedInclusive")]
            public string MinVersionTested { get; set; }

            [JsonPropertyName("maxVersionTestedInclusive")]
            public string MaxVersionTested { get; set; }
        }

        private class Integration
        {
            [JsonPropertyName("integrationName")]
            public string IntegrationName { get; set; }

            [JsonPropertyName("assemblyName")]
            public string AssemblyName { get; set; }

            [JsonPropertyName("minAssemblyVersionInclusive")]
            public string MinAssemblyVersion { get; set; }

            [JsonPropertyName("maxAssemblyVersionInclusive")]
            public string MaxAssemblyVersion { get; set; }

            [JsonPropertyName("packages")]
            public List<Package> Packages { get; set; }
        }

        public static void GenerateTable(string input, string output)
        {
            var jsonContent = File.ReadAllText(input);
            var integrations = JsonSerializer.Deserialize<List<Integration>>(jsonContent);

            var distinctIntegrations = integrations
                .GroupBy(i => i.IntegrationName)
                .Select(group =>
                {
                    var assemblies = group.Select(i => new
                    {
                        Name = i.AssemblyName,
                        MinVersion = i.MinAssemblyVersion,
                        MaxVersion = i.MaxAssemblyVersion,
                        Packages = i.Packages ?? new List<Package>()
                    }).ToList();

                    return new
                    {
                        IntegrationName = group.Key,
                        Implementations = assemblies.Select(assembly => new
                        {
                            Type = "Assembly",
                            Name = assembly.Name,
                            MinVersion = assembly.MinVersion,
                            MaxVersion = assembly.MaxVersion
                        })
                        .Concat(
                            assemblies.SelectMany(a => a.Packages)
                                .Where(p => p.MinVersionTested != null || p.MaxVersionTested != null)
                                .Select(p => new
                                {
                                    Type = "Package",
                                    Name = p.Name,
                                    MinVersion = p.MinVersionTested ?? "-",
                                    MaxVersion = p.MaxVersionTested ?? "-"
                                })
                        )
                        .OrderBy(x => x.Type)
                        .ThenBy(x => x.Name)
                        .ToList()
                    };
                })
                .OrderBy(i => i.IntegrationName);

            using var writer = new StreamWriter(output);
            writer.WriteLine("| Integration | Type | Name | Minimum Version | Maximum Version |");
            writer.WriteLine("|------------|------|------|----------------|----------------|");

            foreach (var integration in distinctIntegrations)
            {
                if (!integration.Implementations.Any())
                {
                    // Write a single row for integrations with no implementations
                    writer.WriteLine($"| {integration.IntegrationName} | - | - | - | - |");
                }
                else
                {
                    // Write a row for each implementation (both assemblies and packages)
                    var isFirst = true;
                    foreach (var impl in integration.Implementations)
                    {
                        var integrationName = isFirst ? integration.IntegrationName : "";
                        writer.WriteLine($"| {integrationName} | {impl.Type} | {impl.Name} | {impl.MinVersion} | {impl.MaxVersion} |");
                        isFirst = false;
                    }
                }
            }
        }
    }
}
