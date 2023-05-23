// <copyright file="MetricTags.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Datadog.Trace.SourceGenerators;

namespace Datadog.Trace.Telemetry.Metrics;

[SuppressMessage("StyleCop.CSharp.ReadabilityRules", "SA1134:Attributes should not share line", Justification = "It's easier to read")]
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "It's easier to read")]
[EnumExtensions]
internal enum MetricTags
{
    None,

    // Tracer components
    [Description("component:total")] Total,
    [Description("component:byref_pinvoke")] Component_ByRefPinvoke,
    [Description("component:calltarget_state_byref_pinvoke")] Component_CallTargetStateByRefPinvoke,
    [Description("component:traceattributes_pinvoke")] Component_TraceAttributesPinvoke,
    [Description("component:managed")] Component_Managed,
    [Description("component:calltarget_defs_pinvoke")] Component_CallTargetDefsPinvoke,
    [Description("component:serverless")] Component_Serverless,
    [Description("component:calltarget_derived_defs_pinvoke")] Component_CallTargetDerivedDefsPinvoke,
    [Description("component:calltarget_interface_defs_pinvoke")] Component_CallTargetInterfaceDefsPinvoke,
    [Description("component:discovery_service")] Component_DiscoveryService,
    [Description("component:rcm")] Component_RCM,
    [Description("component:dynamic_instrumentation")] Component_DynamicInstrumentation,
    [Description("component:tracemethods_pinvoke")] Component_TraceMethodsPinvoke,
    [Description("component:iast")] Component_Iast,

    // Span/trace drop reasons
    [Description("reason:sampling_decision")] DropReason_SamplingDecision,
    [Description("reason:single_span_sampling")] DropReason_SingleSpanSampling,
    [Description("reason:overfull_buffer")] DropReason_OverfullBuffer,
    [Description("reason:serialization_error")] DropReason_SerializationError,
    [Description("reason:api_error")] DropReason_ApiError,

    // New or continued
    [Description("new_continued:new")] TraceCreated_New,
    [Description("new_continued:continued")] TraceCreated_Continued,

    // Status code (bit messy I know)
    [Description("status_code:200")] StatusCode_200,
    [Description("status_code:201")] StatusCode_201,
    [Description("status_code:202")] StatusCode_202,
    [Description("status_code:204")] StatusCode_204,
    [Description("status_code:2xx")] StatusCode_2xx,
    [Description("status_code:301")] StatusCode_301,
    [Description("status_code:302")] StatusCode_302,
    [Description("status_code:307")] StatusCode_307,
    [Description("status_code:308")] StatusCode_308,
    [Description("status_code:3xx")] StatusCode_3xx,
    [Description("status_code:400")] StatusCode_400,
    [Description("status_code:401")] StatusCode_401,
    [Description("status_code:403")] StatusCode_403,
    [Description("status_code:404")] StatusCode_404,
    [Description("status_code:405")] StatusCode_405,
    [Description("status_code:4xx")] StatusCode_4xx,
    [Description("status_code:500")] StatusCode_500,
    [Description("status_code:501")] StatusCode_501,
    [Description("status_code:502")] StatusCode_502,
    [Description("status_code:503")] StatusCode_503,
    [Description("status_code:504")] StatusCode_504,
    [Description("status_code:5xx")] StatusCode_5xx,

    // API Errors
    [Description("type:timeout")] ApiError_Timeout,
    [Description("type:network_error")] ApiError_NetworkError,
    [Description("type:status_code")] ApiError_StatusCode,

    // Partial Flush Reason
    [Description("reason:large_trace")] PartialFlushReason_LargeTrace,
    [Description("reason:single_span_ingestion")] PartialFlushReason_SingleSpanIngestion,

    // ContextHeaderStyle
    [Description("header_style:tracecontext")] ContextHeaderStyle_TraceContext,
    [Description("header_style:datadog")] ContextHeaderStyle_Datadog,
    [Description("header_style:b3multi")] ContextHeaderStyle_B3Multi,
    [Description("header_style:b3single")] ContextHeaderStyle_B3SingleHeader,

    // TelemetryAPI endpoint
    [Description("endpoint:agent")] TelemetryApi_Agent,
    [Description("endpoint:agentless")] TelemetryApi_Agentless,

    // Instrumentation counts
    [Description("component_name:trace_annotations")] InstrumentationCounts_TraceAnnotations,
    [Description("component_name:dd_trace_methods")] InstrumentationCounts_DDTraceMethods,
    [Description("component_name:calltarget")] InstrumentationCounts_CallTarget,
    [Description("component_name:calltarget_derived")] InstrumentationCounts_CallTargetDerived,

    // Integration name
    [Description("integrations_name:httpmessagehandler")]IntegrationName_HttpMessageHandler,
    [Description("integrations_name:aspnetcore")]IntegrationName_AspNetCore,
    [Description("integrations_name:adonet")]IntegrationName_AdoNet,
    [Description("integrations_name:aspnet")]IntegrationName_AspNet,
    [Description("integrations_name:aspnetmvc")]IntegrationName_AspNetMvc,
    [Description("integrations_name:aspnetwebapi2")]IntegrationName_AspNetWebApi2,
    [Description("integrations_name:graphql")]IntegrationName_GraphQL,
    [Description("integrations_name:hotchocolate")]IntegrationName_HotChocolate,
    [Description("integrations_name:mongodb")]IntegrationName_MongoDb,
    [Description("integrations_name:xunit")]IntegrationName_XUnit,
    [Description("integrations_name:nunit")]IntegrationName_NUnit,
    [Description("integrations_name:mstestv2")]IntegrationName_MsTestV2,
    [Description("integrations_name:wcf")]IntegrationName_Wcf,
    [Description("integrations_name:webrequest")]IntegrationName_WebRequest,
    [Description("integrations_name:elasticsearchnet")]IntegrationName_ElasticsearchNet,
    [Description("integrations_name:servicestackredis")]IntegrationName_ServiceStackRedis,
    [Description("integrations_name:stackexchangeredis")]IntegrationName_StackExchangeRedis,
    [Description("integrations_name:serviceremoting")]IntegrationName_ServiceRemoting,
    [Description("integrations_name:rabbitmq")]IntegrationName_RabbitMQ,
    [Description("integrations_name:msmq")]IntegrationName_Msmq,
    [Description("integrations_name:kafka")]IntegrationName_Kafka,
    [Description("integrations_name:cosmosdb")]IntegrationName_CosmosDb,
    [Description("integrations_name:awssdk")]IntegrationName_AwsSdk,
    [Description("integrations_name:awssqs")]IntegrationName_AwsSqs,
    [Description("integrations_name:ilogger")]IntegrationName_ILogger,
    [Description("integrations_name:aerospike")]IntegrationName_Aerospike,
    [Description("integrations_name:azurefunctions")]IntegrationName_AzureFunctions,
    [Description("integrations_name:couchbase")]IntegrationName_Couchbase,
    [Description("integrations_name:mysql")]IntegrationName_MySql,
    [Description("integrations_name:npgsql")]IntegrationName_Npgsql,
    [Description("integrations_name:oracle")]IntegrationName_Oracle,
    [Description("integrations_name:sqlclient")]IntegrationName_SqlClient,
    [Description("integrations_name:sqlite")]IntegrationName_Sqlite,
    [Description("integrations_name:serilog")]IntegrationName_Serilog,
    [Description("integrations_name:log4net")]IntegrationName_Log4Net,
    [Description("integrations_name:nlog")]IntegrationName_NLog,
    [Description("integrations_name:traceannotations")]IntegrationName_TraceAnnotations,
    [Description("integrations_name:grpc")]IntegrationName_Grpc,
    [Description("integrations_name:process")]IntegrationName_Process,
    [Description("integrations_name:hashalgorithm")]IntegrationName_HashAlgorithm,
    [Description("integrations_name:symmetricalgorithm")]IntegrationName_SymmetricAlgorithm,
    [Description("integrations_name:opentelemetry")]IntegrationName_OpenTelemetry,
    [Description("integrations_name:aws_lambda")]IntegrationName_AwsLambda,

    // Integration Component
    // TODO Add these
}
