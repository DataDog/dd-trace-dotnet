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
internal static class MetricTags
{
    internal enum LogLevel
    {
        [Description("level:debug")] Debug,
        [Description("level:info")] Information,
        [Description("level:warn")] Warning,
        [Description("level:error")] Error,
    }

    internal enum InitializationComponent
    {
        [Description("component:total")] Total,
        [Description("component:byref_pinvoke")] ByRefPinvoke,
        [Description("component:calltarget_state_byref_pinvoke")] CallTargetStateByRefPinvoke,
        [Description("component:traceattributes_pinvoke")] TraceAttributesPinvoke,
        [Description("component:managed")] Managed,
        [Description("component:calltarget_defs_pinvoke")] CallTargetDefsPinvoke,
        [Description("component:serverless")] Serverless,
        [Description("component:calltarget_derived_defs_pinvoke")] CallTargetDerivedDefsPinvoke,
        [Description("component:calltarget_interface_defs_pinvoke")] CallTargetInterfaceDefsPinvoke,
        [Description("component:discovery_service")] DiscoveryService,
        [Description("component:rcm")] Rcm,
        [Description("component:dynamic_instrumentation")] DynamicInstrumentation,
        [Description("component:tracemethods_pinvoke")] TraceMethodsPinvoke,
        [Description("component:iast")] Iast,
    }

    internal enum TraceContinuation
    {
        [Description("new_continued:new")] New,
        [Description("new_continued:continued")] Continued,
    }

    internal enum SpanEnqueueReason
    {
        /// <summary>
        /// The span was part of a p0 trace that was kept for sending to the agent
        /// </summary>
        [Description("reason:p0_keep")] P0Keep,

        /// <summary>
        /// The span was selected via single_span_sampling, and otherwise would have been dropped as a p0 span
        /// </summary>
        [Description("reason:single_span_sampling")] SingleSpanSampling,

        /// <summary>
        /// The tracer is not dropping p0 spans, so the span was enqueued 'by default' for sending to the trace-agent
        /// </summary>
        [Description("reason:default")] Default,
    }

    internal enum TraceChunkEnqueueReason
    {
        /// <summary>
        /// The span was part of a p0 trace that was kept for sending to the agent
        /// </summary>
        [Description("reason:p0_keep")] P0Keep,

        /// <summary>
        /// The tracer is not dropping p0 spans, so the span was enqueued 'by default' for sending to the trace-agent
        /// </summary>
        [Description("reason:default")] Default,
    }

    internal enum DropReason
    {
        [Description("reason:p0_drop")] P0Drop,
        [Description("reason:overfull_buffer")] OverfullBuffer,
        [Description("reason:serialization_error")] SerializationError,
        [Description("reason:api_error")] ApiError,
    }

    internal enum StatusCode
    {
        [Description("status_code:200")] Code200,
        [Description("status_code:201")] Code201,
        [Description("status_code:202")] Code202,
        [Description("status_code:204")] Code204,
        [Description("status_code:2xx")] Code2xx,
        [Description("status_code:301")] Code301,
        [Description("status_code:302")] Code302,
        [Description("status_code:307")] Code307,
        [Description("status_code:308")] Code308,
        [Description("status_code:3xx")] Code3xx,
        [Description("status_code:400")] Code400,
        [Description("status_code:401")] Code401,
        [Description("status_code:403")] Code403,
        [Description("status_code:404")] Code404,
        [Description("status_code:405")] Code405,
        [Description("status_code:4xx")] Code4xx,
        [Description("status_code:500")] Code500,
        [Description("status_code:501")] Code501,
        [Description("status_code:502")] Code502,
        [Description("status_code:503")] Code503,
        [Description("status_code:504")] Code504,
        [Description("status_code:5xx")] Code5xx,
    }

    internal enum ApiError
    {
        [Description("type:timeout")] Timeout,
        [Description("type:network")] NetworkError,
        [Description("type:status_code")] StatusCode,
    }

    internal enum PartialFlushReason
    {
        [Description("reason:large_trace")] LargeTrace,
        [Description("reason:single_span_ingestion")] SingleSpanIngestion,
    }

    internal enum ContextHeaderStyle
    {
        [Description("header_style:tracecontext")] TraceContext,
        [Description("header_style:datadog")] Datadog,
        [Description("header_style:b3multi")] B3Multi,
        [Description("header_style:b3single")] B3SingleHeader,
    }

    internal enum TelemetryEndpoint
    {
        [Description("endpoint:agent")] Agent,
        [Description("endpoint:agentless")] Agentless,
    }

    internal enum InstrumentationComponent
    {
        [Description("component_name:calltarget")] CallTarget,
        [Description("component_name:calltarget_derived")] CallTargetDerived,
        [Description("component_name:calltarget_interfaces")] CallTargetInterfaces,
        [Description("component_name:iast")] Iast,
        [Description("component_name:iast_derived")] IastDerived,
        [Description("component_name:iast_aspects")] IastAspects,
    }

    internal enum IntegrationName
    {
        // manual integration
        [Description("integrations_name:datadog")]Manual,
        [Description("integrations_name:opentracing")]OpenTracing,
        // automatic integration
        [Description("integrations_name:httpmessagehandler")]HttpMessageHandler,
        [Description("integrations_name:httpsocketshandler")]HttpSocketsHandler,
        [Description("integrations_name:winhttphandler")]WinHttpHandler,
        [Description("integrations_name:curlhandler")]CurlHandler,
        [Description("integrations_name:aspnetcore")]AspNetCore,
        [Description("integrations_name:adonet")]AdoNet,
        [Description("integrations_name:aspnet")]AspNet,
        [Description("integrations_name:aspnetmvc")]AspNetMvc,
        [Description("integrations_name:aspnetwebapi2")]AspNetWebApi2,
        [Description("integrations_name:graphql")]GraphQL,
        [Description("integrations_name:hotchocolate")]HotChocolate,
        [Description("integrations_name:mongodb")]MongoDb,
        [Description("integrations_name:xunit")]XUnit,
        [Description("integrations_name:nunit")]NUnit,
        [Description("integrations_name:mstestv2")]MsTestV2,
        [Description("integrations_name:wcf")]Wcf,
        [Description("integrations_name:webrequest")]WebRequest,
        [Description("integrations_name:elasticsearchnet")]ElasticsearchNet,
        [Description("integrations_name:servicestackredis")]ServiceStackRedis,
        [Description("integrations_name:stackexchangeredis")]StackExchangeRedis,
        [Description("integrations_name:serviceremoting")]ServiceRemoting,
        [Description("integrations_name:rabbitmq")]RabbitMQ,
        [Description("integrations_name:msmq")]Msmq,
        [Description("integrations_name:kafka")]Kafka,
        [Description("integrations_name:cosmosdb")]CosmosDb,
        [Description("integrations_name:awssdk")]AwsSdk,
        [Description("integrations_name:awssqs")]AwsSqs,
        [Description("integrations_name:awssns")]AwsSns,
        [Description("integrations_name:ilogger")]ILogger,
        [Description("integrations_name:aerospike")]Aerospike,
        [Description("integrations_name:azurefunctions")]AzureFunctions,
        [Description("integrations_name:couchbase")]Couchbase,
        [Description("integrations_name:mysql")]MySql,
        [Description("integrations_name:npgsql")]Npgsql,
        [Description("integrations_name:oracle")]Oracle,
        [Description("integrations_name:sqlclient")]SqlClient,
        [Description("integrations_name:sqlite")]Sqlite,
        [Description("integrations_name:serilog")]Serilog,
        [Description("integrations_name:log4net")]Log4Net,
        [Description("integrations_name:nlog")]NLog,
        [Description("integrations_name:traceannotations")]TraceAnnotations,
        [Description("integrations_name:grpc")]Grpc,
        [Description("integrations_name:process")]Process,
        [Description("integrations_name:hashalgorithm")]HashAlgorithm,
        [Description("integrations_name:symmetricalgorithm")]SymmetricAlgorithm,
        [Description("integrations_name:opentelemetry")]OpenTelemetry,
        [Description("integrations_name:pathtraversal")]PathTraversal,
        [Description("integrations_name:aws_lambda")]AwsLambda,
    }

    public enum InstrumentationError
    {
        [Description("error_type:duck_typing")]DuckTyping,
        [Description("error_type:invoker")]Invoker,
        [Description("error_type:execution")]Execution,
    }
}
