// <copyright file="ManualInstrumentationLegacyConfigurationSource.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.ManualInstrumentation;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.SourceGenerators;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

namespace Datadog.Trace.Configuration.ConfigurationSources;

/// <summary>
/// Wraps the settings passed in from the manual instrumentation API in a configuration source, to make it easier to integrate.
/// Only used for legacy manual instrumentation (where the integration settings are serialized as an array)
/// </summary>
internal class ManualInstrumentationLegacyConfigurationSource : ManualInstrumentationConfigurationSourceBase
{
    public ManualInstrumentationLegacyConfigurationSource(IReadOnlyDictionary<string, object?> dictionary, bool useDefaultSources)
        : base(dictionary, useDefaultSources)
    {
    }

    protected override bool TryGetValue(string key, out object? value)
    {
        // Get the value for the given key, but also record telemetry
        // This is also where any "remapping" should be done, in cases
        // where either the manual-instrumentation key differs or the
        // type stored in the dictionary differs

        var result = TryGetSpecialCase(Dictionary, key, out value) || base.TryGetValue(key, out value);
        if (result)
        {
            if (GetTelemetryKey(key) is { } telemetryKey)
            {
                TelemetryFactory.Metrics.Record(telemetryKey);
            }

            if (value is not null)
            {
                value = RemapResult(key, value);
            }
        }

        return result;
    }

    private static bool TryGetSpecialCase(IReadOnlyDictionary<string, object?> dictionary, string key, out object? value)
    {
        // we currently only special-case integration related settings, which are stored in a nested dictionary

        // We're looking for:
        //   DD_TRACE_<Integration>_ENABLED
        //   DD_TRACE_<Integration>_ANALYTICS_ENABLED
        //   DD_TRACE_<Integration>_ANALYTICS_SAMPLE_RATE

        // yes, this really sucks right now
        var enabledIntegration = GetIntegrationEnabled(key);
        var analyticsIntegration = GetIntegrationAnalyticsEnabled(key);
        var sampleRateIntegration = GetIntegrationAnalyticsSampleRate(key);
        if ((enabledIntegration.HasValue || analyticsIntegration.HasValue || sampleRateIntegration.HasValue)
         && dictionary.TryGetValue(TracerSettingKeyConstants.IntegrationSettingsKey, out var raw)
         && raw is Dictionary<string, object?[]> integrations)
        {
            // ok, we have some integrations, see if we have the _right_ one
            var integrationId = enabledIntegration ?? analyticsIntegration ?? sampleRateIntegration!.Value;
            var integrationName = IntegrationRegistry.GetName(integrationId);
            if (integrations.TryGetValue(integrationName, out var values)
             && values is not null
             && IntegrationSettingsSerializationHelper.TryDeserializeFromManual(
                    values,
                    out var enabledChanged,
                    out var enabled,
                    out var analyticsEnabledChanged,
                    out var analyticsEnabled,
                    out var analyticsSampleRateChanged,
                    out var analyticsSampleRate))
            {
                if (enabledIntegration.HasValue && enabledChanged)
                {
                    TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_Enabled_Set);
                    value = enabled;
                    return true;
                }

                if (analyticsIntegration.HasValue && analyticsEnabledChanged)
                {
                    TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_AnalyticsEnabled_Set);
                    value = analyticsEnabled;
                    return true;
                }

                if (sampleRateIntegration.HasValue && analyticsSampleRateChanged)
                {
                    TelemetryFactory.Metrics.Record(PublicApiUsage.IntegrationSettings_AnalyticsSampleRate_Set);
                    value = analyticsSampleRate;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    // This list is fixed in time and doesn't need to be updated
    [TestingAndPrivateOnly]
    internal static IntegrationId? GetIntegrationEnabled(string key) => key switch
    {
        "DD_TRACE_HTTPMESSAGEHANDLER_ENABLED" => IntegrationId.HttpMessageHandler,
        "DD_TRACE_HTTPSOCKETSHANDLER_ENABLED" => IntegrationId.HttpSocketsHandler,
        "DD_TRACE_WINHTTPHANDLER_ENABLED" => IntegrationId.WinHttpHandler,
        "DD_TRACE_CURLHANDLER_ENABLED" => IntegrationId.CurlHandler,
        "DD_TRACE_ASPNETCORE_ENABLED" => IntegrationId.AspNetCore,
        "DD_TRACE_ADONET_ENABLED" => IntegrationId.AdoNet,
        "DD_TRACE_ASPNET_ENABLED" => IntegrationId.AspNet,
        "DD_TRACE_ASPNETMVC_ENABLED" => IntegrationId.AspNetMvc,
        "DD_TRACE_ASPNETWEBAPI2_ENABLED" => IntegrationId.AspNetWebApi2,
        "DD_TRACE_GRAPHQL_ENABLED" => IntegrationId.GraphQL,
        "DD_TRACE_HOTCHOCOLATE_ENABLED" => IntegrationId.HotChocolate,
        "DD_TRACE_MONGODB_ENABLED" => IntegrationId.MongoDb,
        "DD_TRACE_XUNIT_ENABLED" => IntegrationId.XUnit,
        "DD_TRACE_NUNIT_ENABLED" => IntegrationId.NUnit,
        "DD_TRACE_MSTESTV2_ENABLED" => IntegrationId.MsTestV2,
        "DD_TRACE_WCF_ENABLED" => IntegrationId.Wcf,
        "DD_TRACE_WEBREQUEST_ENABLED" => IntegrationId.WebRequest,
        "DD_TRACE_ELASTICSEARCHNET_ENABLED" => IntegrationId.ElasticsearchNet,
        "DD_TRACE_SERVICESTACKREDIS_ENABLED" => IntegrationId.ServiceStackRedis,
        "DD_TRACE_STACKEXCHANGEREDIS_ENABLED" => IntegrationId.StackExchangeRedis,
        "DD_TRACE_SERVICEREMOTING_ENABLED" => IntegrationId.ServiceRemoting,
        "DD_TRACE_RABBITMQ_ENABLED" => IntegrationId.RabbitMQ,
        "DD_TRACE_MSMQ_ENABLED" => IntegrationId.Msmq,
        "DD_TRACE_KAFKA_ENABLED" => IntegrationId.Kafka,
        "DD_TRACE_COSMOSDB_ENABLED" => IntegrationId.CosmosDb,
        "DD_TRACE_AWSS3_ENABLED" => IntegrationId.AwsS3,
        "DD_TRACE_AWSSDK_ENABLED" => IntegrationId.AwsSdk,
        "DD_TRACE_AWSSQS_ENABLED" => IntegrationId.AwsSqs,
        "DD_TRACE_AWSSNS_ENABLED" => IntegrationId.AwsSns,
        "DD_TRACE_AWSEVENTBRIDGE_ENABLED" => IntegrationId.AwsEventBridge,
        "DD_TRACE_AWSLAMBDA_ENABLED" => IntegrationId.AwsLambda,
        "DD_TRACE_AWSSTEPFUNCTIONS_ENABLED" => IntegrationId.AwsStepFunctions,
        "DD_TRACE_ILOGGER_ENABLED" => IntegrationId.ILogger,
        "DD_TRACE_AEROSPIKE_ENABLED" => IntegrationId.Aerospike,
        "DD_TRACE_AZUREFUNCTIONS_ENABLED" => IntegrationId.AzureFunctions,
        "DD_TRACE_COUCHBASE_ENABLED" => IntegrationId.Couchbase,
        "DD_TRACE_MYSQL_ENABLED" => IntegrationId.MySql,
        "DD_TRACE_NPGSQL_ENABLED" => IntegrationId.Npgsql,
        "DD_TRACE_ORACLE_ENABLED" => IntegrationId.Oracle,
        "DD_TRACE_SQLCLIENT_ENABLED" => IntegrationId.SqlClient,
        "DD_TRACE_SQLITE_ENABLED" => IntegrationId.Sqlite,
        "DD_TRACE_SERILOG_ENABLED" => IntegrationId.Serilog,
        "DD_TRACE_LOG4NET_ENABLED" => IntegrationId.Log4Net,
        "DD_TRACE_NLOG_ENABLED" => IntegrationId.NLog,
        "DD_TRACE_TRACEANNOTATIONS_ENABLED" => IntegrationId.TraceAnnotations,
        "DD_TRACE_GRPC_ENABLED" => IntegrationId.Grpc,
        "DD_TRACE_PROCESS_ENABLED" => IntegrationId.Process,
        "DD_TRACE_HASHALGORITHM_ENABLED" => IntegrationId.HashAlgorithm,
        "DD_TRACE_SYMMETRICALGORITHM_ENABLED" => IntegrationId.SymmetricAlgorithm,
        "DD_TRACE_OPENTELEMETRY_ENABLED" => IntegrationId.OpenTelemetry,
        "DD_TRACE_PATHTRAVERSAL_ENABLED" => IntegrationId.PathTraversal,
        "DD_TRACE_LDAP_ENABLED" => IntegrationId.Ldap,
        "DD_TRACE_SSRF_ENABLED" => IntegrationId.Ssrf,
        "DD_TRACE_AWSKINESIS_ENABLED" => IntegrationId.AwsKinesis,
        "DD_TRACE_AZURESERVICEBUS_ENABLED" => IntegrationId.AzureServiceBus,
        "DD_TRACE_SYSTEMRANDOM_ENABLED" => IntegrationId.SystemRandom,
        "DD_TRACE_AWSDYNAMODB_ENABLED" => IntegrationId.AwsDynamoDb,
        "DD_TRACE_HARDCODEDSECRET_ENABLED" => IntegrationId.HardcodedSecret,
        "DD_TRACE_IBMMQ_ENABLED" => IntegrationId.IbmMq,
        "DD_TRACE_REMOTING_ENABLED" => IntegrationId.Remoting,
        "DD_TRACE_TRUSTBOUNDARYVIOLATION_ENABLED" => IntegrationId.TrustBoundaryViolation,
        "DD_TRACE_UNVALIDATEDREDIRECT_ENABLED" => IntegrationId.UnvalidatedRedirect,
        "DD_TRACE_TESTPLATFORMASSEMBLYRESOLVER_ENABLED" => IntegrationId.TestPlatformAssemblyResolver,
        "DD_TRACE_STACKTRACELEAK_ENABLED" => IntegrationId.StackTraceLeak,
        "DD_TRACE_XPATHINJECTION_ENABLED" => IntegrationId.XpathInjection,
        "DD_TRACE_REFLECTIONINJECTION_ENABLED" => IntegrationId.ReflectionInjection,
        "DD_TRACE_XSS_ENABLED" => IntegrationId.Xss,
        "DD_TRACE_NHIBERNATE_ENABLED" => IntegrationId.NHibernate,
        "DD_TRACE_DOTNETTEST_ENABLED" => IntegrationId.DotnetTest,
        "DD_TRACE_SELENIUM_ENABLED" => IntegrationId.Selenium,
        "DD_TRACE_DIRECTORYLISTINGLEAK_ENABLED" => IntegrationId.DirectoryListingLeak,
        "DD_TRACE_SESSIONTIMEOUT_ENABLED" => IntegrationId.SessionTimeout,
        "DD_TRACE_DATADOGTRACEMANUAL_ENABLED" => IntegrationId.DatadogTraceManual,
        "DD_TRACE_EMAILHTMLINJECTION_ENABLED" => IntegrationId.EmailHtmlInjection,
        _ => null,
    };

    // This list is fixed in time and doesn't need to be updated
    [TestingAndPrivateOnly]
    internal static IntegrationId? GetIntegrationAnalyticsEnabled(string key) => key switch
    {
        "DD_TRACE_HTTPMESSAGEHANDLER_ANALYTICS_ENABLED" => IntegrationId.HttpMessageHandler,
        "DD_TRACE_HTTPSOCKETSHANDLER_ANALYTICS_ENABLED" => IntegrationId.HttpSocketsHandler,
        "DD_TRACE_WINHTTPHANDLER_ANALYTICS_ENABLED" => IntegrationId.WinHttpHandler,
        "DD_TRACE_CURLHANDLER_ANALYTICS_ENABLED" => IntegrationId.CurlHandler,
        "DD_TRACE_ASPNETCORE_ANALYTICS_ENABLED" => IntegrationId.AspNetCore,
        "DD_TRACE_ADONET_ANALYTICS_ENABLED" => IntegrationId.AdoNet,
        "DD_TRACE_ASPNET_ANALYTICS_ENABLED" => IntegrationId.AspNet,
        "DD_TRACE_ASPNETMVC_ANALYTICS_ENABLED" => IntegrationId.AspNetMvc,
        "DD_TRACE_ASPNETWEBAPI2_ANALYTICS_ENABLED" => IntegrationId.AspNetWebApi2,
        "DD_TRACE_GRAPHQL_ANALYTICS_ENABLED" => IntegrationId.GraphQL,
        "DD_TRACE_HOTCHOCOLATE_ANALYTICS_ENABLED" => IntegrationId.HotChocolate,
        "DD_TRACE_MONGODB_ANALYTICS_ENABLED" => IntegrationId.MongoDb,
        "DD_TRACE_XUNIT_ANALYTICS_ENABLED" => IntegrationId.XUnit,
        "DD_TRACE_NUNIT_ANALYTICS_ENABLED" => IntegrationId.NUnit,
        "DD_TRACE_MSTESTV2_ANALYTICS_ENABLED" => IntegrationId.MsTestV2,
        "DD_TRACE_WCF_ANALYTICS_ENABLED" => IntegrationId.Wcf,
        "DD_TRACE_WEBREQUEST_ANALYTICS_ENABLED" => IntegrationId.WebRequest,
        "DD_TRACE_ELASTICSEARCHNET_ANALYTICS_ENABLED" => IntegrationId.ElasticsearchNet,
        "DD_TRACE_SERVICESTACKREDIS_ANALYTICS_ENABLED" => IntegrationId.ServiceStackRedis,
        "DD_TRACE_STACKEXCHANGEREDIS_ANALYTICS_ENABLED" => IntegrationId.StackExchangeRedis,
        "DD_TRACE_SERVICEREMOTING_ANALYTICS_ENABLED" => IntegrationId.ServiceRemoting,
        "DD_TRACE_RABBITMQ_ANALYTICS_ENABLED" => IntegrationId.RabbitMQ,
        "DD_TRACE_MSMQ_ANALYTICS_ENABLED" => IntegrationId.Msmq,
        "DD_TRACE_KAFKA_ANALYTICS_ENABLED" => IntegrationId.Kafka,
        "DD_TRACE_COSMOSDB_ANALYTICS_ENABLED" => IntegrationId.CosmosDb,
        "DD_TRACE_AWSSDK_ANALYTICS_ENABLED" => IntegrationId.AwsSdk,
        "DD_TRACE_AWSSQS_ANALYTICS_ENABLED" => IntegrationId.AwsSqs,
        "DD_TRACE_AWSSNS_ANALYTICS_ENABLED" => IntegrationId.AwsSns,
        "DD_TRACE_AWSEVENTBRIDGE_ANALYTICS_ENABLED" => IntegrationId.AwsEventBridge,
        "DD_TRACE_AWSS3_ANALYTICS_ENABLED" => IntegrationId.AwsS3,
        "DD_TRACE_AWSLAMBDA_ANALYTICS_ENABLED" => IntegrationId.AwsLambda,
        "DD_TRACE_AWSSTEPFUNCTIONS_ANALYTICS_ENABLED" => IntegrationId.AwsStepFunctions,
        "DD_TRACE_ILOGGER_ANALYTICS_ENABLED" => IntegrationId.ILogger,
        "DD_TRACE_AEROSPIKE_ANALYTICS_ENABLED" => IntegrationId.Aerospike,
        "DD_TRACE_AZUREFUNCTIONS_ANALYTICS_ENABLED" => IntegrationId.AzureFunctions,
        "DD_TRACE_COUCHBASE_ANALYTICS_ENABLED" => IntegrationId.Couchbase,
        "DD_TRACE_MYSQL_ANALYTICS_ENABLED" => IntegrationId.MySql,
        "DD_TRACE_NPGSQL_ANALYTICS_ENABLED" => IntegrationId.Npgsql,
        "DD_TRACE_ORACLE_ANALYTICS_ENABLED" => IntegrationId.Oracle,
        "DD_TRACE_SQLCLIENT_ANALYTICS_ENABLED" => IntegrationId.SqlClient,
        "DD_TRACE_SQLITE_ANALYTICS_ENABLED" => IntegrationId.Sqlite,
        "DD_TRACE_SERILOG_ANALYTICS_ENABLED" => IntegrationId.Serilog,
        "DD_TRACE_LOG4NET_ANALYTICS_ENABLED" => IntegrationId.Log4Net,
        "DD_TRACE_NLOG_ANALYTICS_ENABLED" => IntegrationId.NLog,
        "DD_TRACE_TRACEANNOTATIONS_ANALYTICS_ENABLED" => IntegrationId.TraceAnnotations,
        "DD_TRACE_GRPC_ANALYTICS_ENABLED" => IntegrationId.Grpc,
        "DD_TRACE_PROCESS_ANALYTICS_ENABLED" => IntegrationId.Process,
        "DD_TRACE_HASHALGORITHM_ANALYTICS_ENABLED" => IntegrationId.HashAlgorithm,
        "DD_TRACE_SYMMETRICALGORITHM_ANALYTICS_ENABLED" => IntegrationId.SymmetricAlgorithm,
        "DD_TRACE_OPENTELEMETRY_ANALYTICS_ENABLED" => IntegrationId.OpenTelemetry,
        "DD_TRACE_PATHTRAVERSAL_ANALYTICS_ENABLED" => IntegrationId.PathTraversal,
        "DD_TRACE_LDAP_ANALYTICS_ENABLED" => IntegrationId.Ldap,
        "DD_TRACE_SSRF_ANALYTICS_ENABLED" => IntegrationId.Ssrf,
        "DD_TRACE_AWSKINESIS_ANALYTICS_ENABLED" => IntegrationId.AwsKinesis,
        "DD_TRACE_AZURESERVICEBUS_ANALYTICS_ENABLED" => IntegrationId.AzureServiceBus,
        "DD_TRACE_SYSTEMRANDOM_ANALYTICS_ENABLED" => IntegrationId.SystemRandom,
        "DD_TRACE_AWSDYNAMODB_ANALYTICS_ENABLED" => IntegrationId.AwsDynamoDb,
        "DD_TRACE_HARDCODEDSECRET_ANALYTICS_ENABLED" => IntegrationId.HardcodedSecret,
        "DD_TRACE_IBMMQ_ANALYTICS_ENABLED" => IntegrationId.IbmMq,
        "DD_TRACE_REMOTING_ANALYTICS_ENABLED" => IntegrationId.Remoting,
        "DD_TRACE_TRUSTBOUNDARYVIOLATION_ANALYTICS_ENABLED" => IntegrationId.TrustBoundaryViolation,
        "DD_TRACE_UNVALIDATEDREDIRECT_ANALYTICS_ENABLED" => IntegrationId.UnvalidatedRedirect,
        "DD_TRACE_TESTPLATFORMASSEMBLYRESOLVER_ANALYTICS_ENABLED" => IntegrationId.TestPlatformAssemblyResolver,
        "DD_TRACE_STACKTRACELEAK_ANALYTICS_ENABLED" => IntegrationId.StackTraceLeak,
        "DD_TRACE_XPATHINJECTION_ANALYTICS_ENABLED" => IntegrationId.XpathInjection,
        "DD_TRACE_REFLECTIONINJECTION_ANALYTICS_ENABLED" => IntegrationId.ReflectionInjection,
        "DD_TRACE_XSS_ANALYTICS_ENABLED" => IntegrationId.Xss,
        "DD_TRACE_NHIBERNATE_ANALYTICS_ENABLED" => IntegrationId.NHibernate,
        "DD_TRACE_DOTNETTEST_ANALYTICS_ENABLED" => IntegrationId.DotnetTest,
        "DD_TRACE_SELENIUM_ANALYTICS_ENABLED" => IntegrationId.Selenium,
        "DD_TRACE_DIRECTORYLISTINGLEAK_ANALYTICS_ENABLED" => IntegrationId.DirectoryListingLeak,
        "DD_TRACE_SESSIONTIMEOUT_ANALYTICS_ENABLED" => IntegrationId.SessionTimeout,
        "DD_TRACE_DATADOGTRACEMANUAL_ANALYTICS_ENABLED" => IntegrationId.DatadogTraceManual,
        "DD_TRACE_EMAILHTMLINJECTION_ANALYTICS_ENABLED" => IntegrationId.EmailHtmlInjection,
        _ => null,
    };

    // This list is fixed in time and doesn't need to be updated
    [TestingAndPrivateOnly]
    internal static IntegrationId? GetIntegrationAnalyticsSampleRate(string key) => key switch
    {
        "DD_TRACE_HTTPMESSAGEHANDLER_ANALYTICS_SAMPLE_RATE" => IntegrationId.HttpMessageHandler,
        "DD_TRACE_HTTPSOCKETSHANDLER_ANALYTICS_SAMPLE_RATE" => IntegrationId.HttpSocketsHandler,
        "DD_TRACE_WINHTTPHANDLER_ANALYTICS_SAMPLE_RATE" => IntegrationId.WinHttpHandler,
        "DD_TRACE_CURLHANDLER_ANALYTICS_SAMPLE_RATE" => IntegrationId.CurlHandler,
        "DD_TRACE_ASPNETCORE_ANALYTICS_SAMPLE_RATE" => IntegrationId.AspNetCore,
        "DD_TRACE_ADONET_ANALYTICS_SAMPLE_RATE" => IntegrationId.AdoNet,
        "DD_TRACE_ASPNET_ANALYTICS_SAMPLE_RATE" => IntegrationId.AspNet,
        "DD_TRACE_ASPNETMVC_ANALYTICS_SAMPLE_RATE" => IntegrationId.AspNetMvc,
        "DD_TRACE_ASPNETWEBAPI2_ANALYTICS_SAMPLE_RATE" => IntegrationId.AspNetWebApi2,
        "DD_TRACE_GRAPHQL_ANALYTICS_SAMPLE_RATE" => IntegrationId.GraphQL,
        "DD_TRACE_HOTCHOCOLATE_ANALYTICS_SAMPLE_RATE" => IntegrationId.HotChocolate,
        "DD_TRACE_MONGODB_ANALYTICS_SAMPLE_RATE" => IntegrationId.MongoDb,
        "DD_TRACE_XUNIT_ANALYTICS_SAMPLE_RATE" => IntegrationId.XUnit,
        "DD_TRACE_NUNIT_ANALYTICS_SAMPLE_RATE" => IntegrationId.NUnit,
        "DD_TRACE_MSTESTV2_ANALYTICS_SAMPLE_RATE" => IntegrationId.MsTestV2,
        "DD_TRACE_WCF_ANALYTICS_SAMPLE_RATE" => IntegrationId.Wcf,
        "DD_TRACE_WEBREQUEST_ANALYTICS_SAMPLE_RATE" => IntegrationId.WebRequest,
        "DD_TRACE_ELASTICSEARCHNET_ANALYTICS_SAMPLE_RATE" => IntegrationId.ElasticsearchNet,
        "DD_TRACE_SERVICESTACKREDIS_ANALYTICS_SAMPLE_RATE" => IntegrationId.ServiceStackRedis,
        "DD_TRACE_STACKEXCHANGEREDIS_ANALYTICS_SAMPLE_RATE" => IntegrationId.StackExchangeRedis,
        "DD_TRACE_SERVICEREMOTING_ANALYTICS_SAMPLE_RATE" => IntegrationId.ServiceRemoting,
        "DD_TRACE_RABBITMQ_ANALYTICS_SAMPLE_RATE" => IntegrationId.RabbitMQ,
        "DD_TRACE_MSMQ_ANALYTICS_SAMPLE_RATE" => IntegrationId.Msmq,
        "DD_TRACE_KAFKA_ANALYTICS_SAMPLE_RATE" => IntegrationId.Kafka,
        "DD_TRACE_COSMOSDB_ANALYTICS_SAMPLE_RATE" => IntegrationId.CosmosDb,
        "DD_TRACE_AWSS3_ANALYTICS_SAMPLE_RATE" => IntegrationId.AwsS3,
        "DD_TRACE_AWSSDK_ANALYTICS_SAMPLE_RATE" => IntegrationId.AwsSdk,
        "DD_TRACE_AWSSQS_ANALYTICS_SAMPLE_RATE" => IntegrationId.AwsSqs,
        "DD_TRACE_AWSSNS_ANALYTICS_SAMPLE_RATE" => IntegrationId.AwsSns,
        "DD_TRACE_AWSEVENTBRIDGE_ANALYTICS_SAMPLE_RATE" => IntegrationId.AwsEventBridge,
        "DD_TRACE_AWSLAMBDA_ANALYTICS_SAMPLE_RATE" => IntegrationId.AwsLambda,
        "DD_TRACE_AWSSTEPFUNCTIONS_ANALYTICS_SAMPLE_RATE" => IntegrationId.AwsStepFunctions,
        "DD_TRACE_ILOGGER_ANALYTICS_SAMPLE_RATE" => IntegrationId.ILogger,
        "DD_TRACE_AEROSPIKE_ANALYTICS_SAMPLE_RATE" => IntegrationId.Aerospike,
        "DD_TRACE_AZUREFUNCTIONS_ANALYTICS_SAMPLE_RATE" => IntegrationId.AzureFunctions,
        "DD_TRACE_COUCHBASE_ANALYTICS_SAMPLE_RATE" => IntegrationId.Couchbase,
        "DD_TRACE_MYSQL_ANALYTICS_SAMPLE_RATE" => IntegrationId.MySql,
        "DD_TRACE_NPGSQL_ANALYTICS_SAMPLE_RATE" => IntegrationId.Npgsql,
        "DD_TRACE_ORACLE_ANALYTICS_SAMPLE_RATE" => IntegrationId.Oracle,
        "DD_TRACE_SQLCLIENT_ANALYTICS_SAMPLE_RATE" => IntegrationId.SqlClient,
        "DD_TRACE_SQLITE_ANALYTICS_SAMPLE_RATE" => IntegrationId.Sqlite,
        "DD_TRACE_SERILOG_ANALYTICS_SAMPLE_RATE" => IntegrationId.Serilog,
        "DD_TRACE_LOG4NET_ANALYTICS_SAMPLE_RATE" => IntegrationId.Log4Net,
        "DD_TRACE_NLOG_ANALYTICS_SAMPLE_RATE" => IntegrationId.NLog,
        "DD_TRACE_TRACEANNOTATIONS_ANALYTICS_SAMPLE_RATE" => IntegrationId.TraceAnnotations,
        "DD_TRACE_GRPC_ANALYTICS_SAMPLE_RATE" => IntegrationId.Grpc,
        "DD_TRACE_PROCESS_ANALYTICS_SAMPLE_RATE" => IntegrationId.Process,
        "DD_TRACE_HASHALGORITHM_ANALYTICS_SAMPLE_RATE" => IntegrationId.HashAlgorithm,
        "DD_TRACE_SYMMETRICALGORITHM_ANALYTICS_SAMPLE_RATE" => IntegrationId.SymmetricAlgorithm,
        "DD_TRACE_OPENTELEMETRY_ANALYTICS_SAMPLE_RATE" => IntegrationId.OpenTelemetry,
        "DD_TRACE_PATHTRAVERSAL_ANALYTICS_SAMPLE_RATE" => IntegrationId.PathTraversal,
        "DD_TRACE_LDAP_ANALYTICS_SAMPLE_RATE" => IntegrationId.Ldap,
        "DD_TRACE_SSRF_ANALYTICS_SAMPLE_RATE" => IntegrationId.Ssrf,
        "DD_TRACE_AWSKINESIS_ANALYTICS_SAMPLE_RATE" => IntegrationId.AwsKinesis,
        "DD_TRACE_AZURESERVICEBUS_ANALYTICS_SAMPLE_RATE" => IntegrationId.AzureServiceBus,
        "DD_TRACE_SYSTEMRANDOM_ANALYTICS_SAMPLE_RATE" => IntegrationId.SystemRandom,
        "DD_TRACE_AWSDYNAMODB_ANALYTICS_SAMPLE_RATE" => IntegrationId.AwsDynamoDb,
        "DD_TRACE_HARDCODEDSECRET_ANALYTICS_SAMPLE_RATE" => IntegrationId.HardcodedSecret,
        "DD_TRACE_IBMMQ_ANALYTICS_SAMPLE_RATE" => IntegrationId.IbmMq,
        "DD_TRACE_REMOTING_ANALYTICS_SAMPLE_RATE" => IntegrationId.Remoting,
        "DD_TRACE_TRUSTBOUNDARYVIOLATION_ANALYTICS_SAMPLE_RATE" => IntegrationId.TrustBoundaryViolation,
        "DD_TRACE_UNVALIDATEDREDIRECT_ANALYTICS_SAMPLE_RATE" => IntegrationId.UnvalidatedRedirect,
        "DD_TRACE_TESTPLATFORMASSEMBLYRESOLVER_ANALYTICS_SAMPLE_RATE" => IntegrationId.TestPlatformAssemblyResolver,
        "DD_TRACE_STACKTRACELEAK_ANALYTICS_SAMPLE_RATE" => IntegrationId.StackTraceLeak,
        "DD_TRACE_XPATHINJECTION_ANALYTICS_SAMPLE_RATE" => IntegrationId.XpathInjection,
        "DD_TRACE_REFLECTIONINJECTION_ANALYTICS_SAMPLE_RATE" => IntegrationId.ReflectionInjection,
        "DD_TRACE_XSS_ANALYTICS_SAMPLE_RATE" => IntegrationId.Xss,
        "DD_TRACE_NHIBERNATE_ANALYTICS_SAMPLE_RATE" => IntegrationId.NHibernate,
        "DD_TRACE_DOTNETTEST_ANALYTICS_SAMPLE_RATE" => IntegrationId.DotnetTest,
        "DD_TRACE_SELENIUM_ANALYTICS_SAMPLE_RATE" => IntegrationId.Selenium,
        "DD_TRACE_DIRECTORYLISTINGLEAK_ANALYTICS_SAMPLE_RATE" => IntegrationId.DirectoryListingLeak,
        "DD_TRACE_SESSIONTIMEOUT_ANALYTICS_SAMPLE_RATE" => IntegrationId.SessionTimeout,
        "DD_TRACE_DATADOGTRACEMANUAL_ANALYTICS_SAMPLE_RATE" => IntegrationId.DatadogTraceManual,
        "DD_TRACE_EMAILHTMLINJECTION_ANALYTICS_SAMPLE_RATE" => IntegrationId.EmailHtmlInjection,
        _ => null,
    };

    // This list is fixed in time and doesn't need to be updated
    [TestingAndPrivateOnly]
    internal static PublicApiUsage? GetTelemetryKey(string key) => key switch
    {
        TracerSettingKeyConstants.AgentUriKey => PublicApiUsage.ExporterSettings_AgentUri_Set,
        TracerSettingKeyConstants.AnalyticsEnabledKey => PublicApiUsage.TracerSettings_AnalyticsEnabled_Set,
        TracerSettingKeyConstants.CustomSamplingRules => PublicApiUsage.TracerSettings_CustomSamplingRules_Set,
        TracerSettingKeyConstants.DiagnosticSourceEnabledKey => PublicApiUsage.TracerSettings_DiagnosticSourceEnabled_Set,
        TracerSettingKeyConstants.DisabledIntegrationNamesKey => PublicApiUsage.TracerSettings_DisabledIntegrationNames_Set,
        TracerSettingKeyConstants.EnvironmentKey => PublicApiUsage.TracerSettings_Environment_Set,
        TracerSettingKeyConstants.GlobalSamplingRateKey => PublicApiUsage.TracerSettings_GlobalSamplingRate_Set,
        TracerSettingKeyConstants.GrpcTags => PublicApiUsage.TracerSettings_GrpcTags_Set,
        TracerSettingKeyConstants.HeaderTags => PublicApiUsage.TracerSettings_HeaderTags_Set,
        TracerSettingKeyConstants.GlobalTagsKey => PublicApiUsage.TracerSettings_GlobalTags_Set,
        TracerSettingKeyConstants.HttpClientErrorCodesKey => PublicApiUsage.TracerSettings_SetHttpClientErrorStatusCodes,
        TracerSettingKeyConstants.HttpServerErrorCodesKey => PublicApiUsage.TracerSettings_SetHttpServerErrorStatusCodes,
        TracerSettingKeyConstants.KafkaCreateConsumerScopeEnabledKey => PublicApiUsage.TracerSettings_KafkaCreateConsumerScopeEnabled_Set,
        TracerSettingKeyConstants.LogsInjectionEnabledKey => PublicApiUsage.TracerSettings_LogsInjectionEnabled_Set,
        TracerSettingKeyConstants.ServiceNameKey => PublicApiUsage.TracerSettings_ServiceName_Set,
        TracerSettingKeyConstants.ServiceNameMappingsKey => PublicApiUsage.TracerSettings_SetServiceNameMappings,
        TracerSettingKeyConstants.MaxTracesSubmittedPerSecondKey => PublicApiUsage.TracerSettings_MaxTracesSubmittedPerSecond_Set,
        TracerSettingKeyConstants.ServiceVersionKey => PublicApiUsage.TracerSettings_ServiceVersion_Set,
        TracerSettingKeyConstants.StartupDiagnosticLogEnabledKey => PublicApiUsage.TracerSettings_StartupDiagnosticLogEnabled_Set,
        TracerSettingKeyConstants.StatsComputationEnabledKey => PublicApiUsage.TracerSettings_StatsComputationEnabled_Set,
        TracerSettingKeyConstants.TraceEnabledKey => PublicApiUsage.TracerSettings_TraceEnabled_Set,
        TracerSettingKeyConstants.TracerMetricsEnabledKey => PublicApiUsage.TracerSettings_TracerMetricsEnabled_Set,
        _ => null,
    };

    private static object RemapResult(string key, object value) => key switch
    {
        TracerSettingKeyConstants.AgentUriKey => value is Uri uri ? uri.ToString() : value,
        TracerSettingKeyConstants.DisabledIntegrationNamesKey => value is HashSet<string> set ? string.Join(";", set) : value,
        TracerSettingKeyConstants.HttpServerErrorCodesKey => value is List<int> list
                                                                 ? string.Join(",", list.Select(i => i.ToString(CultureInfo.InvariantCulture)))
                                                                 : value,
        TracerSettingKeyConstants.HttpClientErrorCodesKey => value is List<int> list
                                                                 ? string.Join(",", list.Select(i => i.ToString(CultureInfo.InvariantCulture)))
                                                                 : value,
        _ => value,
    };
}
