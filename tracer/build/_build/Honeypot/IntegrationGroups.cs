// <copyright file="IntegrationGroups.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GeneratePackageVersions;
using Nuke.Common;
using Logger = Serilog.Log;

namespace Honeypot
{
    public class IntegrationMap
    {
        public static Dictionary<string, string[]> NugetPackages = new Dictionary<string, string[]>();

        static IntegrationMap()
        {
            NugetPackages.Add("System.Web.Http", new string[] { });
            NugetPackages.Add("System.Web", new string[] { });
            NugetPackages.Add("netstandard", new string[] { });
            NugetPackages.Add("System.Messaging", new string[] { });
            NugetPackages.Add("System", new string[] { });
            NugetPackages.Add("System.Diagnostics.Process", new string[] { });
            NugetPackages.Add("System.Runtime.Remoting", new string[] {  });
            NugetPackages.Add("System.Security.Cryptography", new string[] { });
            NugetPackages.Add("System.Security.Cryptography.Primitives", new string[] { });

            NugetPackages.Add("Oracle.DataAccess", new string[] { });

            NugetPackages.Add("System.Data", new [] { "System.Data.SqlClient" });
            NugetPackages.Add("System.Data.Common", new [] { "System.Data.Common" });
            NugetPackages.Add("AerospikeClient", new [] { "Aerospike.Client" });
            NugetPackages.Add("Microsoft.AspNetCore.Http", new string[] { });
            NugetPackages.Add("System.Web.Mvc", new [] { "Microsoft.AspNet.Mvc" });
            NugetPackages.Add("Amazon.Lambda.RuntimeSupport", new [] { "Amazon.Lambda.RuntimeSupport" });
            NugetPackages.Add("AWSSDK.DynamoDBv2", new [] { "AWSSDK.DynamoDBv2" });
            NugetPackages.Add("AWSSDK.Core", new [] { "AWSSDK.Core" });
            NugetPackages.Add("AWSSDK.Kinesis", new [] { "AWSSDK.Kinesis" });
            NugetPackages.Add("AWSSDK.SQS", new [] { "AWSSDK.SQS" });
            NugetPackages.Add("AWSSDK.SimpleNotificationService", new [] { "AWSSDK.SimpleNotificationService" });
            NugetPackages.Add("Microsoft.Azure.Cosmos.Client", new [] { "Microsoft.Azure.Cosmos" });
            NugetPackages.Add("Elasticsearch.Net", new [] { "Elasticsearch.Net" });
            NugetPackages.Add("GraphQL", new [] { "GraphQL" });
            NugetPackages.Add("GraphQL.SystemReactive", new [] { "GraphQL.SystemReactive" });
            NugetPackages.Add("HotChocolate.Execution", new[] { "HotChocolate.AspNetCore" });
            NugetPackages.Add("System.Net.Http", new [] { "System.Net.Http" });
            NugetPackages.Add("System.Net.Http.WinHttpHandler", new [] { "System.Net.Http.WinHttpHandler" });
            NugetPackages.Add("Microsoft.Extensions.Logging.Abstractions", new [] { "Microsoft.Extensions.Logging.Abstractions" });
            NugetPackages.Add("Microsoft.Extensions.Logging", new [] { "Microsoft.Extensions.Logging" });
            NugetPackages.Add("Microsoft.Extensions.Telemetry", new [] { "Microsoft.Extensions.Telemetry" });
            NugetPackages.Add("Confluent.Kafka", new [] { "Confluent.Kafka" });
            NugetPackages.Add("MongoDB.Driver.Core", new [] { "MongoDB.Driver.Core", "MongoDB.Driver" });
            NugetPackages.Add("Microsoft.VisualStudio.TestPlatform.TestFramework", new [] { "Microsoft.VisualStudio.TestPlatform" });
            NugetPackages.Add("Microsoft.VisualStudio.TestPlatform.Common", new [] { "Microsoft.VisualStudio.TestPlatform" });
            NugetPackages.Add("Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter", new [] { "MSTest.TestAdapter" });
            NugetPackages.Add("MySqlConnector", new [] { "MySqlConnector" });
            NugetPackages.Add("MySql.Data", new [] { "MySql.Data" });
            NugetPackages.Add("Npgsql", new [] { "Npgsql" });
            NugetPackages.Add("nunit.framework", new [] { "NUnit" });
            NugetPackages.Add("NUnit3.TestAdapter", new [] { "NUnit3TestAdapter" });
            NugetPackages.Add("Oracle.ManagedDataAccess", new [] { "Oracle.ManagedDataAccess" });
            NugetPackages.Add("RabbitMQ.Client", new [] { "RabbitMQ.Client" });
            NugetPackages.Add("ServiceStack.Redis", new [] { "ServiceStack.Redis" });
            NugetPackages.Add("System.Data.SqlClient", new [] { "System.Data.SqlClient" });
            NugetPackages.Add("Microsoft.Data.SqlClient", new [] { "Microsoft.Data.SqlClient" });
            NugetPackages.Add("Microsoft.Data.Sqlite", new [] { "Microsoft.Data.Sqlite" });
            NugetPackages.Add("System.Data.SQLite", new [] { "System.Data.SQLite" });
            NugetPackages.Add("StackExchange.Redis", new [] { "StackExchange.Redis" });
            NugetPackages.Add("StackExchange.Redis.StrongName", new [] { "StackExchange.Redis.StrongName" });
            NugetPackages.Add("System.ServiceModel", new [] { "System.ServiceModel.Http" });
            NugetPackages.Add("System.Net.Requests", new [] { "System.Net.Requests" });
            NugetPackages.Add("xunit.execution.dotnet", new [] { "xunit.extensibility.execution" });
            NugetPackages.Add("xunit.execution.desktop", new [] { "xunit" });
            NugetPackages.Add("MSTest", new [] { "MSTest.TestFramework" });
            NugetPackages.Add("Serilog", new [] { "Serilog" });
            NugetPackages.Add("NLog", new [] { "NLog" });
            NugetPackages.Add("log4net", new [] { "log4net" });
            NugetPackages.Add("Microsoft.Azure.Functions.Worker.Core", new string[] { });
            NugetPackages.Add("Microsoft.Azure.WebJobs.Host", new [] { "Microsoft.Azure.WebJobs" });
            NugetPackages.Add("Microsoft.Azure.WebJobs.Script.Grpc", new string[] { });
            NugetPackages.Add("Microsoft.Azure.WebJobs.Script.WebHost", new string[] { });
            NugetPackages.Add("Couchbase.NetClient", new string[] { "CouchbaseNetClient" });
            NugetPackages.Add("Grpc.AspNetCore.Server", new string[] { "Grpc.AspNetCore" });
            NugetPackages.Add("Grpc.Net.Client", new string[] { "Grpc.AspNetCore" });
            NugetPackages.Add("Grpc.Core", new string[] { "Grpc" });
            NugetPackages.Add("Google.Protobuf", new string[] { "Google.Protobuf" });
            NugetPackages.Add("Microsoft.AspNetCore.Mvc.Core", new [] { "Microsoft.AspNetCore.Mvc.Core" });
            NugetPackages.Add("Microsoft.AspNetCore.Identity", new [] { "Microsoft.AspNetCore.Identity" });
            NugetPackages.Add("Microsoft.Extensions.Identity.Core", new [] { "Microsoft.Extensions.Identity.Core" });
            NugetPackages.Add("Microsoft.AspNetCore.Authentication.Abstractions", new [] { "Microsoft.AspNetCore.Authentication.Abstractions" });
            NugetPackages.Add("OpenTelemetry.Api", new [] { "OpenTelemetry.Api" });
            NugetPackages.Add("OpenTelemetry", new [] { "OpenTelemetry" });
            NugetPackages.Add("Microsoft.AspNetCore.Server.IIS", new[] { "Microsoft.AspNetCore.Server.IIS" });
            NugetPackages.Add("Microsoft.AspNetCore.Server.Kestrel.Core", new string[] { "Microsoft.AspNetCore.Server.Kestrel.Core" });
            NugetPackages.Add("Microsoft.AspNetCore.Diagnostics", new[] { "Microsoft.AspNetCore.Diagnostics" });
            NugetPackages.Add("Azure.Messaging.ServiceBus", new string[] { "Azure.Messaging.ServiceBus" });
            NugetPackages.Add("amqmdnetstd", new [] { "IBMMQDotnetClient" });
            NugetPackages.Add("Yarp.ReverseProxy", new [] { "Yarp.ReverseProxy" });
            NugetPackages.Add("Microsoft.AspNetCore.Html.Abstractions", new [] { "Microsoft.AspNetCore.Html.Abstractions" });
            NugetPackages.Add("dotnet", Array.Empty<string>());
            NugetPackages.Add("vstest.console", Array.Empty<string>());
            NugetPackages.Add("vstest.console.arm64", Array.Empty<string>());
            NugetPackages.Add("WebDriver", new[] { "Selenium.WebDriver" });
            NugetPackages.Add("Microsoft.AspNetCore.StaticFiles", new [] { "Microsoft.AspNetCore.StaticFiles" });
            NugetPackages.Add("coverlet.core", Array.Empty<string>());
            NugetPackages.Add("Microsoft.AspNetCore.Session", new [] { "Microsoft.AspNetCore.Session" });
            NugetPackages.Add("Microsoft.TestPlatform.PlatformAbstractions", Array.Empty<string>());
            NugetPackages.Add("Microsoft.VisualStudio.TraceDataCollector", Array.Empty<string>());

            // Manual instrumentation
            NugetPackages.Add("Datadog.Trace.Manual", new string[] { });
            NugetPackages.Add("Datadog.Trace.OpenTracing", new string[] { });
        }

        private IntegrationMap()
        { 
        }

        public static async Task<IntegrationMap> Create(string name, string integrationId, string assemblyName, Version minimumVersion, Version maximumVersion, List<PackageVersionGenerator.TestedPackage> testedVersions)
        {
            if (!NugetPackages.ContainsKey(name))
            {
                throw new Exception($"Missing key: {name} - Every integration must be represented in the packages map.");
            }
            
            var instance = new IntegrationMap
            {
                Name = name,
                IntegrationId = integrationId,
                AssemblyName = assemblyName,
                MinimumSupportedAssemblyVersion = minimumVersion,
                MaximumSupportedAssemblyVersion = maximumVersion
            };

            await instance.PopulatePackages(testedVersions);

            return instance;
        }

        public string Name { get; init; }

        public string IntegrationId { get; init; }

        public string AssemblyName { get; init; }

        public Version MaximumSupportedAssemblyVersion { get; init; }

        public Version MinimumSupportedAssemblyVersion { get; init; }

        public List<IntegrationPackage> Packages { get; } = new();

        private async Task PopulatePackages(List<PackageVersionGenerator.TestedPackage> testedVersions)
        {
            var packageNames = NugetPackages[Name];
            foreach (var packageName in packageNames)
            {
                var searchCriteria = new PackageSearchCriteria
                {
                    IntegrationName = Name,
                    NugetPackageSearchName = packageName,
                    MinVersion = "0.0.1",
                    MaxVersionExclusive = "255.255.255"
                };

                var packages = await NuGetPackageHelper.GetPackageMetadatas(searchCriteria);

                var potentiallySupportedPackages = packages
                                                  .Where(p => p.Identity.HasVersion)
                                                  .OrderByDescending(p => p.Identity.Version)
                                                  .ToList();

                // TODO: Download and check referenced assemblies for assembly versions
                //foreach (var potentiallySupportedPackage in potentiallySupportedPackages)
                //{
                //    potentiallySupportedPackage.
                //}

                var latestPackage = potentiallySupportedPackages.First();
                var latestVersion = new Version(latestPackage.Identity.Version.ToNormalizedString());

                var latestSupportedPackage = potentiallySupportedPackages
                    .FirstOrDefault(x => x.Identity.Version.Version <= MaximumSupportedAssemblyVersion);

                if (latestSupportedPackage is null)
                {
                    Logger.Warning($"No version of {packageName} below maximum package version {MaximumSupportedAssemblyVersion}." +
                                $"Using latest instead");
                }

                var latestSupportedVersion = latestSupportedPackage is null
                                                 ? latestVersion
                                                 : new Version(latestSupportedPackage.Identity.Version.ToNormalizedString());

                var firstPackage = potentiallySupportedPackages.Last();
                var firstVersion = new Version(firstPackage.Identity.Version.ToNormalizedString());
                var firstSupportedPackage = potentiallySupportedPackages
                    .LastOrDefault(x => x.Identity.Version.Version >= MinimumSupportedAssemblyVersion);
                if (firstSupportedPackage is null)
                {
                    Logger.Warning($"No version of {packageName} above minimum package version {MinimumSupportedAssemblyVersion}." +
                                   $"Using first instead");
                }

                var firstSupportedVersion = firstSupportedPackage is null
                    ? firstVersion
                    : new Version(firstSupportedPackage.Identity.Version.ToNormalizedString());

                var allTestedVersions = testedVersions
                                       .Where(x => x.NugetPackageSearchName.Equals(packageName))
                                       .ToList();

                var firstTestedVersion = allTestedVersions.MinBy(x => x.MinVersion)?.MinVersion;
                var latestTestedVersion = allTestedVersions.MaxBy(x => x.MaxVersion)?.MaxVersion;

                Packages.Add(new IntegrationPackage(
                                 NugetName: latestPackage.Identity.Id,
                                 LatestVersion: latestVersion,
                                 LatestSupportedVersion: latestSupportedVersion,
                                 LatestTestedVersion: latestTestedVersion,
                                 FirstVersion: firstVersion,
                                 FirstSupportedVersion: firstSupportedVersion,
                                 FirstTestedVersion: firstTestedVersion));
            }
        }
    }

    public record IntegrationPackage(
        string NugetName,
        Version FirstSupportedVersion,
        Version FirstTestedVersion,
        Version FirstVersion,
        Version LatestSupportedVersion,
        Version LatestTestedVersion,
        Version LatestVersion
    );

    public class PackageSearchCriteria : IPackageVersionEntry
    {
        public string IntegrationName { get; set; }

        public string NugetPackageSearchName { get; set; }

        public string MinVersion { get; set; }

        public string MaxVersionExclusive { get; set; }
    }
}
