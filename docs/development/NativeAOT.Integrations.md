# NativeAOT support by integration

What each integration supports in an application published with NativeAOT and the `Datadog.Trace.Aot` package, why, and
what was verified. The per-sample results are in [`NativeAOT.Samples.md`](./NativeAOT.Samples.md) and the findings (H-n) in
the [tracker](./NativeAOT.Tracker.md).

Sources (2026-10-14):

- The e2e samples: each sample published self-contained with the profiler (JIT) and with NativeAOT, against the same mock
  agent; spans, tags, IAST vulnerabilities, direct log submission and DSM checkpoints compared.
- A NativeAOT publish of the 94 applicable samples (net8.0, ILC 8) with the instrumentation in `--verbose` and
  `TrimmerSingleWarn=false`: its reports (`report.json`, the compatibility of the duck typing registry) and the ILC warnings
  of Datadog.Trace in the code each application reaches.
- ILC with the whole Datadog.Trace as a root, for the dynamic code of the integrations no sample reaches.
- The libraries: whether their latest NuGet version declares trimming/AOT compatibility (`IsTrimmable`, which
  `IsAotCompatible` sets) and the `plain` runs of the e2e (the application in NativeAOT without Datadog).

## When something fails

| What fails | At build time | At runtime | How it shows |
|---|---|---|---|
| Binding an integration method (a missing member, the duck typing of a constraint) | The CallTarget registration records the failure | The first call throws `CallTargetInvokerException`, which disables the integration for that type | A warning in the tracer log and the `IntegrationDisabledDueToError` telemetry; the application keeps running |
| A duck typing mapping that isn't declared nor comes from a constraint | — (`CheckAotDuckTypeDeclarations` fails in CI for the mappings the integration tests record) | `DuckTypeAotMissingProxyRegistrationException` where the proxy is created | The integration's own error handling (usually a warning in the log) |
| A duck typing mapping that also fails with dynamic duck typing | The registry replays the failure | The same exception as under JIT | Same as under JIT |
| A method ILC can't compile | ILC prints `Method '...' will always throw` | The method throws | The package tests fail when it's a method of Datadog.Trace |
| A library that doesn't support NativeAOT | Usually trimming/AOT warnings of the library | The library fails, also without Datadog | The application's own errors |

## Integrations

Status: **Supported** (same spans and tags as under JIT); **Supported, library partial** (what the library runs in NativeAOT
has parity, the rest fails also without Datadog); **Not supported (library)** (the library doesn't run in NativeAOT);
**Not verified** (no NativeAOT run yet); **Not applicable**; **Deferred** (decided not to support yet). "AOT-annotated"
is whether the library's latest version declares trimming/AOT compatibility; a library that doesn't can still work.

| Integration | Library (versions run) | Status | AOT-annotated | Evidence and limits |
|---|---|---|---|---|
| AdoNet | ADO.NET providers | Supported | — | FakeDbCommand 112/112 and the providers below |
| SqlClient | System.Data.SqlClient 4.9.1, Microsoft.Data.SqlClient 7.1.0 | Supported | No (7.x partly) | Parity on the typed phase of the samples |
| Npgsql | 10.0.3 | Supported | Yes | Parity |
| MySql | MySql.Data, MySqlConnector 2.6.2 | Supported | No / Yes | Parity (MySql.Data needs a reflection descriptor of its own) |
| Oracle | Oracle.ManagedDataAccess.Core | Supported | No | Parity against Oracle Free; `Oracle.DataAccess` (unmanaged) is .NET Framework only |
| Sqlite | Microsoft.Data.Sqlite 10, System.Data.SQLite.Core | Supported | No (partly) | 126/126 both |
| Aerospike | 8.5.0 | Supported | No | 19/19 |
| AspNetCore | ASP.NET Core (Kestrel, minimal APIs with the request delegate generator) | Supported | Yes | Spans, AppSec (WAF, blocking, remote activation), RASP, IAST and Code Origin. MVC and Razor Pages don't run in NativeAOT (the framework), so the integration's MVC parts (actions, model binding for AppSec and IAST) don't apply |
| AwsSdk, AwsS3, AwsSqs, AwsSns, AwsDynamoDb, AwsEventBridge, AwsKinesis, AwsStepFunctions | AWSSDK v4 | Supported | Yes | Parity, also DSM. AWSSDK v3 creates its services by reflection (the library). Kinesis `GetRecords` needs `StreamARN`, also under JIT |
| AwsLambda | Amazon.Lambda.RuntimeSupport 2.x | Supported | Yes | Serverless, manual and HTTP spans per invocation |
| AzureEventHubs | Azure.Messaging.EventHubs 5.12.2 | Supported | Yes | The four modes of its test |
| AzureServiceBus | Azure.Messaging.ServiceBus 7.17.5 | Supported | Yes | Five scenarios of its test |
| AzureFunctions, ServerlessCompat | Microsoft.Azure.Functions.Worker 2.x, Datadog.Serverless.Compat | Not verified | No | Needs the Functions host; NativeAOT in the isolated worker isn't officially documented |
| CosmosDb | Microsoft.Azure.Cosmos 3.56 | Supported, library partial | No | CRUD with parity; queries fail in the SDK (query plan with Newtonsoft and `MakeGenericType` of value types) |
| Couchbase | CouchbaseNetClient 2.7.27 / 3.x | Supported (2.x) / Not supported (library, 3.x) | No | 3.x creates `Logger<T>` with `MakeGenericType` in its DI |
| DatadogTraceManual | Manual API, OpenTracing | Supported | — | Manual instrumentation 47/47, OpenTracing, manual DSM |
| DatadogTraceVersionConflict | Datadog.Trace 1.x/2.x from NuGet | Not applicable | — | By design: the publish asks for the manual API of the current version |
| ElasticsearchNet | NEST 5/6/7, Elasticsearch.Net | Not supported (library) | No | Converters and responses by reflection; 7.x emits IL |
| GraphQL | GraphQL.NET 2.x, 3.x, 7.x / 4.x-5.x | Supported, library partial / Not supported (library, 4.x-5.x) | Partly | Same spans; some resolvers of the library fail by reflection; 4.x-5.x build their types with `MakeGenericType` |
| Grpc | Grpc.Net 2.67, Grpc.Core 2.45 | Supported | Yes / No | 80/80 and parity |
| Hangfire | 1.7.0 | Supported | No | With the context propagation through the job parameter |
| HotChocolate | 13-15 | Not supported (library) | No | Its DI doesn't find constructors |
| HttpMessageHandler (sockets, curl, WinHttp) | System.Net.Http | Supported | Yes | 222/222 on net8.0 to net11.0 |
| WebRequest | System.Net.Requests | Supported | Yes | 134/134 |
| Process | System.Diagnostics.Process | Supported | Yes | 10/10 with command collection |
| IbmMq | IBMMQDotnetClient (amqmdnetstd) | Not verified | No | No sample |
| ILogger | Microsoft.Extensions.Logging | Supported | Yes | Logs injection and direct submission. Microsoft.Extensions.Telemetry (`ExtendedLogger`) doesn't run in NativeAOT (DI by reflection, the library) |
| Serilog | 4.x | Supported | Yes | Logs injection and direct submission |
| NLog | 6.x | Supported | Partly | Logs injection and direct submission with NLog 6. Not verified in NativeAOT: the NLog 4 contexts (MDC, MDLC), JSON layouts and a run without configuration file (they keep their ILC warnings) |
| Log4Net | log4net | Not supported (library) | Partly | Doesn't create its appenders in NativeAOT |
| Kafka | Confluent.Kafka 2.15.1 | Supported, library partial | No | Producer with parity (with and without delivery handler, value-type keys, cluster id against a broker, DSM); the library needs a reflection descriptor for librdkafka, and its consumers don't receive in NativeAOT, also without Datadog |
| MongoDb | MongoDB.Driver 3.12 | Supported, library partial | No | The first commands with parity; the driver fails in its serializers (`MakeGenericType` of value types) |
| OpenFeature | 2.3.0 | Supported | Yes | Remote Configuration `FFE_FLAGS` and exposures |
| OpenTelemetry | OpenTelemetry SDK 1.18 / OpenTelemetry.Api 1.3.1 | Supported / Not supported (library, 1.3.1) | Yes | With `DD_TRACE_OTEL_ENABLED` and the resource; OTLP export of traces (JSON and protobuf, also on .NET 8 since H-73) |
| Protobuf | Google.Protobuf | Supported | Yes | DSM |
| RabbitMQ | RabbitMQ.Client 7.2.2 | Supported | Yes | 108/108 and DSM |
| Quartz (activities) | Quartz 3.22 | Supported | Yes | Through the activity listener |
| ServiceStackRedis | ServiceStack.Redis | Supported | No | Parity on the typed phase |
| StackExchangeRedis | 2.1 to 3.3 | Supported | No | Also two versions in the same application |
| IAST and RASP call sites, HashAlgorithm, SymmetricAlgorithm | BCL | Supported | — | Weak cipher, deduplication, LFI/path traversal; the call sites are instrumented at build time |
| Ssrf, Xss, StackTraceLeak, NHibernate (IAST) | RestSharp, ASP.NET Core, NHibernate | Not verified | No (RestSharp) | No specific sample |
| TraceAnnotations | `[Trace]`, `DD_TRACE_METHODS` | Supported | — | 68/68 |
| ServiceRemoting | Service Fabric | Not supported (library) | No | Service Fabric Remoting emits its proxies with Reflection.Emit; NativeAOT applications don't start it |
| Ocelot, YARP, MassTransit, Rebus, Dapper, EF Core (no integration of their own) | — | Not supported (library) | No, except YARP | DI and middlewares by reflection, `Reflection.Emit` |
| AspNet, AspNetMvc, AspNetWebApi2, Wcf, Msmq, Remoting | .NET Framework | Not applicable | — | NativeAOT is .NET 8+ |
| XUnit, NUnit, MsTestV2, DotnetTest, Selenium, TestPlatformAssemblyResolver | Test frameworks | Deferred (CI Visibility) | — | The NativeAOT mode of xUnit v3 runs the tests with other assemblies (`xunit.v3.core.aot`); MSTest's is alpha |
| Dynamic Instrumentation, Exception Replay, Symbol Database | — | Deferred | — | They rewrite methods or read the assemblies' files at runtime: not compiled in NativeAOT applications |
| Continuous Profiler | — | Not applicable | — | Needs the CLR profiling API |

## Findings in the tracer

| Finding | Status |
|---|---|
| H-72: the instrumentation logged `[ERR] ... Expected a null constant` (GraphQL, Rebus): dnlib reads the local PDB constants of an enum type it can't resolve as class constants, which its writer rejects | Fixed: they're written as enum constants |
| H-73: ILC 8 can't compile `EnumerateTags<TagWriter<__Canon>>`: the OTLP export of traces in JSON threw `InvalidProgramException` on .NET 8 | Fixed (the state is a struct); the package tests fail on any `will always throw` of Datadog.Trace |
| H-74: `will always throw` of the ASP.NET Core observers in the samples without ASP.NET Core (ILC 8) | Test-only: the samples' helper looks up Datadog.Trace types by name, which keeps them; applications don't |
| H-75: ILC warnings of Datadog.Trace in real applications (49 in the samples) | Explained where verified (see below); the unverified paths keep theirs |
| H-76: the search of generic instantiations hits its limit in 27 samples | No deferred shape left unresolved; to review |
| H-77: the Kafka client handle proxy was only declared for `Producer<Null, Null>`: no cluster id with other types | Fixed (open generic `Producer<,>` and `Consumer<,>`) |

## ILC warnings left

The paths that keep their ILC warning because NativeAOT didn't run them:

| Warning | Code | Why it's not verified |
|---|---|---|
| IL2026 | NLog `DiagnosticContextHelper.GetMdcProxy`, `GetMdlcProxy` | Logs injection with NLog 4 (MDC, MDLC); NLog 5+ uses `ScopeContext` |
| IL2077 | NLog `LogsInjectionHelper.AddAttributeToJson4Layout` | Logs injection into a JSON layout |
| IL2091 | NLog `LoggingConfigurationFileLoaderLoadInstrumentation.OnMethodEnd` | An NLog 6 run without configuration file |
| IL3050 | Log4Net `Log4NetCommon.TryAddAppenderToResponse` | log4net doesn't run in NativeAOT |
| IL2026, IL2067, IL2070, IL3050 | AppSec `ObjectExtractor` | Only reached by MVC model binding, which doesn't run in NativeAOT |
