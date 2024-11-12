// <copyright file="IntegrationIdExtensions.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Configuration;

namespace Datadog.Trace.Telemetry.Metrics;

internal static class IntegrationIdExtensions
{
    public static MetricTags.IntegrationName GetMetricTag(this IntegrationId integrationId)
        => integrationId switch
        {
            IntegrationId.HttpMessageHandler => MetricTags.IntegrationName.HttpMessageHandler,
            IntegrationId.HttpSocketsHandler => MetricTags.IntegrationName.HttpSocketsHandler,
            IntegrationId.WinHttpHandler => MetricTags.IntegrationName.WinHttpHandler,
            IntegrationId.CurlHandler => MetricTags.IntegrationName.CurlHandler,
            IntegrationId.AspNetCore => MetricTags.IntegrationName.AspNetCore,
            IntegrationId.AdoNet => MetricTags.IntegrationName.AdoNet,
            IntegrationId.AspNet => MetricTags.IntegrationName.AspNet,
            IntegrationId.AspNetMvc => MetricTags.IntegrationName.AspNetMvc,
            IntegrationId.AspNetWebApi2 => MetricTags.IntegrationName.AspNetWebApi2,
            IntegrationId.GraphQL => MetricTags.IntegrationName.GraphQL,
            IntegrationId.HotChocolate => MetricTags.IntegrationName.HotChocolate,
            IntegrationId.MongoDb => MetricTags.IntegrationName.MongoDb,
            IntegrationId.XUnit => MetricTags.IntegrationName.XUnit,
            IntegrationId.NUnit => MetricTags.IntegrationName.NUnit,
            IntegrationId.MsTestV2 => MetricTags.IntegrationName.MsTestV2,
            IntegrationId.Wcf => MetricTags.IntegrationName.Wcf,
            IntegrationId.WebRequest => MetricTags.IntegrationName.WebRequest,
            IntegrationId.ElasticsearchNet => MetricTags.IntegrationName.ElasticsearchNet,
            IntegrationId.ServiceStackRedis => MetricTags.IntegrationName.ServiceStackRedis,
            IntegrationId.StackExchangeRedis => MetricTags.IntegrationName.StackExchangeRedis,
            IntegrationId.ServiceRemoting => MetricTags.IntegrationName.ServiceRemoting,
            IntegrationId.RabbitMQ => MetricTags.IntegrationName.RabbitMQ,
            IntegrationId.Msmq => MetricTags.IntegrationName.Msmq,
            IntegrationId.Kafka => MetricTags.IntegrationName.Kafka,
            IntegrationId.CosmosDb => MetricTags.IntegrationName.CosmosDb,
            IntegrationId.AwsSdk => MetricTags.IntegrationName.AwsSdk,
            IntegrationId.AwsSns => MetricTags.IntegrationName.AwsSns,
            IntegrationId.AwsSqs => MetricTags.IntegrationName.AwsSqs,
            IntegrationId.AwsEventBridge => MetricTags.IntegrationName.AwsEventBridge,
            IntegrationId.AwsLambda => MetricTags.IntegrationName.AwsLambda,
            IntegrationId.ILogger => MetricTags.IntegrationName.ILogger,
            IntegrationId.Aerospike => MetricTags.IntegrationName.Aerospike,
            IntegrationId.AzureFunctions => MetricTags.IntegrationName.AzureFunctions,
            IntegrationId.Couchbase => MetricTags.IntegrationName.Couchbase,
            IntegrationId.MySql => MetricTags.IntegrationName.MySql,
            IntegrationId.Npgsql => MetricTags.IntegrationName.Npgsql,
            IntegrationId.Oracle => MetricTags.IntegrationName.Oracle,
            IntegrationId.SqlClient => MetricTags.IntegrationName.SqlClient,
            IntegrationId.Sqlite => MetricTags.IntegrationName.Sqlite,
            IntegrationId.Serilog => MetricTags.IntegrationName.Serilog,
            IntegrationId.Log4Net => MetricTags.IntegrationName.Log4Net,
            IntegrationId.NLog => MetricTags.IntegrationName.NLog,
            IntegrationId.TraceAnnotations => MetricTags.IntegrationName.TraceAnnotations,
            IntegrationId.Grpc => MetricTags.IntegrationName.Grpc,
            IntegrationId.Process => MetricTags.IntegrationName.Process,
            IntegrationId.HashAlgorithm => MetricTags.IntegrationName.HashAlgorithm,
            IntegrationId.SymmetricAlgorithm => MetricTags.IntegrationName.SymmetricAlgorithm,
            IntegrationId.OpenTelemetry => MetricTags.IntegrationName.OpenTelemetry,
            IntegrationId.PathTraversal => MetricTags.IntegrationName.PathTraversal,
            IntegrationId.Ssrf => MetricTags.IntegrationName.Ssrf,
            IntegrationId.Ldap => MetricTags.IntegrationName.Ldap,
            IntegrationId.HardcodedSecret => MetricTags.IntegrationName.HardcodedSecret,
            IntegrationId.AwsKinesis => MetricTags.IntegrationName.AwsKinesis,
            IntegrationId.AzureServiceBus => MetricTags.IntegrationName.AzureServiceBus,
            IntegrationId.SystemRandom => MetricTags.IntegrationName.SystemRandom,
            IntegrationId.AwsDynamoDb => MetricTags.IntegrationName.AwsDynamoDb,
            IntegrationId.IbmMq => MetricTags.IntegrationName.IbmMq,
            IntegrationId.Remoting => MetricTags.IntegrationName.Remoting,
            IntegrationId.TrustBoundaryViolation => MetricTags.IntegrationName.TrustBoundaryViolation,
            IntegrationId.UnvalidatedRedirect => MetricTags.IntegrationName.UnvalidatedRedirect,
            IntegrationId.TestPlatformAssemblyResolver => MetricTags.IntegrationName.TestPlatformAssemblyResolver,
            IntegrationId.StackTraceLeak => MetricTags.IntegrationName.StackTraceLeak,
            IntegrationId.XpathInjection => MetricTags.IntegrationName.XpathInjection,
            IntegrationId.ReflectionInjection => MetricTags.IntegrationName.ReflectionInjection,
            IntegrationId.Xss => MetricTags.IntegrationName.Xss,
            IntegrationId.NHibernate => MetricTags.IntegrationName.NHibernate,
            IntegrationId.DotnetTest => MetricTags.IntegrationName.DotnetTest,
            IntegrationId.Selenium => MetricTags.IntegrationName.Selenium,
            IntegrationId.DatadogTraceManual => MetricTags.IntegrationName.DatadogTraceManual,
            IntegrationId.DirectoryListingLeak => MetricTags.IntegrationName.DirectoryListingLeak,
            IntegrationId.SessionTimeout => MetricTags.IntegrationName.SessionTimeout,
            IntegrationId.EmailHtmlInjection => MetricTags.IntegrationName.EmailHtmlInjection,
            _ => throw new InvalidOperationException($"Unknown IntegrationID {integrationId}"), // dangerous, but we test it will never be called
        };
}
