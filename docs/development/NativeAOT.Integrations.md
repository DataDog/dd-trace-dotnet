# Inventario de integraciones en NativeAOT

Vista por integración (las 63 de CallTarget de `supported_calltargets.g.json` y las que no lo son) de qué funciona en una
aplicación NativeAOT con `Datadog.Trace.Aot`, qué no y por qué. Complementa [`NativeAOT.Samples.md`](./NativeAOT.Samples.md)
(la vista por sample, con la evidencia e2e) y el [tracker](./NativeAOT.Tracker.md) (hallazgos H-n).

**Fuentes** (2026-10-14):

- El e2e de samples (JIT con el profiler frente a NativeAOT, mismos spans y etiquetas): `NativeAOT.Samples.md`.
- Una pasada de publish NativeAOT de los 94 samples aplicables (net8.0, ILC 8) con las herramientas actuales, el
  instrumentador en `--verbose` y `TrimmerSingleWarn=false`: sus informes (`report.json`, compatibilidad del registro de
  duck typing) y los avisos de ILC de Datadog.Trace en el código que cada aplicación alcanza de verdad.
- ILC con Datadog.Trace entero como raíz (`TrimmerRootAssembly`), para ver el código dinámico de todas las integraciones,
  también las que ningún sample alcanza.
- Las propias librerías: si se declaran compatibles con trimming/AOT (`AssemblyMetadata("IsTrimmable")`, lo que pone
  `IsAotCompatible`) en su última versión de NuGet, y el modo `plain` del e2e (la aplicación en NativeAOT sin Datadog).

## Cómo falla una integración en NativeAOT

- **CallTarget**: el build enlaza los métodos de la integración con cada método instrumentado (`CallTargetRegistryGenerator`).
  Si no puede (falta un miembro, el duck typing de una restricción falla), registra el fallo y, en runtime, el primer uso
  lanza `CallTargetInvokerException`: la integración se desactiva para ese tipo con un aviso en el log y la telemetría
  `IntegrationDisabledDueToError`. No hay enlace en runtime (`IntegrationMapper` emite IL, que NativeAOT no tiene).
- **Métodos genéricos instrumentados**: cada instanciación se registra sola desde la entrada del método reescrito (la
  registración es genérica sobre sus parámetros), así que funcionan con cualquier instanciación. Solo los proxies de duck
  typing que dependen de los argumentos genéricos ("formas diferidas") necesitan las instanciaciones que el build encuentra
  (`GenericInstantiationDiscovery`).
- **Duck typing**: el registro AOT reproduce el motor dinámico, también sus fallos (el mismo comportamiento que en JIT). Un
  mapeo que no está declarado ni sale de una restricción falla en runtime; `CheckAotDuckTypeDeclarations` exige en CI que
  estén declarados todos los que graban los integration tests (salvo CI Visibility y Debugger, fuera).
- **Código dinámico en los helpers** (`MakeGenericType`, `DynamicMethod`, reflexión): con alternativa para NativeAOT
  (H-41 y siguientes) o funcionando por lo que el build conserva (tipos buscados por nombre, wrappers de delegados, modelos
  JSON). ILC avisa de lo que no puede demostrar (ver "Avisos de ILC").

## Resultado de la pasada (94 samples, ILC 8)

- **Publish**: 93/94. `Security.AspNetCore5` falla en ILC también sin Datadog (una dependencia con IL inválido; además MVC).
- **CallTarget**: ningún fallo propio de NativeAOT. El único fallo de enlace es `GetRecords` de Kinesis (busca
  `StreamARN`, que la versión del sample no tiene), igual bajo JIT.
- **Formas genéricas diferidas**: ninguna sin resolver, ni en los 27 samples donde la búsqueda de instanciaciones llega a su
  límite (50.000 contextos, H-76).
- **Duck typing**: ningún mapeo que falle solo en NativeAOT, salvo en los dos samples que referencian su propio
  Datadog.Trace (`CallTargetNativeTest`, `LifetimeManager.TerminationSignals`: el generador no puede usar el motor dinámico
  como oráculo con otro build). Los mapeos declarados cuyo tipo no existe en la versión de la librería se ignoran, como bajo
  JIT.
- **Emulación de metadatos**: solo `IMetaDataEmit.DefineMethod` sin implementar, que pide la reescritura de conflicto de
  versiones de Datadog.Trace (`DistributedTracer`): no aplica, una aplicación NativeAOT tiene un único Datadog.Trace.
- **Hallazgos nuevos**: H-72 a H-76 (abajo).

## Por integración

Estados: **Soportada** (paridad e2e), **Soportada (librería parcial)** (lo que la librería ejecuta tiene paridad; el resto
falla también sin Datadog), **No soportada (librería)** (la librería no funciona en NativeAOT; no hay nada que instrumentar),
**No aplica**, **Sin evidencia** (sin sample; hay que probarla) y **Aplazada** (decisión del usuario).

| Integración | Librería (versión probada) | Estado | Evidencia y límites | Acción |
|---|---|---|---|---|
| AdoNet | ADO.NET (proveedores) | Soportada | FakeDbCommand 112/112, Devart, todos los proveedores de abajo | — |
| SqlClient | System.Data.SqlClient 4.9.1, Microsoft.Data.SqlClient 7.1.0 | Soportada | Fase tipada con paridad; las fases `Assembly.LoadFile` de los samples no aplican | — |
| Npgsql | 10.0.3 | Soportada | Fase tipada con paridad; librería compatible | — |
| MySql | MySql.Data, MySqlConnector 2.6.2 | Soportada | Fase tipada con paridad (MySql.Data con descriptor de test) | — |
| Oracle | Oracle.ManagedDataAccess.Core | Soportada | Paridad contra Oracle Free (27 grupos); `Oracle.DataAccess` (no gestionado) no aplica | — |
| Sqlite | Microsoft.Data.Sqlite 10, System.Data.SQLite.Core | Soportada | 126/126 los dos | — |
| Aerospike | 8.5.0 | Soportada | 19/19; la librería no se declara compatible pero funciona | — |
| AspNetCore | ASP.NET Core (Kestrel, minimal APIs con RDG) | Soportada | CodeOrigin.MinimalApi, Rasp.MinimalApi, app del paquete (AppSec, RASP, IAST). MVC y Razor Pages no funcionan en NativeAOT (framework): sus partes de la integración (model binding para AppSec/IAST, spans de MVC) no aplican | — |
| AspNet, AspNetMvc, AspNetWebApi2, Wcf, Msmq, Remoting | .NET Framework | No aplica | NativeAOT es .NET 8+ | — |
| AwsSdk, AwsS3, AwsSqs, AwsSns, AwsDynamoDb, AwsEventBridge, AwsKinesis, AwsStepFunctions | AWSSDK v4 | Soportada | Paridad, también DSM; v4 se declara compatible. Con AWSSDK 3.x el SDK crea sus servicios por reflexión (librería). Kinesis `GetRecords` necesita `StreamARN` (también bajo JIT) | — |
| AwsLambda | Amazon.Lambda.RuntimeSupport | Soportada | Spans serverless, manual y HTTP por invocación; el error del sample es suyo (System.Text.Json por reflexión) | — |
| AzureEventHubs | 5.12.2 | Soportada | Los cuatro modos del test | — |
| AzureServiceBus | 7.17.5 | Soportada | Cinco escenarios del test | — |
| AzureFunctions, ServerlessCompat | Microsoft.Azure.Functions.Worker 2.x, Datadog.Serverless.Compat | Sin evidencia | NativeAOT en el worker aislado no está documentado oficialmente (hay incidencias de serialización por reflexión); Worker.Core no se declara compatible. Hace falta el host de Functions | Probar con el host (job de Azure Functions) o declararla no soportada |
| CosmosDb | 3.56 | Soportada (librería parcial) | CRUD con paridad; las queries fallan en el SDK (plan de consulta con Newtonsoft y `MakeGenericType` de tipos valor) | — |
| Couchbase | CouchbaseNetClient 2.7.27 / 3.x | Soportada (2.x) / No soportada (librería, 3.x) | 3.x crea `Logger<T>` con `MakeGenericType` en su DI | — |
| DatadogTraceManual | API manual, OpenTracing | Soportada | ManualInstrumentation 47/47, OpenTracing, DSM manual | — |
| DatadogTraceVersionConflict | Datadog.Trace 1.x/2.x por NuGet | No aplica | Por diseño: el publish pide la API manual de la versión actual | — |
| ElasticsearchNet | NEST 5/6/7, Elasticsearch.Net | No soportada (librería) | Converters y respuestas por reflexión; v7 emite IL | — |
| GraphQL | GraphQL.NET 2.x, 3.x, 7.x / 4.x-5.x | Soportada (librería parcial) / No soportada (librería, 4.x-5.x) | Mismos spans; los resolvers por reflexión de la librería fallan en algunas operaciones. 4.x-5.x construyen sus tipos con `MakeGenericType` | — |
| Grpc | Grpc.Net 2.67, Grpc.Core 2.45 | Soportada | 80/80 y paridad; Grpc.Net se declara compatible | — |
| Hangfire | 1.7.0 | Soportada | Con la propagación por el parámetro del job (H-61) | — |
| HotChocolate | 13-15 | No soportada (librería) | Su DI no encuentra constructores | — |
| HttpMessageHandler (Socket, Curl, WinHttp) | System.Net.Http | Soportada | 222/222 en net8-net11 | — |
| WebRequest | System.Net.Requests | Soportada | 134/134 | — |
| Process | System.Diagnostics.Process | Soportada | 10/10 con la recogida de comandos | — |
| IbmMq | amqmdnetstd / IBMMQDotnetClient | Sin evidencia | Sin sample; la librería no se declara compatible | Probar (contenedor de IBM MQ) |
| ILogger | Microsoft.Extensions.Logging | Soportada | Logs injection y envío directo. Microsoft.Extensions.Telemetry (`ExtendedLogger`) no funciona (DI por reflexión, librería) | — |
| Serilog | Serilog 4 | Soportada | Logs injection y envío directo | — |
| NLog | NLog 5 | Soportada | También solo con el catálogo | — |
| Log4Net | log4net | No soportada (librería) | No crea sus appenders en NativeAOT | — |
| Kafka | Confluent.Kafka 2.15.1 | Soportada (librería parcial) | Productor con paridad (con y sin handler de entrega, DSM); los consumidores de la librería no reciben en NativeAOT, tampoco sin Datadog. Sin probar: el cluster id (la versión del sample no tiene `DescribeCluster`; tipos buscados por nombre y `Activator.CreateInstance`). Riesgo: el handler de entrega por defecto se crea con `MakeGenericType(DeliveryReport<TKey,TValue>)`; con claves o valores de tipo valor puede no existir y el span del `Produce` síncrono se cerraría sin el informe de entrega (H-75) | Instanciaciones desde el build (H-75); probar el cluster id con una versión con `DescribeCluster` |
| MongoDb | MongoDB.Driver 3.12 | Soportada (librería parcial) | Los primeros comandos con paridad; el driver falla en sus serializadores (`MakeGenericType` de tipos valor) | — |
| OpenFeature | 2.3.0 | Soportada | Remote Configuration `FFE_FLAGS` y exposición | — |
| OpenTelemetry | OpenTelemetry SDK 1.18 / OpenTelemetry.Api 1.3.1 | Soportada / No soportada (librería, 1.3.1) | Con `DD_TRACE_OTEL_ENABLED` y el recurso. Exportación OTLP de trazas en JSON rota en .NET 8 (H-73) | Arreglar H-73 |
| Protobuf | Google.Protobuf | Soportada | DSM | — |
| RabbitMQ | RabbitMQ.Client 7.2.2 | Soportada | 108/108 y DSM | — |
| Quartz (actividades) | Quartz 3.22 | Soportada | Por el listener de actividades | — |
| ServiceStackRedis | ServiceStack.Redis | Soportada | Fase tipada con paridad | — |
| StackExchangeRedis | 2.1-3.3 | Soportada | También dos versiones en la misma aplicación | — |
| HashAlgorithm, SymmetricAlgorithm, aspectos de IAST y RASP (call sites) | BCL | Soportada | WeakCipher, Deduplication, LFI/PATH_TRAVERSAL en la app del paquete; los call sites se instrumentan al compilar | — |
| Ssrf (RestSharp), Xss, StackTraceLeak, NHibernate (IAST) | RestSharp, ASP.NET Core | Sin evidencia | Sin sample específico; RestSharp no se declara compatible | Probar con las apps de seguridad |
| TraceAnnotations | `[Trace]`, `DD_TRACE_METHODS` | Soportada | 68/68 | — |
| ServiceRemoting | Service Fabric | No soportada (librería) | Service Fabric Remoting emite sus proxies con Reflection.Emit; en NativeAOT no se arranca (check constante) | — |
| Ocelot, YARP (vía HttpMessageHandler) | Ocelot, YARP | No soportada (librería) | Su DI y middlewares por reflexión | — |
| MassTransit, Rebus, Dapper, EF Core (sin integración propia) | — | No soportada (librería) | DI, `Reflection.Emit` | — |
| XUnit, NUnit, MsTestV2, DotnetTest, Selenium, TestPlatformAssemblyResolver | Frameworks de test | Aplazada (CI Visibility) | El modo NativeAOT de xUnit v3 usa otros ensamblados; el de MSTest es alpha (F12.3) | — |
| Dynamic Instrumentation, Exception Replay, SymDB | — | Aplazada | Reescriben métodos o leen los ficheros en runtime; no se compilan en NativeAOT | — |

## Avisos de ILC en las aplicaciones reales

49 avisos distintos de Datadog.Trace en los 94 samples (con `TrimmerSingleWarn=false`), en código que esas aplicaciones
alcanzan; ninguno con las tres apps de referencia (consola, minimal API, app del paquete). Con el comportamiento verificado:

- **Funcionan por lo que conserva el build o tienen alternativa para NativeAOT** (paridad e2e): helpers de cabeceras de
  SQS/SNS/Kafka y RabbitMQ (H-41), metadatos de gRPC legacy, filtros de Hangfire, NLog (contextos, reglas, layouts),
  Serilog (sink, propiedades), serialización BSON de MongoDB, procesadores de OpenTelemetry, `TraceAnnotationInfoFactory`,
  `ActivatorHelper.DefaultActivator`. Acción: supresiones justificadas (H-75).
- **Newtonsoft por `System.ComponentModel`** (`JObject`, `JContainer.ITypedList`): Datadog.Trace no los usa así. Acción:
  añadirlos a `VendorsTrimming.cs`.
- **AppSec `ObjectExtractor`**: solo en aplicaciones con MVC (model binding), que no funcionan en NativeAOT.
- **Log4Net `TryAddAppenderToResponse`**: log4net no funciona en NativeAOT.
- **Kafka**: el handler de entrega por defecto y su wrapper (`MakeGenericType`, `MakeGenericMethod`) son el único caso con
  pérdida posible de datos (ver Kafka arriba).

## Hallazgos nuevos

- **H-72**: el escritor del PDB portable de dnlib registra `[ERR] ... Expected a null constant` al reescribir GraphQL 7 y
  Rebus (una constante local del PDB). El ensamblado se escribe bien y NativeAOT no usa ese PDB en runtime, pero el `[ERR]`
  sale en el build del cliente: rebajar los errores del PDB a aviso.
- **H-73**: ILC 8 no compila `EnumerateTags<TagWriter<__Canon>>` (struct genérico sobre un tipo referencia en un método
  genérico virtual): la exportación OTLP de trazas en JSON lanza `InvalidProgramException` en .NET 8 NativeAOT (verificado;
  protobuf funciona; ILC 9+ bien). Arreglo en el tracer: que el estado del `TagWriter` del JSON sea un struct.
- **H-74**: con ILC 8 algunas aplicaciones sin ASP.NET Core (73 samples) compilan los observers de ASP.NET Core y
  `RunBlockingCheckUnsafe` (mensajes "will always throw ... Failed to load assembly") aunque el switch
  `Datadog.Trace.AspNetCore.IsSupported` está a `false`: no se ejecutan, pero el mensaje sale en el build. Buscar qué los
  alcanza (grafo de dependencias de ILC).
- **H-75**: los avisos de ILC de las integraciones verificadas (arriba) necesitan sus supresiones justificadas, y el handler
  de entrega de Kafka sus instanciaciones desde el build (el instrumentador conoce los `Producer<TKey,TValue>` de la app).
- **H-76**: la búsqueda de instanciaciones genéricas llega a su límite en 27 samples sin consecuencias hoy (ninguna forma
  diferida sin resolver); revisar el límite o acotar mejor los módulos que recorre.
