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
        [Description("integration_name:datadog")]Manual,
        [Description("integration_name:opentracing")]OpenTracing,

        // automatic "custom" integration
        [Description("integration_name:ciapp")]CiAppManual,
        [Description("integration_name:debugger_span_probe")]DebuggerSpanProbe,
        [Description("integration_name:aws_lambda")]AwsLambda,
        [Description("integration_name:msbuild")]Msbuild,

        // automatic "standard" integration
        [Description("integration_name:httpmessagehandler")]HttpMessageHandler,
        [Description("integration_name:httpsocketshandler")]HttpSocketsHandler,
        [Description("integration_name:winhttphandler")]WinHttpHandler,
        [Description("integration_name:curlhandler")]CurlHandler,
        [Description("integration_name:aspnetcore")]AspNetCore,
        [Description("integration_name:adonet")]AdoNet,
        [Description("integration_name:aspnet")]AspNet,
        [Description("integration_name:aspnetmvc")]AspNetMvc,
        [Description("integration_name:aspnetwebapi2")]AspNetWebApi2,
        [Description("integration_name:graphql")]GraphQL,
        [Description("integration_name:hotchocolate")]HotChocolate,
        [Description("integration_name:mongodb")]MongoDb,
        [Description("integration_name:xunit")]XUnit,
        [Description("integration_name:nunit")]NUnit,
        [Description("integration_name:mstestv2")]MsTestV2,
        [Description("integration_name:wcf")]Wcf,
        [Description("integration_name:webrequest")]WebRequest,
        [Description("integration_name:elasticsearchnet")]ElasticsearchNet,
        [Description("integration_name:servicestackredis")]ServiceStackRedis,
        [Description("integration_name:stackexchangeredis")]StackExchangeRedis,
        [Description("integration_name:serviceremoting")]ServiceRemoting,
        [Description("integration_name:rabbitmq")]RabbitMQ,
        [Description("integration_name:msmq")]Msmq,
        [Description("integration_name:kafka")]Kafka,
        [Description("integration_name:cosmosdb")]CosmosDb,
        [Description("integration_name:awssdk")]AwsSdk,
        [Description("integration_name:awssqs")]AwsSqs,
        [Description("integration_name:awssns")]AwsSns,
        [Description("integration_name:ilogger")]ILogger,
        [Description("integration_name:aerospike")]Aerospike,
        [Description("integration_name:azurefunctions")]AzureFunctions,
        [Description("integration_name:couchbase")]Couchbase,
        [Description("integration_name:mysql")]MySql,
        [Description("integration_name:npgsql")]Npgsql,
        [Description("integration_name:oracle")]Oracle,
        [Description("integration_name:sqlclient")]SqlClient,
        [Description("integration_name:sqlite")]Sqlite,
        [Description("integration_name:serilog")]Serilog,
        [Description("integration_name:log4net")]Log4Net,
        [Description("integration_name:nlog")]NLog,
        [Description("integration_name:traceannotations")]TraceAnnotations,
        [Description("integration_name:grpc")]Grpc,
        [Description("integration_name:process")]Process,
        [Description("integration_name:hashalgorithm")]HashAlgorithm,
        [Description("integration_name:symmetricalgorithm")]SymmetricAlgorithm,
        [Description("integration_name:opentelemetry")]OpenTelemetry,
        [Description("integration_name:pathtraversal")]PathTraversal,
        [Description("integration_name:ssrf")] Ssrf,
        [Description("integration_name:ldap")] Ldap,
    }

    public enum InstrumentationError
    {
        [Description("error_type:duck_typing")]DuckTyping,
        [Description("error_type:invoker")]Invoker,
        [Description("error_type:execution")]Execution,
    }

    public enum WafAnalysis
    {
        // The generator splits on ; to add multiple tags
        // Note the initial 'waf_version'. This is an optimisation to avoid multiple array allocations
        // It is replaced with the "real" waf_version at runtime
        // CAUTION: waf_version should aways be placed in first position
        [Description("waf_version;rule_triggered:false;request_blocked:false;waf_timeout:false;request_excluded:false")]Normal,
        [Description("waf_version;rule_triggered:true;request_blocked:false;waf_timeout:false;request_excluded:false")]RuleTriggered,
        [Description("waf_version;rule_triggered:true;request_blocked:true;waf_timeout:false;request_excluded:false")]RuleTriggeredAndBlocked,
        [Description("waf_version;rule_triggered:false;request_blocked:false;waf_timeout:true;request_excluded:false")]WafTimeout,
        [Description("waf_version;rule_triggered:false;request_blocked:false;waf_timeout:false;request_excluded:true")]RequestExcludedViaFilter,
    }

    public enum IastInstrumentedSources
    {
        [Description("source_type:http.request.body")] RequestBody,
        [Description("source_type:http.request.path")] RequestPath,
        [Description("source_type:http.request.parameter.name")] RequestParameterName,
        [Description("source_type:http.request.parameter")] RequestParameterValue,
        [Description("source_type:http.request.path.parameter")] RoutedParameterValue,
        [Description("source_type:http.request.header")] RequestHeader,
        [Description("source_type:http.request.header.name")] RequestHeaderName,
        [Description("source_type:http.request.query")] RequestQuery,
        [Description("source_type:http.http.cookie.name")] CookieName,
        [Description("source_type:http.cookie.value")] CookieValue,
        [Description("source_type:http.http.request.matrix.parameter")] MatrixParameter,
    }

    public enum IastInstrumentedSinks
    {
        [Description("vulnerability_type:weak_cipher")] WeakCipher = 1,
        [Description("vulnerability_type:weak_hash")] WeakHash = 2,
        [Description("vulnerability_type:sql_injection")] SqlInjection = 3,
        [Description("vulnerability_type:command_injection")] CommandInjection = 4,
        [Description("vulnerability_type:path_traversal")] PathTraversal = 5,
        [Description("vulnerability_type:ldap_injection")] LdapInjection = 6,
        [Description("vulnerability_type:ssrf")] Ssrf = 7,
        [Description("vulnerability_type:unvalidated_redirect")] UnvalidatedRedirect = 8,
        [Description("vulnerability_type:insecure_cookie")] InsecureCookie = 9,
        [Description("vulnerability_type:no_httponly_cookie")] NoHttpOnlyCookie = 10,
        [Description("vulnerability_type:no_samesite_cookie")] NoSameSiteCookie = 11,
    }
}
