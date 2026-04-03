// <copyright file="IntegrationDefinitions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Collections.Generic;
using static GeneratePackageVersions.IntegrationDefinitionBuilder;
using static GeneratePackageVersions.TFM;

namespace GeneratePackageVersions;

/// <summary>
/// Single source of truth for all integration package version test definitions.
/// Replaces PackageVersionsGeneratorDefinitions.json and IntegrationGroups.cs.
/// </summary>
public static class IntegrationDefinitions
{
    public static IReadOnlyList<IntegrationDefinition> All { get; } = new[]
    {
        // ──────────────────────────────────────────────────
        // Scheduling
        // ──────────────────────────────────────────────────

        Create("Hangfire")
            .Sample("Samples.Hangfire")
            .Package("Hangfire.Core")
            .Versions("1.7.0", "2.0.0")
            .Specific("1.7.*", "1.8.*")
            .Build(),

        Create("Quartz")
            .Sample("Samples.Quartz")
            .Package("Quartz")
            .Versions("3.1.0", "4.0.0")
            .Specific("3.*.*")
            .Build(),

        // ──────────────────────────────────────────────────
        // AWS
        // ──────────────────────────────────────────────────

        Create("AwsSdk")
            .Sample("Samples.AWS.DynamoDBv2")
            .Package("AWSSDK.Core")
            .Versions("3.0.0", "5.0.0")
            .Specific("3.1.*", "3.3.*", "3.*.*", "4.*.*")
            .When(maxVersionExclusive: "3.3.0", onlyFrameworks: new[] { Net48 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("AwsDynamoDb")
            .Sample("Samples.AWS.DynamoDBv2")
            .Package("AWSSDK.DynamoDBv2")
            .Versions("3.0.0", "5.0.0")
            .Specific("3.1.*", "3.3.*", "3.*.*", "4.*.*")
            .When(maxVersionExclusive: "3.3.0", onlyFrameworks: new[] { Net48 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("AwsKinesis")
            .Sample("Samples.AWS.Kinesis")
            .Package("AWSSDK.Kinesis")
            .Versions("3.0.0", "5.0.0")
            .Specific("3.1.*", "3.3.*", "3.*.*", "4.*.*")
            .When(maxVersionExclusive: "3.3.0", onlyFrameworks: new[] { Net48 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("AwsLambda")
            .Sample("Samples.Amazon.Lambda.RuntimeSupport")
            .Package("Amazon.Lambda.RuntimeSupport")
            .Versions("1.4.0", "2.0.0")
            .Specific("1.*.*")
            .Frameworks(Net60, Net70, Net80)
            .When(minVersion: "1.4.0", onlyFrameworks: new[] { Net60, Net70, Net80 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("AwsSqs")
            .Sample("Samples.AWS.SQS")
            .Package("AWSSDK.SQS")
            .Versions("3.0.0", "5.0.0")
            .Specific("3.1.*", "3.3.*", "3.*.*", "4.*.*")
            .When(maxVersionExclusive: "3.3.0", onlyFrameworks: new[] { Net48 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("AwsSns")
            .Sample("Samples.AWS.SimpleNotificationService")
            .Package("AWSSDK.SimpleNotificationService")
            .Versions("3.0.0", "5.0.0")
            .Specific("3.1.*", "3.3.*", "3.*.*", "4.*.*")
            .When(maxVersionExclusive: "3.3.0", onlyFrameworks: new[] { Net48 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("AwsEventBridge")
            .Sample("Samples.AWS.EventBridge")
            .Package("AWSSDK.EventBridge")
            .Versions("3.3.100", "5.0.0")
            .Specific("3.3.*", "3.5.*", "3.7.*", "4.*.*")
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("AwsS3")
            .Sample("Samples.AWS.S3")
            .Package("AWSSDK.S3")
            .Versions("3.3.0", "5.0.0")
            .Specific("3.3.*", "3.5.*", "3.7.*", "4.*.*")
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("AwsStepFunctions")
            .Sample("Samples.AWS.StepFunctions")
            .Package("AWSSDK.StepFunctions")
            .Versions("3.3.0", "5.0.0")
            .Specific("3.3.*", "3.5.*", "3.7.*", "4.*.*")
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        // ──────────────────────────────────────────────────
        // Databases
        // ──────────────────────────────────────────────────

        Create("MongoDB")
            .Sample("Samples.MongoDB")
            .Package("MongoDB.Driver")
            .Versions("2.0.0", "4.0.0")
            .Specific("2.0.*", "2.*.*", "3.4.*", "3.*.*")
            .When(minVersion: "3.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31 })
            .When(maxVersionExclusive: "2.3.0", onlyFrameworks: new[] { Net48 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("Npgsql")
            .Sample("Samples.Npgsql")
            .Package("Npgsql")
            .Versions("4.0.0", "11.0.0")
            .Specific("4.*.*", "6.*.*", "9.*.*", "10.*.*")
            .When(minVersion: "10.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .When(minVersion: "9.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50 })
            .When(minVersion: "6.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("SystemDataSqlClient")
            .Sample("Samples.SqlServer")
            .Package("System.Data.SqlClient")
            .Versions("4.1.0", "5.0.0")
            .Specific("4.1.*", "4.5.*", "4.8.*", "4.*.*")
            .When(minVersion: "4.9.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50 })
            .DockerDependency(DockerDependencyType.LinuxAndMac)
            .Build(),

        Create("MicrosoftDataSqlClient")
            .Sample("Samples.Microsoft.Data.SqlClient")
            .Package("Microsoft.Data.SqlClient")
            .Versions("1.0.0", "8.0.0")
            .Specific("1.*.*", "2.*.*", "3.*.*", "4.*.*", "6.*.*", "7.*.*")
            .When(minVersion: "5.1.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30 })
            .When(minVersion: "6.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .DockerDependency(DockerDependencyType.LinuxAndMac)
            .Build(),

        Create("MySqlData")
            .Sample("Samples.MySql")
            .Package("MySql.Data")
            .Versions("6.7.9", "10.0.0")
            .Specific("6.7.*", "6.*.*", "8.*.*", "9.*.*")
            .When(minVersion: "8.0.33", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("MySqlConnector")
            .Sample("Samples.MySqlConnector")
            .Package("MySqlConnector")
            .Versions("0.61.0", "3.0.0")
            .Specific("0.61.*", "1.0.*", "1.*.*", "2.*.*")
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("MicrosoftDataSqlite")
            .Sample("Samples.Microsoft.Data.Sqlite")
            .Package("Microsoft.Data.Sqlite")
            .Versions("2.0.0", "11.0.0")
            .Specific("2.*.*", "5.*.*", "8.*.*", "9.*.*", "10.*.*")
            .When(minVersion: "3.1.32", maxVersionExclusive: "4.0.0", excludeFrameworks: new[] { NetCoreApp30 })
            .When(excludeFrameworks: new[] { NetCoreApp21 })
            .Build(),

        Create("CosmosDb")
            .Sample("Samples.CosmosDb")
            .Package("Microsoft.Azure.Cosmos")
            .Versions("3.6.0", "4.0.0")
            .Specific("3.6.*", "3.*.*")
            .When(minVersion: "3.29.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30 })
            .Build(),

        Create("CosmosDbVnext")
            .Sample("Samples.CosmosDb.Vnext")
            .Package("Microsoft.Azure.Cosmos")
            .Versions("3.12.0", "4.0.0")
            .Specific("3.12.*", "3.*.*")
            .When(minVersion: "3.29.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30 })
            .Build(),

        Create("Aerospike")
            .Sample("Samples.Aerospike")
            .Package("Aerospike.Client")
            .Versions("4.0.0", "9.0.0")
            .Specific("4.0.*", "4.*.*", "5.*.*", "7.*.*", "8.*.*")
            .When(minVersion: "8.1.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .When(minVersion: "6.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50 })
            .When(minVersion: "5.0.0", excludeFrameworks: new[] { Net48 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("Couchbase")
            .Sample("Samples.Couchbase")
            .Package("CouchbaseNetClient")
            .Versions("2.4.8", "3.0.0")
            .Specific("2.4.*", "2.*.*")
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("Couchbase3")
            .Sample("Samples.Couchbase3")
            .Package("CouchbaseNetClient")
            .Versions("3.0.0", "4.0.0")
            .Specific("3.0.*", "3.*.*")
            .When(minVersion: "3.2.6", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        // ──────────────────────────────────────────────────
        // Search
        // ──────────────────────────────────────────────────

        Create("ElasticSearch7")
            .Sample("Samples.Elasticsearch.V7")
            .Package("Elasticsearch.Net")
            .Versions("7.0.0", "8.0.0")
            .Specific("7.0.*", "7.8.*", "7.*.*")
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("ElasticSearch6")
            .Sample("Samples.Elasticsearch")
            .Package("Elasticsearch.Net")
            .Versions("6.0.0", "7.0.0")
            .Specific("6.0.*", "6.8.*", "6.*.*")
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("ElasticSearch5")
            .Sample("Samples.Elasticsearch.V5")
            .Package("Elasticsearch.Net")
            .Versions("5.3.0", "6.0.0")
            .Specific("5.3.*", "5.5.*", "5.*.*")
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        // ──────────────────────────────────────────────────
        // GraphQL
        // ──────────────────────────────────────────────────

        Create("GraphQL")
            .Sample("Samples.GraphQL4")
            .Package("GraphQL")
            .Versions("4.1.0", "6.0.0")
            .Specific("4.1.*", "4.3.*", "4.*.*", "5.*.*")
            .Frameworks(NetCoreApp31, Net50, Net60, Net70, Net80, Net90, Net100)
            .When(minVersion: "5.0.0", maxVersionExclusive: "5.1.1", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .When(maxVersionExclusive: "6.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30 })
            .Build(),

        Create("GraphQL7")
            .Sample("Samples.GraphQL7")
            .Package("GraphQL")
            .Versions("7.0.0", "9.0.0")
            .Specific("7.*.*", "8.*.*")
            .Frameworks(NetCoreApp31, Net50, Net60, Net70, Net80, Net90, Net100)
            .When(maxVersionExclusive: "8.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30 })
            .Build(),

        Create("HotChocolate")
            .Sample("Samples.HotChocolate")
            .Package("HotChocolate.AspNetCore")
            .Versions("11.0.0", "16.0.0")
            .Specific("11.*.*", "12.*.*", "13.*.*", "14.*.*", "15.*.*")
            .When(minVersion: "12.22.0", excludeFrameworks: new[] { NetCoreApp31, Net50 })
            .When(maxVersionExclusive: "15.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30 })
            .When(minVersion: "15.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .Build(),

        // ──────────────────────────────────────────────────
        // Redis
        // ──────────────────────────────────────────────────

        Create("StackExchangeRedis")
            .Sample("Samples.StackExchange.Redis")
            .Package("StackExchange.Redis")
            .Versions("1.0.187", "3.0.0")
            .Specific("1.0.*", "1.2.*", "2.*.*")
            .When(maxVersionExclusive: "1.1.700", onlyFrameworks: new[] { Net48 })
            .When(minVersion: "2.2.0", excludeFrameworks: new[] { NetCoreApp21 })
            .When(minVersion: "2.7.4", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("ServiceStackRedis")
            .Sample("Samples.ServiceStack.Redis")
            .Package("ServiceStack.Redis")
            .Versions("4.0.48", "11.0.0")
            .Specific("4.*.*", "6.*.*", "8.*.*", "10.*.*")
            .When(minVersion: "10.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50 })
            .When(maxVersionExclusive: "5.0.0", onlyFrameworks: new[] { Net48 })
            .When(minVersion: "6.2.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        // ──────────────────────────────────────────────────
        // Messaging
        // ──────────────────────────────────────────────────

        Create("RabbitMQ")
            .Sample("Samples.RabbitMQ")
            .Package("RabbitMQ.Client")
            .Versions("3.6.9", "8.0.0")
            .Specific("3.*.*", "4.*.*", "5.*.*", "6.*.*", "7.*.*")
            .When(minVersion: "7.0.0", excludeFrameworks: new[] { Net48 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("DataStreamsRabbitMQ")
            .Sample("Samples.DataStreams.RabbitMQ")
            .Package("RabbitMQ.Client")
            .Versions("3.6.9", "8.0.0")
            .Specific("3.*.*", "4.*.*", "5.*.*", "6.*.*", "7.*.*")
            .When(minVersion: "7.0.0", excludeFrameworks: new[] { Net48 })
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("Kafka")
            .Sample("Samples.Kafka")
            .Package("Confluent.Kafka")
            .Versions("1.4.0", "3.0.0")
            .Specific("1.4.*", "1.*.*", "2.*.*")
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("AzureServiceBus")
            .Sample("Samples.AzureServiceBus")
            .Package("Azure.Messaging.ServiceBus")
            .Versions("7.4.0", "7.18.0")
            .Specific("7.4.*", "7.10.*", "7.13.*", "7.*.*")
            .Build(),

        Create("AzureServiceBusAPM")
            .Sample("Samples.AzureServiceBus.APM")
            .Package("Azure.Messaging.ServiceBus")
            .Versions("7.18.0", "8.0.0")
            .Specific("7.18.*", "7.*.*")
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        Create("AzureEventHubs")
            .Sample("Samples.AzureEventHubs")
            .Package("Azure.Messaging.EventHubs")
            .Versions("5.11.0", "6.0.0")
            .Specific("5.11.*", "5.*.*")
            .DockerDependency(DockerDependencyType.All)
            .Build(),

        // ──────────────────────────────────────────────────
        // gRPC + Protobuf
        // ──────────────────────────────────────────────────

        Create("Protobuf")
            .Sample("Samples.GoogleProtobuf")
            .Package("Google.Protobuf")
            .Versions("3.0.0", "4.0.0")
            .Specific("3.12.*", "3.*.*")
            .Build(),

        Create("Grpc")
            .Sample("Samples.GrpcDotNet")
            .Package("Grpc.AspNetCore")
            .Versions("2.0.0", "3.0.0")
            .Specific("2.29.*", "2.30.0", "2.*.*")
            .When(minVersion: "2.0.0", skipAlpine: true)
            .When(maxVersionExclusive: "2.38.1", skipArm64: true)
            .When(minVersion: "2.57.0", excludeFrameworks: new[] { NetCoreApp30, NetCoreApp31, Net50 })
            .When(minVersion: "2.76.0", excludeFrameworks: new[] { NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .Build(),

        Create("GrpcLegacy")
            .Sample("Samples.GrpcLegacy")
            .Package("Grpc")
            .Versions("2.0.0", "3.0.0")
            .Specific("2.29.*", "2.30.0", "2.*.*")
            .When(minVersion: "2.0.0", skipAlpine: true)
            .When(maxVersionExclusive: "2.38.1", skipArm64: true)
            .Build(),

        // ──────────────────────────────────────────────────
        // Testing Frameworks (CI Visibility)
        // ──────────────────────────────────────────────────

        Create("XUnit")
            .Sample("Samples.XUnitTests")
            .Package("xunit")
            .Versions("2.2.0", "3.0.0")
            .Specific("2.2.*", "2.4.3", "2.*.*")
            .When(minVersion: "2.4.5", maxVersionExclusive: "3.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50 })
            .Build(),

        Create("XUnitRetries")
            .Sample("Samples.XUnitTestsRetries")
            .Package("xunit")
            .Versions("2.2.0", "3.0.0")
            .Specific("2.2.*", "2.4.3", "2.*.*")
            .When(minVersion: "2.4.5", maxVersionExclusive: "3.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50 })
            .Build(),

        Create("XUnitV3")
            .Sample("Samples.XUnitTestsV3")
            .Package("xunit.v3")
            .Versions("1.0.0", "4.0.0")
            .Specific("2.*.*", "3.*.*")
            .When(minVersion: "1.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50 })
            .When(minVersion: "2.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .Build(),

        Create("XUnitRetriesV3")
            .Sample("Samples.XUnitTestsRetriesV3")
            .Package("xunit.v3")
            .Versions("1.0.0", "4.0.0")
            .Specific("2.*.*", "3.*.*")
            .When(minVersion: "1.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50 })
            .When(minVersion: "2.0.0", excludeFrameworks: new[] { Net48, NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .Build(),

        Create("NUnit")
            .Sample("Samples.NUnitTests")
            .Package("NUnit")
            .Versions("3.6.0", "5.0.0")
            .Specific("3.6.*", "3.10.*", "3.*.*", "4.*.*")
            .When(minVersion: "4.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50 })
            .Build(),

        Create("NUnitRetries")
            .Sample("Samples.NUnitTestsRetries")
            .Package("NUnit")
            .Versions("3.6.0", "5.0.0")
            .Specific("3.6.*", "3.10.*", "3.*.*", "4.*.*")
            .When(minVersion: "4.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50 })
            .Build(),

        Create("MSTest")
            .Sample("Samples.MSTestTests")
            .Package("MSTest.TestFramework")
            .Versions("2.0.0", "5.0.0")
            .Specific("2.0.*", "2.*.*", "3.*.*", "4.*.*")
            .When(minVersion: "4.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .Build(),

        Create("MSTest2")
            .Sample("Samples.MSTestTests2")
            .Package("MSTest.TestFramework")
            .Versions("2.0.0", "5.0.0")
            .Specific("2.0.*", "2.*.*", "3.*.*", "4.*.*")
            .When(minVersion: "4.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .Build(),

        Create("MSTest2Retries")
            .Sample("Samples.MSTestTestsRetries")
            .Package("MSTest.TestFramework")
            .Versions("2.0.0", "5.0.0")
            .Specific("2.0.*", "2.*.*", "3.*.*", "4.*.*")
            .When(minVersion: "4.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .Build(),

        // ──────────────────────────────────────────────────
        // Logging
        // ──────────────────────────────────────────────────

        Create("Serilog")
            .Sample("LogsInjection.Serilog")
            .Package("Serilog")
            .Versions("1.0.0", "5.0.0")
            .Specific("1.*.*", "1.4.*", "2.*.*", "3.*.*", "4.*.*")
            .When(maxVersionExclusive: "2.0.0", onlyFrameworks: new[] { Net48 })
            .When(minVersion: "3.1.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30 })
            .Build(),

        Create("NLog")
            .Sample("LogsInjection.NLog")
            .Package("NLog")
            .Versions("1.0.0", "7.0.0")
            .Specific("1.0.*", "2.1.*", "4.*.*", "5.*.*", "6.*.*")
            .When(maxVersionExclusive: "4.5.0", onlyFrameworks: new[] { Net48 })
            .Build(),

        Create("log4net")
            .Sample("LogsInjection.Log4Net")
            .Package("log4net")
            .Versions("1.0.0", "4.0.0")
            .Specific("1.*.*", "2.*.*", "3.*.*")
            .When(maxVersionExclusive: "2.0.6", onlyFrameworks: new[] { Net48 })
            .Build(),

        Create("ILogger")
            .Sample("LogsInjection.ILogger.ExtendedLogger")
            .Package("Microsoft.Extensions.Telemetry")
            .Versions("8.0.0", "11.0.0")
            .Specific("8.*.*", "9.*.*", "10.*.*")
            .When(minVersion: "9.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50, Net60, Net70 })
            .When(minVersion: "8.0.0", excludeFrameworks: new[] { NetCoreApp21, NetCoreApp30, NetCoreApp31, Net50 })
            .Build(),

        // ──────────────────────────────────────────────────
        // OpenTelemetry
        // ──────────────────────────────────────────────────

        Create("OpenTelemetry")
            .Sample("Samples.OpenTelemetrySdk")
            .Package("OpenTelemetry.Api")
            .Versions("1.0.0", "2.0.0")
            .Specific("1.0.1", "1.3.2", "1.5.1", "1.*.*")
            .Build(),

        // ──────────────────────────────────────────────────
        // HTTP / Reverse Proxy
        // ──────────────────────────────────────────────────

        Create("Yarp")
            .Sample("Samples.Yarp.DistributedTracing")
            .Package("Yarp.ReverseProxy")
            .Versions("1.0.0", "3.0.0")
            .Specific("1.0.*", "1.1.0", "2.*.*")
            .When(minVersion: "2.0.0", excludeFrameworks: new[] { NetCoreApp31, Net50 })
            .Build(),

        // ──────────────────────────────────────────────────
        // Browser / Selenium
        // ──────────────────────────────────────────────────

        Create("Selenium")
            .Sample("Samples.Selenium")
            .Package("Selenium.WebDriver")
            .Versions("4.0.0", "5.0.0")
            .Specific("4.0.*", "4.10.*", "4.15.*", "4.20.*", "4.*.*")
            .Build(),

        // ──────────────────────────────────────────────────
        // Feature Flags
        // ──────────────────────────────────────────────────

        Create("OpenFeature")
            .Sample("Samples.OpenFeature")
            .Package("OpenFeature")
            .Versions("2.0.0", "3.0.0")
            .Specific("2.0.0", "2.10.0")
            .Build(),
    };
}
