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

    internal enum DatadogConfiguration
    {
        [Description("config_datadog:dd_trace_debug")] DebugEnabled,
        [Description("config_datadog:dd_runtime_metrics_enabled")] RuntimeMetricsEnabled,
        [Description("config_datadog:dd_service")] Service,
        [Description("config_datadog:dd_tags")] Tags,
        [Description("config_datadog:dd_trace_enabled")] TraceEnabled,
        [Description("config_datadog:dd_trace_propagation_style")] PropagationStyle,
        [Description("config_datadog:dd_trace_sample_rate")] SampleRate,
        [Description("config_datadog:dd_trace_otel_enabled")] OpenTelemetryEnabled,
        [Description("config_datadog:unknown")] Unknown,
    }

    internal enum OpenTelemetryConfiguration
    {
        [Description("config_opentelemetry:otel_log_level")] LogLevel,
        [Description("config_opentelemetry:otel_metrics_exporter")] MetricsExporter,
        [Description("config_opentelemetry:otel_propagators")] Propagators,
        [Description("config_opentelemetry:otel_resource_attributes")] ResourceAttributes,
        [Description("config_opentelemetry:otel_sdk_disabled")] SdkDisabled,
        [Description("config_opentelemetry:otel_service_name")] ServiceName,
        [Description("config_opentelemetry:otel_traces_exporter")] TracesExporter,
        [Description("config_opentelemetry:otel_traces_sampler")] TracesSampler,
        [Description("config_opentelemetry:otel_traces_sampler_arg")] TracesSamplerArg,
        [Description("config_opentelemetry:unknown")] Unknown,
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
        [Description("integration_name:awseventbridge")]AwsEventBridge,
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
        [Description("integration_name:ssrf")]Ssrf,
        [Description("integration_name:ldap")]Ldap,
        [Description("integration_name:hardcodedsecret")]HardcodedSecret,
        [Description("integration_name:awskinesis")]AwsKinesis,
        [Description("integration_name:azureservicebus")]AzureServiceBus,
        [Description("integration_name:systemrandom")] SystemRandom,
        [Description("integration_name:awsdynamodb")]AwsDynamoDb,
        [Description("integration_name:ibmmq")]IbmMq,
        [Description("integration_name:remoting")]Remoting,
        [Description("integration_name:trustboundaryviolation")] TrustBoundaryViolation,
        [Description("integration_name:unvalidatedredirect")] UnvalidatedRedirect,
        [Description("integration_name:testplatformassemblyresolver")] TestPlatformAssemblyResolver,
        [Description("integration_name:stacktraceleak")] StackTraceLeak,
        [Description("integration_name:xpathinjection")] XpathInjection,
        [Description("integration_name:reflectioninjection")] ReflectionInjection,
        [Description("integration_name:xss")] Xss,
        [Description("integration_name:nhibernate")] NHibernate,
        [Description("integration_name:dotnettest")] DotnetTest,
        [Description("integration_name:selenium")] Selenium,
        [Description("integration_name:directorylistingleak")] DirectoryListingLeak,
        [Description("integration_name:sessiontimeout")] SessionTimeout,
        [Description("integration_name:datadogtracemanual")] DatadogTraceManual,
        [Description("integration_name:emailhtmlinjection")] EmailHtmlInjection
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

    [EnumExtensions]
    public enum RaspRuleType
    {
        [Description("waf_version;rule_type:lfi")] Lfi = 0,
        [Description("waf_version;rule_type:ssrf")] Ssrf = 1,
        [Description("waf_version;rule_type:sql_injection")] SQlI = 2,
        [Description("waf_version;rule_type:command_injection")] CommandInjection = 3,
    }

    public enum TruncationReason
    {
        [Description("truncation_reason:string_too_long")]StringTooLong = 1,
        [Description("truncation_reason:list_or_map_too_large")]ListOrMapTooLarge = 2,
        [Description("truncation_reason:object_too_deep")]ObjectTooDeep = 4,
    }

    [EnumExtensions]
    public enum IastInstrumentedSources
    {
        [Description("source_type:http.request.body")] RequestBody = 0,
        [Description("source_type:http.request.path")] RequestPath = 1,
        [Description("source_type:http.request.parameter.name")] RequestParameterName = 2,
        [Description("source_type:http.request.parameter")] RequestParameterValue = 3,
        [Description("source_type:http.request.path.parameter")] RoutedParameterValue = 4,
        [Description("source_type:http.request.header")] RequestHeaderValue = 5,
        [Description("source_type:http.request.header.name")] RequestHeaderName = 6,
        [Description("source_type:http.request.query")] RequestQuery = 7,
        [Description("source_type:http.request.cookie.name")] CookieName = 8,
        [Description("source_type:http.request.cookie.value")] CookieValue = 9,
        [Description("source_type:http.request.matrix.parameter")] MatrixParameter = 10,
        [Description("source_type:http.request.uri")] RequestUri = 11,
        [Description("source_type:grpc.request.body")] GrpcRequestBody = 12,
        [Description("source_type:sql.row.value")] SqlRowValue = 13,
    }

    [EnumExtensions]
    public enum IastInstrumentedSinks
    {
        [Description("vulnerability_type:none")] None = 0,
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
        [Description("vulnerability_type:weak_randomness")] WeakRandomness = 12,
        [Description("vulnerability_type:hardcoded_secret")] HardcodedSecret = 13,
        [Description("vulnerability_type:xcontenttype_header_missing")] XContentTypeHeaderMissing = 14,
        [Description("vulnerability_type:trust_boundary_violation")] TrustBoundaryViolation = 15,
        [Description("vulnerability_type:hsts_header_missing")] HstsHeaderMissing = 16,
        [Description("vulnerability_type:header_injection")] HeaderInjection = 17,
        [Description("vulnerability_type:stacktrace_leak")] StackTraceLeak = 18,
        [Description("vulnerability_type:nosql_mongodb_injection")] NoSqlMongoDbInjection = 19,
        [Description("vulnerability_type:xpath_injection")] XPathInjection = 20,
        [Description("vulnerability_type:reflection_injection")] ReflectionInjection = 21,
        [Description("vulnerability_type:insecure_auth_protocol")] InsecureAuthProtocol = 22,
        [Description("vulnerability_type:xss")] Xss = 23,
        [Description("vulnerability_type:directory_listing_leak")] DirectoryListingLeak = 24,
        [Description("vulnerability_type:session_timeout")] SessionTimeout = 25,
        [Description("vulnerability_type:email_html_injection")] EmailHtmlInjection = 26,
    }

    public enum AuthenticationFramework
    {
        [Description("framework:aspnetcore_identity")] AspNetCoreIdentity,
        [Description("framework:unknown")] Unknown,
    }

    public enum CIVisibilityTestFramework
    {
        [Description("test_framework:xunit")] XUnit,
        [Description("test_framework:nunit")] NUnit,
        [Description("test_framework:mstest")] MSTest,
        [Description("test_framework:benchmarkdotnet")] BenchmarkDotNet,
        [Description("test_framework:unknown")] Unknown,
    }

    public enum CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmark
    {
        [Description("event_type:test")] Test,
        [Description("event_type:test;is_benchmark")] Test_IsBenchmark,
        [Description("event_type:suite")] Suite,
        [Description("event_type:module")] Module,
        [Description("event_type:session")] Session_NoCodeOwner_IsSupportedCi,
        [Description("event_type:session;is_unsupported_ci")] Session_NoCodeOwner_UnsupportedCi,
        [Description("event_type:session;has_codeowner;is_unsupported_ci")] Session_HasCodeOwner_UnsupportedCi,
        [Description("event_type:session;has_codeowner")] Session_HasCodeOwner_IsSupportedCi,
    }

    public enum CIVisibilityTestingEventTypeWithCodeOwnerAndSupportedCiAndBenchmarkAndEarlyFlakeDetectionAndRum
    {
        [Description("event_type:test")] Test,
        [Description("event_type:test;is_benchmark")] Test_IsBenchmark,
        [Description("event_type:suite")] Suite,
        [Description("event_type:module")] Module,
        [Description("event_type:session")] Session_NoCodeOwner_IsSupportedCi,
        [Description("event_type:session;is_unsupported_ci")] Session_NoCodeOwner_UnsupportedCi,
        [Description("event_type:session;has_codeowner;is_unsupported_ci")] Session_HasCodeOwner_UnsupportedCi,
        [Description("event_type:session;has_codeowner")] Session_HasCodeOwner_IsSupportedCi,
        [Description("event_type:test;is_new:true")] Test_EFDTestIsNew,
        [Description("event_type:test;is_new:true;early_flake_detection_abort_reason:slow")] Test_EFDTestIsNew_EFDTestAbortSlow,
        [Description("event_type:test;browser_driver:selenium")] Test_BrowserDriverSelenium,
        [Description("event_type:test;is_new:true;browser_driver:selenium")] Test_EFDTestIsNew_BrowserDriverSelenium,
        [Description("event_type:test;is_new:true;early_flake_detection_abort_reason:slow;browser_driver:selenium")] Test_EFDTestIsNew_EFDTestAbortSlow_BrowserDriverSelenium,
        [Description("event_type:test;browser_driver:selenium;is_rum:true")] Test_BrowserDriverSelenium_IsRum,
        [Description("event_type:test;is_new:true;browser_driver:selenium;is_rum:true")] Test_EFDTestIsNew_BrowserDriverSelenium_IsRum,
        [Description("event_type:test;is_new:true;early_flake_detection_abort_reason:slow;browser_driver:selenium;is_rum:true")] Test_EFDTestIsNew_EFDTestAbortSlow_BrowserDriverSelenium_IsRum,
    }

    public enum CIVisibilityCoverageLibrary
    {
        [Description("library:custom")] Custom,
        [Description("library:unknown")] Unknown,
    }

    public enum CIVisibilityTestingEventType
    {
        [Description("event_type:test")] Test,
        [Description("event_type:suite")] Suite,
        [Description("event_type:module")] Module,
        [Description("event_type:session")] Session,
    }

    public enum CIVisibilityEndpoints
    {
        [Description("endpoint:test_cycle")] TestCycle,
        [Description("endpoint:code_coverage")] CodeCoverage,
    }

    public enum CIVisibilityEndpointAndCompression
    {
        [Description("endpoint:test_cycle")] TestCycleUncompressed,
        [Description("endpoint:test_cycle;rq_compressed:true")] TestCycleRequestCompressed,
        [Description("endpoint:code_coverage")] CodeCoverageUncompressed,
        [Description("endpoint:code_coverage;rq_compressed:true")] CodeCoverageRequestCompressed,
    }

    public enum CIVisibilityErrorType
    {
        [Description("error_type:timeout")] Timeout,
        [Description("error_type:network")] Network,
        [Description("error_type:status_code")] StatusCode,
        [Description("error_type:status_code_4xx_response")] StatusCode4xx,
        [Description("error_type:status_code_5xx_response")] StatusCode5xx,
        [Description("error_type:status_code_4xx_response;status_code:400")] StatusCode400,
        [Description("error_type:status_code_4xx_response;status_code:401")] StatusCode401,
        [Description("error_type:status_code_4xx_response;status_code:403")] StatusCode403,
        [Description("error_type:status_code_4xx_response;status_code:404")] StatusCode404,
        [Description("error_type:status_code_4xx_response;status_code:408")] StatusCode408,
        [Description("error_type:status_code_4xx_response;status_code:429")] StatusCode429,
    }

    public enum CIVisibilityCommands
    {
        [Description("command:get_repository")] GetRepository,
        [Description("command:get_branch")] GetBranch,
        [Description("command:get_remote")] GetRemote,
        [Description("command:get_head")] GetHead,
        [Description("command:check_shallow")] CheckShallow,
        [Description("command:unshallow")] Unshallow,
        [Description("command:get_local_commits")] GetLocalCommits,
        [Description("command:get_objects")] GetObjects,
        [Description("command:pack_objects")] PackObjects,
    }

    public enum CIVisibilityExitCodes
    {
        [Description("exit_code:missing")] Missing,
        [Description("exit_code:unknown")] Unknown,
        [Description("exit_code:-1")] ECMinus1,
        [Description("exit_code:1")] EC1,
        [Description("exit_code:2")] EC2,
        [Description("exit_code:127")] EC127,
        [Description("exit_code:128")] EC128,
        [Description("exit_code:129")] EC129,
    }

    public enum CIVisibilityITRSettingsResponse
    {
        [Description("")] CoverageDisabled_ItrSkipDisabled,
        [Description("coverage_enabled")] CoverageEnabled_ItrSkipDisabled,
        [Description("itrskip_enabled")] CoverageDisabled_ItrSkipEnabled,
        [Description("coverage_enabled;itrskip_enabled")] CoverageEnabled_ItrSkipEnabled,
        [Description("early_flake_detection_enabled:true")] CoverageDisabled_ItrSkipDisabled_EFDEnabled,
        [Description("coverage_enabled;early_flake_detection_enabled:true")] CoverageEnabled_ItrSkipDisabled_EFDEnabled,
        [Description("itrskip_enabled;early_flake_detection_enabled:true")] CoverageDisabled_ItrSkipEnabled_EFDEnabled,
        [Description("coverage_enabled;itrskip_enabled;early_flake_detection_enabled:true")] CoverageEnabled_ItrSkipEnabled_EFDEnabled,
    }

    public enum CIVisibilityRequestCompressed
    {
        [Description("")] Uncompressed,
        [Description("rq_compressed:true")] Compressed,
    }

    public enum CIVisibilityResponseCompressed
    {
        [Description("")] Uncompressed,
        [Description("rs_compressed:true")] Compressed,
    }
}
