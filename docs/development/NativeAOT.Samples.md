# Inventario de samples: JIT frente a NativeAOT

Todos los proyectos de samples del repositorio (`tracer/test/test-applications`, `tracer/samples`, `tracer/tools`,
`profiler/src/Demos`, `shared/samples`), con su resultado de paridad entre JIT (con el profiler) y NativeAOT (con
`Datadog.Trace.Aot`). Complementa el e2e de samples del [tracker](NativeAOT.Tracker.md), donde están los hallazgos (H-n) que
se citan aquí.

**Cómo se mide.** El harness `.nativeaot-scratch/e2e-samples` (sin commit) publica cada sample para net8.0 dos veces:
self-contained con el profiler (oráculo JIT, que además graba los mapeos DuckType) y con `PublishAot` y los targets del
paquete. Los dos corren contra el mismo agente simulado y `compare.py` compara los spans agrupados (nombre, servicio, tipo,
recurso normalizado, componente, `span.kind`, error) y sus etiquetas, las vulnerabilidades IAST, los logs de envío directo
y los checkpoints DSM. Modos: driver HTTP (`WEB_REQUESTS`), Lambda simulada, señales, Remote Configuration simulada
(`RCM_RESPONSE`), DogStatsD (`STATSD_METRICS`) y `plain` (NativeAOT sin Datadog, para separar los límites de la librería de
los del tracer). Donde un sample necesita algo solo para test (descriptores de reflexión del propio sample o de su
librería), se indica.

**Estados.**

- **Paridad**: mismos spans, etiquetas y señales que bajo JIT.
- **Parcial**: lo que la aplicación consigue ejecutar en NativeAOT tiene paridad; el resto no se ejecuta por la librería o
  por el propio sample (se indica qué), no por el tracer.
- **Incompatible**: la librería o el sample no funcionan en NativeAOT, también sin Datadog (`plain`), o usan algo que
  NativeAOT no tiene (MVC, `Reflection.Emit`, carga de ensamblados).
- **Por diseño**: la aplicación referencia el paquete `Datadog.Trace` de otra versión mayor (1.x/2.x, el tracer completo de
  entonces): una aplicación NativeAOT solo puede contener un `Datadog.Trace`, así que el publish falla con un error que
  pide la API manual de la versión actual.
- **No aplica**: no puede ser una aplicación NativeAOT (.NET Framework, IIS, WPF, host externo), es un proyecto de tests de
  un runner (CI Visibility), un producto que necesita reescribir métodos en runtime (Dynamic Instrumentation, Exception
  Replay, Continuous Profiler) o un escenario propio del loader nativo.
- **Biblioteca**: proyecto auxiliar de otro sample (se prueba a través de él).
- **Pendiente**: aplica y aún no tiene resultado.

## Resumen

Inventario cerrado el 2026-10-09 sobre los 224 proyectos (unos 20 son bibliotecas auxiliares, que se prueban con sus
samples; la tabla cuenta las aplicaciones de cada fila de las secciones siguientes). Cada sample aplicable se ejecutó con
la configuración de su integration test, incluido `DD_TRACE_OTEL_ENABLED=true`, que `TracingIntegrationTest` pone en 68
de ellos (H-62).

| Estado | Aplicaciones |
|---|---|
| Paridad | 56 |
| Parcial | 14 |
| Incompatible (la librería, el sample o el framework; igual sin Datadog) | 23 |
| Por diseño (`Datadog.Trace` 1.x/2.x por NuGet) | 11 |
| No evaluable (el sample falla también bajo JIT o no publica) | 3 |
| No aplica | 84 |

- Ninguna diferencia atribuible al tracer queda abierta. Las que salieron en el inventario están corregidas: Hangfire con
  OTel tumbaba la aplicación (H-61), y con la configuración de los tests Quartz pasa a paridad (H-62).
- En los parciales, lo que la aplicación consigue ejecutar en NativeAOT tiene paridad. El resto lo impide la librería o
  el propio sample: fases con `Assembly.LoadFile` de los samples de ADO.NET y de ServiceStack.Redis, los consumidores de
  Confluent.Kafka, los serializadores de MongoDB, el plan de consulta de Cosmos, los resolvers de GraphQL.NET y la
  serialización por reflexión del sample de Lambda.
- Los incompatibles fallan igual sin Datadog (`plain`): MVC/Razor, `Reflection.Emit` (Dapper, NEST 7), DI por reflexión
  (HotChocolate, YARP, Ocelot, MassTransit, Rebus, Couchbase 3), `MakeGenericType` sobre tipos valor (GraphQL 4,
  OpenTelemetry.Api 1.3.1), log4net, carga de ensamblados y APIs que NativeAOT no tiene.
- Los "por diseño" fallan al publicar con un error que pide la API manual de la versión del tracer: una aplicación
  NativeAOT solo puede contener un `Datadog.Trace`.
- Además, sin sample propio, tienen paridad: Remote Configuration (feature flags, `APM_TRACING`, activación y bloqueo de
  AppSec), runtime metrics, Code Origin for spans, `DD_TRACE_METHODS`, el esquema de atributos v1 y el transporte por Unix
  domain socket.
- Siguen abiertos dos hallazgos que no cambian la paridad: H-58 (Code Origin apunta al código generado por RDG, igual que
  bajo JIT) y H-60 (contexto de hilo OTel, opt-in, pendiente del empaquetado nativo).

## tracer/test/test-applications/integrations

Versión probada entre paréntesis (la de los integration tests en net8.0 o la última del sample).

| Sample | Estado | Spans JIT / AOT | Evidencia y notas |
|---|---|---|---|
| LogsInjection.ILogger | Paridad | 5 / 5 | Spans y logs de envío directo iguales (solo cambia cuántas veces se repite un mensaje de espera del sample, que depende del arranque) |
| LogsInjection.ILogger.ExtendedLogger | Incompatible | 5 / 0 | Microsoft.Extensions.Telemetry 8: la DI no encuentra el constructor de `ProcessLogEnricher`; igual en `plain` |
| LogsInjection.ILogger.VersionConflict.2x | Por diseño | 5 / — | Referencia `Datadog.Trace` 2.x por NuGet |
| LogsInjection.Log4Net | Incompatible | 1 / 0 | log4net no crea sus appenders en NativeAOT (`PlatformNotSupportedException` en la configuración por reflexión); igual en `plain` |
| LogsInjection.Log4Net.VersionConflict.2x | Por diseño | 9 / — | `Datadog.Trace` 2.x por NuGet |
| LogsInjection.NLog | Paridad | 1 / 1 | Span y 3 logs de envío directo; también solo con el catálogo (sin el mapa grabado). Parche de test: el helper usa `Assembly.Location` |
| LogsInjection.NLog.VersionConflict.2x | Por diseño | 9 / — | `Datadog.Trace` 2.x por NuGet |
| LogsInjection.NLog10.VersionConflict.2x, LogsInjection.NLog20.VersionConflict.2x | No aplica | — | Solo net48 |
| LogsInjection.Serilog | Paridad | 1 / 1 | Span y logs; parche de test de `Assembly.Location` y descriptor de Serilog.Expressions |
| LogsInjection.Serilog.VersionConflict.2x | Por diseño | 9 / — | `Datadog.Trace` 2.x por NuGet |
| LogsInjection.Serilog14.VersionConflict.2x | No aplica | — | Solo net48 |
| Samples.AWS.DynamoDBv2 (AWSSDK v4) | Paridad | 17 / 17 | localstack; valida H-54 (PDB de Windows de AWSSDK) **Con la configuración de su test (OTel): 17 / 17.** |
| Samples.AWS.EventBridge (v4) | Paridad | 4 / 4 | También el checkpoint DSM **Con la configuración de su test (OTel): 4 / 4 y el checkpoint DSM.** |
| Samples.AWS.Kinesis (v4) | Paridad | 7 / 7 | También el checkpoint DSM **Con la configuración de su test (OTel, DSM): 7 / 7 y el checkpoint DSM.** |
| Samples.AWS.S3 (v4) | Paridad | 16 / 16 | **Con la configuración de su test (OTel): 16 / 16.** |
| Samples.AWS.SQS (v4) | Paridad | 28 / 28 | 6 checkpoints DSM; fallback de `CachedMessageHeadersHelper` (H-41) **Con la configuración de su test (OTel, DSM): 28 / 28 y 6 checkpoints DSM.** |
| Samples.AWS.SimpleNotificationService (v4) | Paridad | 5 / 5 | Checkpoint DSM; fallback de H-41 **Con la configuración de su test (OTel, DSM): 5 / 5 y el checkpoint DSM.** |
| Samples.AWS.StepFunctions (v4) | Paridad | 4 / 4 | Con el SDK 3.3 por defecto de los samples, AWSSDK.Core crea sus servicios por reflexión: se prueba con v4 **Con la configuración de su test (OTel): 4 / 4.** |
| Samples.AWS.Lambda | No aplica | — | Handlers para el runtime gestionado de Lambda (contenedor con el emulador); una Lambda NativeAOT es un ejecutable con `Amazon.Lambda.RuntimeSupport` (fila siguiente) |
| Samples.Amazon.Lambda.RuntimeSupport | Parcial | 9 / 9 | Runtime API y extensión simulados: span serverless, manual y HTTP por invocación. El span serverless lleva error en AOT porque el sample serializa un tipo anónimo con System.Text.Json por reflexión |
| Samples.Aerospike (8.5.0) | Paridad | 19 / 19 | **Con la configuración de su test (OTel): 19 / 19.** |
| Samples.AspNetCoreMinimalApis | Incompatible | 0 / 0 | Registra MVC (`AddControllersWithViews`): en NativeAOT, `ApplicationPartManager` aborta al arrancar (`ConsolidatedAssemblyApplicationPartFactory`) |
| Samples.AspNetCoreMvc21, Samples.AspNetCoreMvc30 | No aplica | — | netcoreapp2.1/net48 y netcoreapp3.0 |
| Samples.AspNetCoreMvc31 | Incompatible | — | MVC: `ApplicationPartManager` aborta al arrancar (`ConsolidatedAssemblyApplicationPartFactory`); igual en `plain` |
| Samples.AspNetCoreRazorPages | Incompatible | — | Razor Pages (MVC): igual que Samples.AspNetCoreMvc31 |
| Samples.AzureEventHubs (5.12.2) | Paridad | 5 / 5 | Emulador de Event Hubs. **Con la configuración de su test (OTel), los cuatro modos: `TestEventHubsMessageBatch` con y sin enlaces de lote (5 / 5, 2 / 2), `TestEventHubsEnumerable` con 3 mensajes (2 / 2) y `TestEventHubsBufferedProducer` (5 / 5).** |
| Samples.AzureServiceBus.APM (7.17.5) | Paridad | 3 / 3, 2 / 2, 2 / 2, 2 / 2, 5 / 5 | Emulador de Service Bus, con la configuración de su test (OTel): `SendMessages`, `ReceiveMessages`, `ReceiveMessagesMultiple`, `ScheduleMessages` y `TestServiceBusMessageBatch` |
| Samples.AzureServiceBus | No aplica en local | — | Crea colas y topics con `ServiceBusAdministrationClient`, que el emulador no expone (su test pide un namespace real) |
| Samples.CIVisibilityIpc | No aplica | — | CI Visibility: el servidor IPC vive en el proceso del test |
| Samples.Console | Paridad | 1 / 1 | `traces 1`; además, con `wait`: configuración dinámica `APM_TRACING` por RCM y runtime metrics (ver más abajo) |
| Samples.CosmosDb.Vnext (3.56) | Parcial | CRUD 20 / 20; Query 35 / 23 | Emulador vnext (gateway). CRUD con paridad (**con OTel, 20 / 19: la petición de fondo del SDK a IMDS sigue pendiente al salir en AOT, que acaba antes, y su span se cierra después del último flush**). Query: la primera consulta con paridad; después el SDK falla al deserializar el plan de consulta con Newtonsoft (`MakeGenericType` de colecciones de tipos valor) y el sample para. Descriptores de test (System.Configuration, el SDK, Newtonsoft) |
| Samples.CosmosDb | No aplica | — | Emulador de Windows (`LinuxUnsupported`, `SkipInCI`); el SDK se prueba con CosmosDb.Vnext |
| Samples.Couchbase (CouchbaseNetClient 2.7.27) | Paridad | 23 / 18 | Con la configuración de su test (OTel): mismas operaciones y etiquetas; los 5 `GetClusterConfig` de más bajo JIT son sondeos periódicos (cada 2,5 s, la ejecución JIT dura 17 s y la AOT 0,3 s). Descriptor de test: CouchbaseNetClient 2.x crea sus conexiones y Newtonsoft los documentos del sample por reflexión |
| Samples.Couchbase3 (3.9.6) | Incompatible | 506 / 0 | CouchbaseNetClient crea `Logger<T>` con `MakeGenericType` en su DI; bajo JIT el sample tampoco conecta con el servidor 5.0.1 del compose (sale con 13) |
| Samples.Dapper | Incompatible | 20 / 2 | Dapper emite IL (`Reflection.Emit`); igual en `plain` |
| Samples.DataStreams.AzureServiceBus | No aplica en local | — | Igual que Samples.AzureServiceBus |
| Samples.DataStreams.HttpClient | Paridad | 3 / 3 | También los checkpoints DSM |
| Samples.DataStreams.Kafka (Confluent.Kafka 1.9.2) | Parcial | 48 / 6 | Producción con spans y checkpoints DSM; como en Samples.Kafka, los consumidores de Confluent.Kafka no reciben en NativeAOT y el sample espera hasta el timeout. Descriptor de test: librdkafka enlazada por reflexión |
| Samples.DataStreams.ManualAPI | Paridad | 2 / 2 | Checkpoints DSM |
| Samples.DataStreams.RabbitMQ | Paridad | 31 / 31 | 12 checkpoints DSM (colas `amq.gen-*` normalizadas) |
| Samples.Deduplication (IAST) | Paridad | 2 / 2 | También las vulnerabilidades (categoría `iast`) |
| Samples.Elasticsearch (NEST 6) | Incompatible | — | NEST crea sus converters de Json.NET por reflexión; con descriptores, el despacho a Elasticsearch.Net falla igual sin Datadog |
| Samples.Elasticsearch.V5 (NEST 5) | Incompatible | — | Como NEST 6: converters y respuestas creados por reflexión (`plain`) |
| Samples.Elasticsearch.V7 | Incompatible | 321 / 0 | El serializador de Elasticsearch.Net/NEST 7 usa `Reflection.Emit` |
| Samples.FakeDbCommand | Paridad | 112 / 112 | **Con la configuración de su test (OTel): 112 / 112.** |
| Samples.GoogleProtobuf (DSM) | Paridad | 2 / 2 | **Con la configuración de su test (OTel, DSM): 2 / 2.** |
| Samples.GraphQL (2.3.0) | Parcial | 18 / 18 | Mismos spans; las dos queries de `hero` acaban en error de GraphQL.NET (`NULL_REFERENCE` al resolver), también sin Datadog. Descriptores de test (GraphQL crea sus tipos por reflexión) **Con la configuración de su test (OTel, extensiones de error): igual (18 / 18).** |
| Samples.GraphQL3 (3.3.2) | Parcial | 18 / 18 | Como Samples.GraphQL; comprobado con `plain` **Con la configuración de su test (OTel, extensiones de error): igual (18 / 18).** |
| Samples.GraphQL4 (5.2.0) | Incompatible | 18 / 0 | GraphQL.NET construye sus tipos con `MakeGenericType`; igual en `plain` |
| Samples.GraphQL7 | Parcial | 14 / 14 | Mismos spans y respuestas; dos operaciones con error de ejecución de la librería (resolvers por reflexión) **Con la configuración de su test (OTel, extensiones de error): igual (14 / 14; la mutación y la query de `hero` con error de la librería).** |
| Samples.GrpcDotNet (2.67.0) | Paridad | 80 / 80 | Cliente y servidor gRPC, ASP.NET Core y HttpClient (H-49) **Con la configuración de su test (OTel): 80 / 80.** |
| Samples.GrpcLegacy (Grpc.Core 2.45) | Paridad | 27 / 28 | H-53; el span de servidor de más es `VerySlow`, que bajo JIT no termina antes de salir **Con la configuración de su test (OTel): 41 / 42, la misma diferencia de `VerySlow`.** |
| Samples.Hangfire (1.7.0) | Paridad | 3 / 3 | Con `DD_TRACE_OTEL_ENABLED`, incluida la propagación del contexto por el parámetro del job (los `hangfire.perform` son hijos del span que encola, H-61). Proxies inversos de los filtros; descriptor de test (Hangfire invoca los jobs por reflexión) |
| Samples.HotChocolate | Incompatible | 13 / 0 | Su DI no encuentra constructores (`DataLoaderRootFieldTypeInterceptor`); igual en `plain` |
| Samples.HttpMessageHandler | Paridad | 222 / 222 | **Con la configuración de su test (OTel): 222 / 222, también con el esquema v1.** |
| Samples.InstrumentedTests, Samples.MSTestTests, Samples.MSTestTests2, Samples.MSTestTestsRetries, Samples.NUnitGlobalCoverageMemory, Samples.NUnitTests, Samples.NUnitTestsRetries, Samples.Selenium, Samples.XUnitTests, Samples.XUnitTestsRetries | No aplica | — | CI Visibility: proyectos de tests que ejecuta un runner (`dotnet test`) |
| Samples.XUnitTestsV3, Samples.XUnitTestsRetriesV3 | No aplica | — | CI Visibility: aunque xUnit v3 genera un ejecutable, sus tests lo lanzan con `dotnet test` (`RunDotnetTestSampleAndWaitForExit`) |
| Samples.Kafka (Confluent.Kafka 2.15.1) | Parcial | 183 / 63 | Productor con paridad (62 spans, con y sin handler de entrega) y el span de error del consumidor; los consumidores de Confluent.Kafka no reciben mensajes en NativeAOT, tampoco sin Datadog |
| Samples.LargePayload | Paridad | 10 000 / 10 000 | |
| Samples.LifetimeManager.TerminationSignals | Paridad | — | Dos SIGTERM: salida 143 y una sola ejecución de las tareas de cierre (H-55) |
| Samples.ManualInstrumentation | Paridad | 47 / 47 | Incluido `_dd.git.*` (H-45) |
| Samples.Microsoft.Data.SqlClient (7.1.0) | Parcial | 181 / 112 | Fase tipada con paridad; la siguiente usa `Assembly.LoadFile` **Con la configuración de su test (OTel): igual (181 / 112).** |
| Samples.Microsoft.Data.Sqlite (10.0.12) | Paridad | 126 / 126 | **Con la configuración de su test (OTel): 126 / 126.** |
| Samples.MongoDB (3.12) | Parcial | 15 / 4 | Los 4 primeros comandos con paridad; luego el driver falla (`MakeGenericType` de serializadores sobre tipos valor). **Con la configuración de su test (OTel): el driver falla antes, en el constructor estático de sus serializadores LINQ (19 / 0), y la aplicación cae antes de enviar las trazas.** |
| Samples.Msmq | No aplica | — | Solo net48 |
| Samples.MySql (MySql.Data) | Parcial | 192 / 118 | Fase tipada con paridad (descriptor de test); después `Assembly.LoadFile` **Con la configuración de su test (OTel): igual (192 / 118).** |
| Samples.MySqlConnector (2.6.2) | Parcial | 181 / 112 | Fase tipada con paridad; después `Assembly.LoadFile` **Con la configuración de su test (OTel): igual (181 / 112).** |
| Samples.NetActivitySdk | Incompatible | — | `MakeGenericType` de OpenTelemetry.Api 1.3.1 en un constructor estático; igual en `plain` |
| Samples.NoMultiLoader | Paridad | 0 / 0 | Misma salida; el sample valida el loader nativo (no hay spans) |
| Samples.Npgsql (10.0.3) | Parcial | 181 / 112 | Fase tipada con paridad; después `Assembly.LoadFile` **Con la configuración de su test (OTel): igual (181 / 112).** |
| Samples.Ocelot.DistributedTracing | Incompatible | — | Ocelot no encuentra el `Invoke` de sus middlewares por reflexión; igual en `plain` |
| Samples.OpenFeature (2.3.0) | Paridad | 2 / 2 | Remote Configuration `FFE_FLAGS`: mismas evaluaciones, etiquetas `ffe_*` del span raíz y exposición por el proxy EVP |
| Samples.OpenTelemetry.HttpClient, Samples.OpenTelemetry.WebRequest | Paridad | 8 / 8, 8 / 8 | **Con la configuración de su test (OTel): 8 / 8, 8 / 8.** |
| Samples.OpenTelemetrySdk (1.18.0) | Paridad | 38 / 38 | Con `DD_TRACE_OTEL_ENABLED`: servicio y atributos del recurso incluidos (H-41); repetido con la versión actual |
| Samples.OpenTracing | Paridad | 1 / 1 | **Con la configuración de su test (OTel): 1 / 1.** |
| Samples.OracleMDA (Oracle.ManagedDataAccess.Core) | Paridad | 112 / 112 | Con la configuración de su test (OTel), contra Oracle Database Free (`gvenzl/oracle-free`, el mismo servicio `FREE` del compose): los 27 grupos de spans y sus etiquetas iguales (con los nombres de tabla aleatorios normalizados) |
| Samples.Owin.WebApi2 | No aplica | — | Solo net48 |
| Samples.ProcessStart | Paridad | 3 / 3 | **Con la configuración de su test (OTel, recogida de comandos): 10 / 10.** |
| Samples.Quartz (3.22.0) | Paridad | 2 / 2 | Con `DD_TRACE_OTEL_ENABLED` (sus spans salen del listener de actividades, como en su test). Descriptor de test: Quartz crea sus componentes y los jobs por nombre de tipo |
| Samples.RabbitMQ (7.2.2) | Paridad | 108 / 108 | También DSM (H-41, H-42, H-46) **Con la configuración de su test (OTel): 108 / 108 y 6 checkpoints DSM, también con el esquema v1.** |
| Samples.Remoting | No aplica | — | Solo net48 |
| Samples.RuntimeMetrics | Paridad | — | Las mismas 22 métricas `runtime.dotnet.*` por DogStatsD con EventListener y con `DD_RUNTIME_METRICS_DIAGNOSTICS_METRICS_API_ENABLED` |
| Samples.SQLite.Core | Paridad | 126 / 126 | **Con la configuración de su test (OTel): 126 / 126.** |
| Samples.ServiceStack.Redis | Parcial | 39 / 13 | Fase tipada con paridad; las otras dos usan `Assembly.LoadFile`/`AssemblyLoadContext` **Con la configuración de su test (OTel): igual (39 / 13).** |
| Samples.SqlServer (System.Data.SqlClient 4.9.1) | Parcial | 217 / 138 | Fase tipada con paridad; los procedimientos almacenados van después de la fase `LoadFile` **Con la configuración de su test (OTel): igual (217 / 138).** |
| Samples.SqlServer.NetFramework20 | No aplica | — | Solo net48 |
| Samples.StackExchange.Redis (3.3.0) | Paridad | 203 / 199 | Los 4 que faltan son llamadas `dynamic` del propio sample (no compilan en NativeAOT); también solo con el catálogo **Con la configuración de su test (OTel): igual (203 / 199).** |
| Samples.Telemetry | Paridad | 1 / 1 | |
| Samples.TraceAnnotations | Paridad | 18 / 18; 68 / 68 | Atributos (H-44) y, con el `DD_TRACE_METHODS` de su test, 68 / 68 (H-59) |
| Samples.TraceAnnotations.VersionMismatch.AfterFeature, Samples.TraceAnnotations.VersionMismatch.BeforeFeature | Por diseño | 18 / — | `Datadog.Trace` 2.x por NuGet |
| Samples.Trimming | Incompatible | 30 / 2 | Controladores MVC (404); los spans de esa petición sí salen |
| Samples.VersionConflict.1x, Samples.VersionConflict.2x | Por diseño | 2 / —, 3 / — | `Datadog.Trace` 1.29 y 2.x por NuGet |
| Samples.Wcf | No aplica | — | Solo net48 |
| Samples.WeakCipher (IAST) | Paridad | 6 / 6 | 6 vulnerabilidades `WEAK_CIPHER` |
| Samples.WebRequest | Paridad | 91 / 91 | **Con la configuración de su test (OTel, peer service y mapeos de servicio): 134 / 134.** |
| Samples.WebRequest.NetFramework20 | No aplica | — | Solo net48 |
| Samples.Yarp.DistributedTracing | Incompatible | 4 / 0 | YARP crea sus componentes por reflexión en la DI; igual en `plain` (excepción de arranque) |
| dependency-libs (ActivitySampleHelper, LogsInjectionHelper, LogsInjectionHelper.VersionConflict, PluginApplication, Samples.DatabaseHelper, Samples.DatabaseHelper.netstandard, Samples.DatabaseHelper.NetFramework20, Samples.ExampleLibrary, Samples.ExampleLibraryTracer, Samples.NoMultiLoader.Deps, Samples.WebRequestHelper.NetFramework20) | Biblioteca | — | Se prueban con sus samples |

## tracer/test/test-applications/regression

| Sample | Estado | Spans JIT / AOT | Evidencia y notas |
|---|---|---|---|
| AspNetCoreSmokeTest | Incompatible | 7 / 2 | Controladores MVC (`AddControllers`/`AddMvc`): en NativeAOT no se descubren y la petición a `/api/values` falla; el sample exige `PROFILER_IS_NOT_REQUIRED=True` sin el profiler |
| AssemblyLoad.FileNotFoundException | Paridad | 1 / 1 | |
| AssemblyLoadContextRedirect | No aplica | — | Carga el ensamblado en un `AssemblyLoadContext` propio (y referencia `Datadog.Trace` 2.1 por NuGet) |
| AssemblyLoadContextResolve | No aplica | — | Comprueba el `AssemblyLoadContext` del loader del profiler |
| AssemblyResolveMscorlibResources.InfiniteRecursionCrash | Paridad | 0 / 0 | Misma salida (la excepción original se lanza y se captura) |
| DataDogThreadTest | No evaluable | 10 / 0 | El sample falla también bajo JIT (`SampleHelpers.GetTraceId` lanza `TargetException`); su smoke test está desactivado en CI |
| DeepNestedHierarchy | No aplica | 1 / — | Regresión del profiler nativo (jerarquía genérica muy profunda, como la de OData): el equivalente en NativeAOT es instrumentarla al publicar, que funciona; el sample exige el profiler y sale sin él |
| Devart.Data.DBCommand | Paridad | 3 / 3 | |
| DogStatsD.RaceCondition | No evaluable | 2 500 / 0 | El sample falla también bajo JIT (igual que DataDogThreadTest); ningún test lo usa |
| DuplicateTypeProxy | Incompatible | 1 / 0 | `Assembly.LoadFile`/`AssemblyLoadContext` por diseño del sample |
| EnumerateAssemblyReferences | Incompatible | 0 / 0 | El sample llama a `Assembly.GetReferencedAssemblies()`, que NativeAOT no soporta (`PlatformNotSupportedException`) |
| HttpMessageHandler.StackOverflowException | Paridad | 3 / 3 | |
| IBM.Data.DB2.DBCommand | No aplica | — | Solo net48 |
| MismatchedTracerVersions/Mismatched.AspNetCore, MismatchedTracerVersions/Mismatched.Cli | Por diseño | — | `Datadog.Trace` 1.28 por NuGet (además, necesitan un feed local de paquetes) |
| NetCoreAssemblyLoadFailureOlderNuGet | Por diseño | 3 / — | `Datadog.Trace` 1.19.1 por NuGet |
| Reproduction.Wpf.ExpenseIt (ExpenseItDemo) | No aplica | — | WPF (Windows) |
| RuntimeMetricsShutdown | Paridad | — | Sale bien (rc 0) igual que bajo JIT |
| Sandbox.AutomaticInstrumentation, Sandbox.LegacySecurityPolicy | No aplica | — | Solo net48 |
| ServiceBus.Minimal.MassTransit | Incompatible | 1 934 / 0 | MassTransit/EF Core: resolución y DI por reflexión |
| ServiceBus.Minimal.NServiceBus | No evaluable | — | No publica ni bajo JIT en este entorno |
| ServiceBus.Minimal.Rebus | Incompatible | 204 / 3 | Rebus: DI por reflexión |
| StackExchange.Redis.AssemblyConflict.LegacyProject | No aplica | — | Proyecto clásico de .NET Framework 4.8 |
| StackExchange.Redis.AssemblyConflict.SdkProject | Paridad | 8 / 8 | Dos StackExchange.Redis (con y sin nombre seguro) en la misma aplicación |
| StackExchange.Redis.StackOverflowException (2.1.58) | Paridad | 38 / 38 | |
| dependency-libs/AppDomainInstance | No aplica | — | Solo net48 |
| dependency-libs (Datadog.StackExchange.Redis, .Abstractions, .StrongName) | Biblioteca | — | Se prueban con StackExchange.Redis.AssemblyConflict.SdkProject |

## tracer/test/test-applications/security

| Sample | Estado | Evidencia y notas |
|---|---|---|
| Samples.Security.AspNetCore2 | No aplica | netcoreapp2.1 |
| Samples.Security.AspNetCore5 | Incompatible | ILC no compila el IL de una de sus dependencias, también sin Datadog (`Invalid IL or CLR metadata`); además usa MVC |
| Samples.Security.AspNetCoreBare | Incompatible | Arranca, pero sus controladores MVC no se descubren en NativeAOT (404), también sin Datadog |
| aspnet/Samples.Security.AspNetMvc5, aspnet/Samples.Security.WebApi, aspnet/Samples.Security.WebForms | No aplica | .NET Framework (IIS) |

AppSec en NativeAOT se valida con el test del paquete (`ManualApiApplicationWithThePackage`: ataque, RASP con
`rasp-930-100`, IAST) y con la app de test `CodeOrigin.MinimalApi` del harness: activación remota (`ASM_FEATURES`), la WAF
detecta lo mismo que bajo JIT (`crs-930-120`, `crs-941-110`) y el bloqueo de IP por `ASM_DATA` (403, `blk-001-001`).

## tracer/test/test-applications/debugger

| Sample | Estado | Evidencia y notas |
|---|---|---|
| Samples.Debugger.AspNetCore5, Samples.Probes (y sus dependency-libs: Samples.Probes.External, Samples.Probes.TestRuns, Samples.Probes.Unreferenced.External) | No aplica | Dynamic Instrumentation y Exception Replay instrumentan métodos en runtime (ReJIT); en NativeAOT se desactivan con un aviso (H-57). Code Origin for spans sí funciona: etiquetas idénticas a JIT en `CodeOrigin.MinimalApi` |

## tracer/test/test-applications/aspnet

| Sample | Estado | Evidencia y notas |
|---|---|---|
| Samples.AspNet.MultipleAppsInDomain, Samples.AspNet.VersionConflict, Samples.AspNet472.LoaderOptimizationRegKey, Samples.AspNetAsyncHandler, Samples.AspNetMvc4, Samples.AspNetMvc5, Samples.Owin.Iis.WebApi2, Samples.WebForms | No aplica | .NET Framework (IIS) |

## tracer/test/test-applications/azure-functions

| Sample | Estado | Evidencia y notas |
|---|---|---|
| Samples.AzureFunctions.V3InProcess, Samples.AzureFunctions.V4InProcess | No aplica | Modelo in-process: las funciones se cargan en el host de Functions |
| Samples.AzureFunctions.V4Isolated, .AspNetCore, .AspNetCore.SdkV1, .Durable, .HostLogsDisabled, .Messaging, .SdkV1 | No aplica | Worker aislado que lanza el host de Functions (`func`); sus tests corren con el host en Windows (`BuildAndRunWindowsAzureFunctionsTests`) |

## tracer/test/test-applications/instrumentation

| Sample | Estado | Evidencia y notas |
|---|---|---|
| CallTargetNativeTest | Paridad | 18 modos: los mismos `BeginMethod`/`EndMethod` y excepciones que con el profiler (test opt-in `AotInstrumentNativeHostIntegrationTests`), y en NativeAOT 16 de 18 idénticos (los otros dos solo cambian el orden de una continuación y el formato de una traza, H-20) |
| Datadog.Tracer.Native.Checks | No aplica | `Main` vacío: el proyecto existe para las comprobaciones del nativo |

## tracer/test/test-applications/throughput

| Sample | Estado | Evidencia y notas |
|---|---|---|
| Samples.AspNetCoreSimpleController | No aplica | Benchmark (net5.0, MVC) |
| Samples.KafkaBenchmark | No aplica | Benchmark de Kafka |

## tracer/samples (ejemplos de la documentación)

| Sample | Estado | Evidencia y notas |
|---|---|---|
| OpenTelemetry (OpenTelemetry.AspNetCoreApplication) | Paridad | 8 / 8 con `DD_TRACE_OTEL_ENABLED`: ASP.NET Core, HttpClient y la actividad propia del sample |
| AutomaticTraceIdInjection (Log4NetExample, MicrosoftExtensionsExample, NLog40Example, NLog45Example, NLog46Example, SerilogExample) | No aplica | net7.0/net462; los mismos escenarios se prueban con LogsInjection.* |
| AzureFunctionsWithAgentLessLogging | No aplica | Azure Functions |
| ConsoleApp | No aplica | netcoreapp2.1/3.1; equivalente a Samples.Console |
| IISInDocker, WindowsContainer | No aplica | IIS / contenedor de Windows |
| NugetDeployment | No aplica | net5.0 |
| RabbitMQ.DistributedTracing (Send, Receive) | No aplica | netcoreapp3.1; el escenario se prueba con Samples.RabbitMQ y Samples.DataStreams.RabbitMQ |
| ServiceFabricRemoting (WebApp, WeatherService.NetCore31, WeatherService.NetFx461, WeatherService.Abstractions) | No aplica | Service Fabric |

## tracer/tools

| Sample | Estado | Evidencia y notas |
|---|---|---|
| Samples.Transport.UnixDomainSocket | No aplica (herramienta, netcoreapp3.1); su funcionalidad, con paridad | El transporte por Unix domain socket hacia el agente (`DD_TRACE_AGENT_URL=unix://…`) se prueba con Samples.HttpMessageHandler y el agente simulado escuchando en un socket: 222 / 222, con las trazas, la telemetría y el discovery por el socket en JIT y en NativeAOT |

## profiler/src/Demos

| Sample | Estado | Evidencia y notas |
|---|---|---|
| Samples.BuggyBits, Samples.Computer01, Samples.ExceptionGenerator, Samples.FileAccess, Samples.HttpRequest, Samples.ParallelCountSites, Samples.WaitHandles, Samples.Website-AspNetCore01, Website-AspNet (y Shared/RuntimeMetrics, Shared/Util) | No aplica | Demos del Continuous Profiler, que necesita la API de profiling del CLR |

## shared/samples

| Sample | Estado | Evidencia y notas |
|---|---|---|
| Datadog.AutoInstrumentation.ManagedLoader.Demo (Driver, DefAD.Asm1) | No aplica | Demo del loader gestionado (net45/netcoreapp2.0) |
| Datadog.DynamicDiagnosticSourceBindings.Demo (LateLoadDS.NetFx, LoadUnloadPlugin.NetCore31, Simple.NetCore31, Simple.NetFx45) | No aplica | Demos de carga dinámica de DiagnosticSource (netcoreapp3.1/.NET Framework) |
| Datadog.Logging.Demo (EmitterAndComposerApp, EmitterLib) | No aplica | Demo de la librería de logging compartida (netcoreapp3.1) |

## Harness

| App | Estado | Evidencia y notas |
|---|---|---|
| apps/CodeOrigin.MinimalApi (solo en el harness) | Paridad | Minimal API con RDG y `RequestDelegate`: Code Origin (`_dd.code_origin.*` idénticas, H-57), activación remota de AppSec y bloqueo de IP por RCM |
