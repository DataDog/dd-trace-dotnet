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

            NugetPackages.Add("Oracle.DataAccess", new string[] { });

            NugetPackages.Add("System.Data", new [] { "System.Data.SqlClient" });
            NugetPackages.Add("System.Data.Common", new [] { "System.Data.Common" });
            NugetPackages.Add("AerospikeClient", new [] { "Aerospike.Client" });
            NugetPackages.Add("Microsoft.AspNetCore.Http", new string[] { });
            NugetPackages.Add("System.Web.Mvc", new [] { "Microsoft.AspNet.Mvc" });
            NugetPackages.Add("AWSSDK.Core", new [] { "AWSSDK.Core" });
            NugetPackages.Add("AWSSDK.SQS", new [] { "AWSSDK.SQS" });
            NugetPackages.Add("Microsoft.Azure.Cosmos.Client", new [] { "Microsoft.Azure.Cosmos" });
            NugetPackages.Add("Elasticsearch.Net", new [] { "Elasticsearch.Net" });
            NugetPackages.Add("GraphQL", new [] { "GraphQL" });
            NugetPackages.Add("GraphQL.SystemReactive", new [] { "GraphQL.SystemReactive" });
            NugetPackages.Add("HotChocolate.Execution", new[] { "HotChocolate.AspNetCore" });
            NugetPackages.Add("System.Net.Http", new [] { "System.Net.Http" });
            NugetPackages.Add("System.Net.Http.WinHttpHandler", new [] { "System.Net.Http.WinHttpHandler" });
            NugetPackages.Add("Microsoft.Extensions.Logging.Abstractions", new [] { "Microsoft.Extensions.Logging.Abstractions" });
            NugetPackages.Add("Microsoft.Extensions.Logging", new [] { "Microsoft.Extensions.Logging" });
            NugetPackages.Add("Confluent.Kafka", new [] { "Confluent.Kafka" });
            NugetPackages.Add("MongoDB.Driver.Core", new [] { "MongoDB.Driver.Core", "MongoDB.Driver" });
            NugetPackages.Add("Microsoft.VisualStudio.TestPlatform.TestFramework", new [] { "Microsoft.VisualStudio.TestPlatform" });
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
            NugetPackages.Add("Microsoft.Azure.WebJobs.Host", new [] { "Microsoft.Azure.WebJobs" });
            NugetPackages.Add("Microsoft.Azure.WebJobs.Script.WebHost", new string[] { });
            NugetPackages.Add("Couchbase.NetClient", new string[] { "CouchbaseNetClient" });
            NugetPackages.Add("Grpc.AspNetCore.Server", new string[] { "Grpc.AspNetCore" });
            NugetPackages.Add("Grpc.Net.Client", new string[] { "Grpc.AspNetCore" });
            NugetPackages.Add("Grpc.Core", new string[] { "Grpc" });
            NugetPackages.Add("Microsoft.AspNetCore.Mvc.Core", new [] { "Microsoft.AspNetCore.Mvc.Core" });
        }

        private IntegrationMap()
        { 
        }

        public static async Task<IntegrationMap> Create(string name, string assemblyName, Version maximumVersion)
        {
            var instance = new IntegrationMap();

            if (!NugetPackages.ContainsKey(name))
            {
                throw new Exception($"Missing key: {name} - Every integration must be represented in the packages map.");
            }

            instance.Name = name;
            instance.AssemblyName = assemblyName;
            instance.MaximumAssemblyVersion = maximumVersion;

            await instance.PopulatePackages();

            return instance;
        }

        public string Name { get; set; }

        public string AssemblyName { get; set; }

        public Version MaximumAssemblyVersion { get; set; }

        public List<IntegrationPackage> Packages { get; set; } = new List<IntegrationPackage>();

        private async Task PopulatePackages()
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
                    .FirstOrDefault(x => x.Identity.Version.Version <= MaximumAssemblyVersion);

                if (latestSupportedPackage is null)
                {
                    Logger.Warn($"No version of {packageName} below maximum package version {MaximumAssemblyVersion}." +
                                $"Using latest instead");
                }

                var latestSupportedVersion = latestSupportedPackage is null
                                                 ? latestVersion
                                                 : new Version(latestSupportedPackage.Identity.Version.ToNormalizedString());

                Packages.Add(new IntegrationPackage
                {
                    NugetName = latestPackage.Identity.Id,
                    LatestNuget = latestVersion,
                    LatestSupportedNuget = latestSupportedVersion,
                });
            }
        }
    }

    public class IntegrationPackage
    {
        public string NugetName { get; set; }
        public Version LatestSupportedNuget { get; set; }
        public Version LatestNuget { get; set; }
    }

    public class PackageSearchCriteria : IPackageVersionEntry
    {
        public string IntegrationName { get; set; }

        public string NugetPackageSearchName { get; set; }

        public string MinVersion { get; set; }

        public string MaxVersionExclusive { get; set; }
    }
}
