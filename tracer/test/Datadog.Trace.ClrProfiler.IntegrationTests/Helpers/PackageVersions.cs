// <copyright file="PackageVersions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Datadog.Trace.ClrProfiler.IntegrationTests;

/// <summary>
/// Runtime loader for the package version test manifest. Replaces the four generated
/// .g.cs files (PackageVersions.g.cs, PackageVersionsLatestMinors.g.cs, etc.) that
/// used #if preprocessor directives for compile-time TFM filtering.
///
/// This class loads package_versions.json at runtime and filters versions by the
/// current target framework. All existing test consumption patterns are preserved:
///   - [MemberData(nameof(PackageVersions.X), MemberType = typeof(PackageVersions))]
///   - from arr in PackageVersions.X
///   - [PackageVersionData(nameof(PackageVersions.X))]
/// </summary>
public static class PackageVersions
{
    // When TestAllPackageVersions is not set (local/VS runs), return string.Empty
    // so tests use the default package version from the sample csproj's <ApiVersion>.
    // The Nuke build sets this env var when running multi-version integration tests.
    private static readonly bool UseManifestVersions =
        string.Equals(Environment.GetEnvironmentVariable("TestAllPackageVersions"), "true", StringComparison.OrdinalIgnoreCase);

    private static readonly Lazy<Manifest> ManifestData = new(LoadManifest);

    private static readonly string CurrentFramework = GetCurrentFramework();

    // ──────────────────────────────────────────────────
    // One property per integration -- preserves MemberData compatibility.
    // Property names must match IntegrationDefinitions.IntegrationName exactly.
    // ──────────────────────────────────────────────────

    public static IEnumerable<object[]> Hangfire => GetVersions(nameof(Hangfire));

    public static IEnumerable<object[]> Quartz => GetVersions(nameof(Quartz));

    public static IEnumerable<object[]> AwsSdk => GetVersions(nameof(AwsSdk));

    public static IEnumerable<object[]> AwsDynamoDb => GetVersions(nameof(AwsDynamoDb));

    public static IEnumerable<object[]> AwsKinesis => GetVersions(nameof(AwsKinesis));

    public static IEnumerable<object[]> AwsLambda => GetVersions(nameof(AwsLambda));

    public static IEnumerable<object[]> AwsSqs => GetVersions(nameof(AwsSqs));

    public static IEnumerable<object[]> AwsSns => GetVersions(nameof(AwsSns));

    public static IEnumerable<object[]> AwsEventBridge => GetVersions(nameof(AwsEventBridge));

    public static IEnumerable<object[]> AwsS3 => GetVersions(nameof(AwsS3));

    public static IEnumerable<object[]> AwsStepFunctions => GetVersions(nameof(AwsStepFunctions));

    public static IEnumerable<object[]> MongoDB => GetVersions(nameof(MongoDB));

    public static IEnumerable<object[]> ElasticSearch7 => GetVersions(nameof(ElasticSearch7));

    public static IEnumerable<object[]> ElasticSearch6 => GetVersions(nameof(ElasticSearch6));

    public static IEnumerable<object[]> ElasticSearch5 => GetVersions(nameof(ElasticSearch5));

    public static IEnumerable<object[]> GraphQL => GetVersions(nameof(GraphQL));

    public static IEnumerable<object[]> GraphQL7 => GetVersions(nameof(GraphQL7));

    public static IEnumerable<object[]> HotChocolate => GetVersions(nameof(HotChocolate));

    public static IEnumerable<object[]> Npgsql => GetVersions(nameof(Npgsql));

    public static IEnumerable<object[]> Protobuf => GetVersions(nameof(Protobuf));

    public static IEnumerable<object[]> RabbitMQ => GetVersions(nameof(RabbitMQ));

    public static IEnumerable<object[]> DataStreamsRabbitMQ => GetVersions(nameof(DataStreamsRabbitMQ));

    public static IEnumerable<object[]> SystemDataSqlClient => GetVersions(nameof(SystemDataSqlClient));

    public static IEnumerable<object[]> MicrosoftDataSqlClient => GetVersions(nameof(MicrosoftDataSqlClient));

    public static IEnumerable<object[]> StackExchangeRedis => GetVersions(nameof(StackExchangeRedis));

    public static IEnumerable<object[]> ServiceStackRedis => GetVersions(nameof(ServiceStackRedis));

    public static IEnumerable<object[]> MySqlData => GetVersions(nameof(MySqlData));

    public static IEnumerable<object[]> MySqlConnector => GetVersions(nameof(MySqlConnector));

    public static IEnumerable<object[]> MicrosoftDataSqlite => GetVersions(nameof(MicrosoftDataSqlite));

    public static IEnumerable<object[]> XUnit => GetVersions(nameof(XUnit));

    public static IEnumerable<object[]> XUnitRetries => GetVersions(nameof(XUnitRetries));

    public static IEnumerable<object[]> XUnitV3 => GetVersions(nameof(XUnitV3));

    public static IEnumerable<object[]> XUnitRetriesV3 => GetVersions(nameof(XUnitRetriesV3));

    public static IEnumerable<object[]> NUnit => GetVersions(nameof(NUnit));

    public static IEnumerable<object[]> NUnitRetries => GetVersions(nameof(NUnitRetries));

    public static IEnumerable<object[]> MSTest => GetVersions(nameof(MSTest));

    public static IEnumerable<object[]> MSTest2 => GetVersions(nameof(MSTest2));

    public static IEnumerable<object[]> MSTest2Retries => GetVersions(nameof(MSTest2Retries));

    public static IEnumerable<object[]> Kafka => GetVersions(nameof(Kafka));

    public static IEnumerable<object[]> CosmosDb => GetVersions(nameof(CosmosDb));

    public static IEnumerable<object[]> CosmosDbVnext => GetVersions(nameof(CosmosDbVnext));

    public static IEnumerable<object[]> Serilog => GetVersions(nameof(Serilog));

    public static IEnumerable<object[]> NLog => GetVersions(nameof(NLog));

#pragma warning disable SA1300 // Element should begin with upper-case letter
#pragma warning disable IDE1006 // Naming Styles
    public static IEnumerable<object[]> log4Net => GetVersions(nameof(log4Net));
#pragma warning restore IDE1006 // Naming Styles
#pragma warning restore SA1300 // Element should begin with upper-case letter

    public static IEnumerable<object[]> ILogger => GetVersions(nameof(ILogger));

    public static IEnumerable<object[]> Aerospike => GetVersions(nameof(Aerospike));

    public static IEnumerable<object[]> Couchbase => GetVersions(nameof(Couchbase));

    public static IEnumerable<object[]> Couchbase3 => GetVersions(nameof(Couchbase3));

    public static IEnumerable<object[]> Grpc => GetVersions(nameof(Grpc));

    public static IEnumerable<object[]> GrpcLegacy => GetVersions(nameof(GrpcLegacy));

    public static IEnumerable<object[]> OpenTelemetry => GetVersions(nameof(OpenTelemetry));

    public static IEnumerable<object[]> Yarp => GetVersions(nameof(Yarp));

    public static IEnumerable<object[]> AzureServiceBus => GetVersions(nameof(AzureServiceBus));

    public static IEnumerable<object[]> AzureServiceBusAPM => GetVersions(nameof(AzureServiceBusAPM));

    public static IEnumerable<object[]> AzureEventHubs => GetVersions(nameof(AzureEventHubs));

    public static IEnumerable<object[]> Selenium => GetVersions(nameof(Selenium));

    public static IEnumerable<object[]> OpenFeature => GetVersions(nameof(OpenFeature));

    // ──────────────────────────────────────────────────
    // Core logic
    // ──────────────────────────────────────────────────

    public static IEnumerable<object[]> GetVersions(string integrationName)
    {
        if (!UseManifestVersions)
        {
            // Local/VS runs: use the default package version from the sample csproj.
            // The test infrastructure treats string.Empty as "use whatever is in <ApiVersion>".
            return new[] { new object[] { string.Empty } };
        }

        var manifest = ManifestData.Value;
        if (manifest?.Integrations is null ||
            !manifest.Integrations.TryGetValue(integrationName, out var entry))
        {
            // Fallback: return a single empty-string entry so xUnit doesn't error
            // on a parameterized test with no data.
            return new[] { new object[] { string.Empty } };
        }

        var matching = entry.Versions
            .Where(v => v.Frameworks.Contains(CurrentFramework))
            .Select(v => new object[] { v.Version })
            .ToList();

        return matching.Count > 0
            ? matching
            : new[] { new object[] { string.Empty } };
    }

    private static Manifest LoadManifest()
    {
        // Strategy: look for the JSON file as a content asset in the output directory.
        // The file is generated by the GeneratePackageVersions Nuke target and included
        // as <Content CopyToOutputDirectory="PreserveNewest"> in the csproj.
        var basePath = AppContext.BaseDirectory;
#if TEST_ALL_MINOR_PACKAGE_VERSIONS
        const string manifestFileName = "package_versions_all_minors.json";
#else
        const string manifestFileName = "package_versions.json";
#endif
        var path = Path.Combine(basePath, "PackageVersions", manifestFileName);

        // Fallback: walk up from the output directory to find it in the test project
        if (!File.Exists(path))
        {
            path = FindInParentDirectories(basePath, Path.Combine("PackageVersions", manifestFileName));
        }

        if (path is null || !File.Exists(path))
        {
            // No manifest found -- all properties will return the empty-string fallback.
            return null;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Manifest>(json, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        });
    }

    private static string FindInParentDirectories(string startDir, string relativePath)
    {
        var dir = startDir;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            var parent = Directory.GetParent(dir);
            if (parent is null)
            {
                break;
            }

            dir = parent.FullName;
        }

        return null;
    }

    private static string GetCurrentFramework()
    {
        // The .NET SDK automatically defines these constants based on the target framework.
        // This is the same mechanism the old .g.cs files used with #if directives.
#if NET48
        return "net48";
#elif NETCOREAPP2_1
        return "netcoreapp2.1";
#elif NETCOREAPP3_0
        return "netcoreapp3.0";
#elif NETCOREAPP3_1
        return "netcoreapp3.1";
#elif NET5_0
        return "net5.0";
#elif NET6_0
        return "net6.0";
#elif NET7_0
        return "net7.0";
#elif NET8_0
        return "net8.0";
#elif NET9_0
        return "net9.0";
#elif NET10_0
        return "net10.0";
#else
        // Unknown TFM -- return empty so all versions are filtered out (safe default)
        return string.Empty;
#endif
    }

    // ──────────────────────────────────────────────────
    // Manifest DTOs (must match the JSON schema from TestManifestGenerator)
    // ──────────────────────────────────────────────────

    private class Manifest
    {
        public DateTimeOffset GeneratedAt { get; set; }

        public Dictionary<string, IntegrationEntry> Integrations { get; set; }
    }

    private class IntegrationEntry
    {
        public List<VersionEntry> Versions { get; set; }
    }

    private class VersionEntry
    {
        public string Version { get; set; }

        public List<string> Frameworks { get; set; }

        public bool SkipAlpine { get; set; }

        public bool SkipArm64 { get; set; }
    }
}
