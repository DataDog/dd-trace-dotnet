using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Nuke.Common.IO;
using static Nuke.Common.IO.FileSystemTasks;
using Logger = Serilog.Log;

namespace CodeGenerators
{
    /// <summary>
    /// Generates CSV files containing information about supported integrations from the supported_versions.json file.
    /// </summary>
    public static class SupportedIntegrationsTableGenerator
    {
        public class SupportedVersion
        {
            public string IntegrationName { get; set; }
            public string AssemblyName { get; set; }
            public string MinAssemblyVersionInclusive { get; set; }
            public string MaxAssemblyVersionInclusive { get; set; }
            public List<Package> Packages { get; set; }
        }

        public class Package
        {
            public string Name { get; set; }
            public string MinVersionAvailableInclusive { get; set; }
            public string MinVersionSupportedInclusive { get; set; }
            public string MinVersionTestedInclusive { get; set; }
            public string MaxVersionSupportedInclusive { get; set; }
            public string MaxVersionAvailableInclusive { get; set; }
            public string MaxVersionTestedInclusive { get; set; }
        }

        public class IntegrationInfo
        {
            public string IntegrationName { get; set; }
            public string DisplayName { get; set; }
            public string NuGetPackage { get; set; }
            public string AssemblyName { get; set; }
            public string MinVersion { get; set; }
            public string MaxVersion { get; set; }
            public bool IsBuiltIn { get; set; }
            public bool IsNetCore { get; set; }
            public bool IsNetFramework { get; set; }
        }

        // skip these integrations as they are helpers/utilities, not real integrations
        // Well OpenTelemetry is a real integration, but unsure how to present that
        private static readonly HashSet<string> HelperIntegrations = new()
        {
            "CallTargetNativeTest",
            "ServiceRemoting",
            "OpenTelemetry",
            "AssemblyResolve"
        };

        // User-friendly display names for integrations
        private static readonly Dictionary<string, string> IntegrationDisplayNames = new()
        {
            ["AdoNet"] = "ADO.NET",
            ["Aerospike"] = "Aerospike",
            ["AspNet"] = "ASP.NET",
            ["AspNetCore"] = "ASP.NET Core",
            ["AspNetMvc"] = "ASP.NET MVC",
            ["AspNetWebApi2"] = "ASP.NET Web API",
            ["AwsDynamoDb"] = "AWS DynamoDB",
            ["AwsKinesis"] = "AWS Kinesis",
            ["AwsLambda"] = "AWS Lambda",
            ["AwsSns"] = "AWS SNS",
            ["AwsSqs"] = "AWS SQS",
            ["AzureFunctions"] = "Azure Functions",
            ["CosmosDb"] = "Azure Cosmos DB",
            ["Couchbase"] = "Couchbase",
            ["DatadogTraceManual"] = "Manual Instrumentation",
            ["DiagnosticSource"] = "DiagnosticSource (Activity)",
            ["Elasticsearch"] = "Elasticsearch",
            ["GraphQL"] = "GraphQL .NET",
            ["Grpc"] = "gRPC",
            ["HotChocolate"] = "HotChocolate (GraphQL)",
            ["HttpMessageHandler"] = "HttpClient",
            ["ILogger"] = "ILogger",
            ["Kafka"] = "Kafka (Confluent)",
            ["MassTransit"] = "MassTransit",
            ["MongoDB"] = "MongoDB",
            ["Msmq"] = "MSMQ",
            ["MySql"] = "MySQL (MySql.Data)",
            ["MySqlData"] = "MySQL (MySqlConnector)",
            ["NLog"] = "NLog",
            ["Npgsql"] = "PostgreSQL (Npgsql)",
            ["NServiceBus"] = "NServiceBus",
            ["NUnit"] = "NUnit",
            ["Oracle"] = "Oracle",
            ["Owin"] = "OWIN",
            ["Process"] = "Process (Command Injection)",
            ["RabbitMQ"] = "RabbitMQ",
            ["Registry"] = "Windows Registry (LFI)",
            ["Remoting"] = ".NET Remoting",
            ["Serilog"] = "Serilog",
            ["ServiceStackRedis"] = "Redis (ServiceStack)",
            ["StackExchangeRedis"] = "Redis (StackExchange)",
            ["Sqlite"] = "SQLite",
            ["SqlClient"] = "SQL Server",
            ["Ssrf"] = "SSRF Protection",
            ["StackTraceLeak"] = "StackTrace Leak Protection",
            ["TestPlatformAssemblyResolver"] = "Test Platform Assembly Resolver",
            ["Wcf"] = "WCF",
            ["WebRequest"] = "WebClient / WebRequest",
            ["Xss"] = "XSS Protection",
            ["XUnit"] = "xUnit",
            ["MsTestV2"] = "MSTest"
        };

        // not 100% sure on these TBH would have to check
        private static readonly HashSet<string> NetCoreOnlyIntegrations = new()
        {
            "AspNetCore",
            "Grpc",
            "HotChocolate"
        };

        private static readonly HashSet<string> NetFrameworkOnlyIntegrations = new()
        {
            "AspNet",
            "AspNetMvc",
            "AspNetWebApi2",
            "Msmq",
            "Owin",
            "Remoting",
            "Wcf"
        };

        // runtime stuff that _usually_ don't come from NuGet packages, but they could
        private static readonly HashSet<string> BuiltInAssemblies = new()
        {
            "System.Web",
            "System.Web.Mvc",
            "System.Web.Http",
            "System.Messaging",
            "System.Runtime.Remoting",
            "System.ServiceModel",
            "System.Diagnostics.Process",
            "System.Data",
            "System.Data.Common",
            "System.Data.SqlClient",
            "System.Net.Http",
            "System.Net.Requests",
            "Microsoft.Owin",
            "Owin",
            "netstandard"
        };

        /// <summary>
        /// Generates CSV files containing supported integrations information.
        /// </summary>
        /// <param name="supportedVersionsPath">Path to the supported_versions.json file</param>
        /// <param name="outputDirectory">Directory where CSV files will be saved</param>
        public static void GenerateCsvFiles(AbsolutePath supportedVersionsPath, AbsolutePath outputDirectory)
        {
            Logger.Information("Reading supported versions from {Path}", supportedVersionsPath);

            var supportedVersions = ReadSupportedVersions(supportedVersionsPath);
            var integrations = ProcessIntegrations(supportedVersions);

            GenerateOutputFiles(integrations, outputDirectory);
        }

        private static List<SupportedVersion> ReadSupportedVersions(AbsolutePath supportedVersionsPath)
        {
            var json = File.ReadAllText(supportedVersionsPath);
            return JsonSerializer.Deserialize<List<SupportedVersion>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private static void GenerateOutputFiles(List<IntegrationInfo> integrations, AbsolutePath outputDirectory)
        {
            // Generate .NET Core/.NET CSV
            GenerateCsv(
                integrations.Where(i => i.IsNetCore).OrderBy(i => i.DisplayName).ThenBy(i => i.NuGetPackage),
                outputDirectory / "supported_integrations_netcore.csv",
                ".NET Core / .NET");

            // Generate .NET Framework CSV
            GenerateCsv(
                integrations.Where(i => i.IsNetFramework).OrderBy(i => i.DisplayName).ThenBy(i => i.NuGetPackage),
                outputDirectory / "supported_integrations_netfx.csv",
                ".NET Framework");

            // Generate combined CSV
            GenerateCsv(
                integrations.OrderBy(i => i.DisplayName).ThenBy(i => i.NuGetPackage),
                outputDirectory / "supported_integrations.csv",
                "All Frameworks");
        }

        private static List<IntegrationInfo> ProcessIntegrations(List<SupportedVersion> supportedVersions)
        {
            var integrations = new List<IntegrationInfo>();

            foreach (var version in supportedVersions)
            {
                if (IsHelperIntegration(version.IntegrationName))
                    continue;

                var displayName = GetDisplayName(version.IntegrationName);

                if (version.Packages != null && version.Packages.Any())
                {
                    foreach (var package in version.Packages)
                    {
                        var info = CreateIntegrationInfo(version, package, displayName);
                        integrations.Add(info);
                    }
                }
                else
                {
                    var info = CreateIntegrationInfoFromAssembly(version, displayName);
                    integrations.Add(info);
                }
            }

            return integrations;
        }

        private static IntegrationInfo CreateIntegrationInfo(SupportedVersion version, Package package, string displayName)
        {
            return new IntegrationInfo
            {
                IntegrationName = version.IntegrationName,
                DisplayName = displayName,
                NuGetPackage = package.Name,
                AssemblyName = version.AssemblyName,
                MinVersion = GetVersionString(package.MinVersionSupportedInclusive ?? version.MinAssemblyVersionInclusive),
                MaxVersion = GetVersionString(package.MaxVersionSupportedInclusive ?? version.MaxAssemblyVersionInclusive),
                IsBuiltIn = IsBuiltInPackage(package.Name),
                IsNetCore = !NetFrameworkOnlyIntegrations.Contains(version.IntegrationName),
                IsNetFramework = !NetCoreOnlyIntegrations.Contains(version.IntegrationName)
            };
        }

        private static IntegrationInfo CreateIntegrationInfoFromAssembly(SupportedVersion version, string displayName)
        {
            var packageName = GetPackageNameForAssembly(version.IntegrationName, version.AssemblyName);

            return new IntegrationInfo
            {
                IntegrationName = version.IntegrationName,
                DisplayName = displayName,
                NuGetPackage = packageName,
                AssemblyName = version.AssemblyName,
                MinVersion = GetVersionString(version.MinAssemblyVersionInclusive),
                MaxVersion = GetVersionString(version.MaxAssemblyVersionInclusive),
                IsBuiltIn = IsBuiltInPackage(packageName),
                IsNetCore = !NetFrameworkOnlyIntegrations.Contains(version.IntegrationName),
                IsNetFramework = !NetCoreOnlyIntegrations.Contains(version.IntegrationName)
            };
        }

        private static bool IsHelperIntegration(string integrationName)
        {
            return HelperIntegrations.Contains(integrationName);
        }

        private static bool IsBuiltInPackage(string packageName)
        {
            return BuiltInAssemblies.Contains(packageName) ||
                   packageName.StartsWith("System.") ||
                   packageName == "netstandard";
        }

        private static string GetDisplayName(string integrationName)
        {
            return IntegrationDisplayNames.TryGetValue(integrationName, out var displayName)
                ? displayName
                : integrationName;
        }

        private static string GetPackageNameForAssembly(string integrationName, string assemblyName)
        {
            // For assemblies without NuGet packages, return the assembly name
            // The built-in status will be determined by IsBuiltInPackage
            return assemblyName;
        }

        private static string GetVersionString(string version)
        {
            if (string.IsNullOrEmpty(version))
                return "N/A";

            // Handle versions like "4.65535.65535" by showing just major version
            if (version.Contains("65535"))
            {
                var parts = version.Split('.');
                if (parts.Length > 0)
                {
                    return parts[0] + ".x";
                }
            }

            return version;
        }

        private static void GenerateCsv(IEnumerable<IntegrationInfo> integrations, AbsolutePath outputPath, string framework)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Display Name,Integration Name,NuGet Package,Assembly,Min Version,Max Version,Built-in");

            foreach (var integration in integrations)
            {
                var builtIn = integration.IsBuiltIn ? "Yes" : "No";
                sb.AppendLine($"\"{integration.DisplayName}\",\"{integration.IntegrationName}\",\"{integration.NuGetPackage}\",\"{integration.AssemblyName}\",\"{integration.MinVersion}\",\"{integration.MaxVersion}\",\"{builtIn}\"");
            }

            EnsureExistingDirectory(outputPath.Parent);
            File.WriteAllText(outputPath, sb.ToString());

            Logger.Information("Generated {Framework} supported integrations CSV: {Path}", framework, outputPath);
            Logger.Information("Total integrations: {Count}", integrations.Count());
        }
    }
}
