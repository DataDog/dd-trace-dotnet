# Plan: soporte NativeAOT completo en el tracer

> Documento de trabajo, sin trackear en git. Versión 3 (2026-10-08): decisiones cerradas y revisión profunda (sección 12).
> Seguimiento del día a día: [`NativeAOT.Tracker.md`](./NativeAOT.Tracker.md).

## 0. Resumen ejecutivo

- **Meta**: que una app publicada con `PublishAot=true` tenga el tracer funcionando con la mayor paridad funcional posible respecto a JIT + profiler nativo. Eso incluye integraciones CallTarget, DuckTyping, arranque del tracer y servicios (métricas, telemetría, RCM, AppSec). IAST es una opción activable en build.
- **Cómo**:
  - la reescritura de IL en build la hace **el profiler nativo alojado offline** (idea de #5736, reimplementada sobre dnlib, multiplataforma y sin fork de Cecil), con gate en la Fase 0;
  - el runtime usa **despacho estático** por adapters generados (structs genéricos);
  - DuckType usa el generador de la base;
  - todo se entrega con el paquete NuGet **`Datadog.Trace.Aot`**.
- **Dónde**: rama nueva apilada sobre #8252 (sección 10). #8252 queda como la base DuckType AOT.

## 1. Decisiones cerradas (2026-10-08)

| # | Decisión | Motivo |
|---|---|---|
| D-1 | La reescritura la hace el **nativo alojado offline**, con gate en la Fase 0 y plan B managed | Paridad por construcción, definiciones canónicas e IAST incluidos (sección 2) |
| D-2 | Runtime con **despacho estático por adapters** (no registro por type handles) | Targets genéricos, sin reflexión ni `MethodInfo.Invoke` (3.3) |
| D-3 | **dnlib** para todo el tooling AOT nuevo | Alineado con el generador DuckType; tokens traducibles sin fork |
| D-4 | **IAST**: opción activable en build (`DatadogAotIast`) | Coste en build y en runtime solo si se pide |
| D-5 | **Retirar `apply-aot`** legacy | Está roto y no sirve para NativeAOT |
| D-6 | Paquete NuGet **`Datadog.Trace.Aot`** (libre en nuget.org) | `Bundle` copia el home a la salida; `Datadog.Trace` es solo la API manual |
| D-7 | **Rama nueva apilada sobre #8252**; troceo posterior en PRs por componente | Revisabilidad y code owners distintos (nativo/managed); #8252 se puede mergear por sí solo |
| D-8 | **GitHub** (#5736, #8383, descripciones) lo gestiona el usuario cuando haya algo funcionando | Seguimos en prototipo |
| D-9 | Instrumentador en **proceso propio**, nunca tarea MSBuild in-process | Estado global del nativo; los nodos de MSBuild se reutilizan |
| D-10 | El pipeline **solo actúa en `publish` con `PublishAot=true`**, antes de ILC, y **nunca sobrescribe `obj/`** | `build`, `run` y la depuración no cambian; publish incremental seguro |
| D-11 | Política de error: **warning y app sin instrumentar** por defecto; `DatadogAotFailOnError=true` para fallar | No romper la build del cliente por nuestra herramienta |
| D-12 | Runtime: **holders genéricos + handlers dinámicos reutilizados**; registro en el contexto genérico mediante `Registration_N.Ensure()` insertado a la entrada del método (sección 3.3; sustituye D-2) | Mismo código de runtime que en JIT, hot path intacto, targets genéricos soportados |

---

## 2. Estado de partida

### 2.1 Base (#8252, `a82583bb66`)

| Área | Estado |
|---|---|
| DuckTyping AOT | Completo. Generador dnlib (`Runner/DuckTypeAot/*`), registro con contrato y MVID, `DuckTypeAotEngine`, `DuckType.RuntimeMode`, discovery por atributos (solo 6 usos de `[DuckType]` en `Datadog.Trace`), recorder en runtime (`DuckTypeAotDiscoveryRecorder`), `verify-compat`, props con `TrimmerRootDescriptor`, publish NativeAOT e2e (`DuckTypeAotNativeAotPublishIntegrationTests`), suites de paridad, benchmarks y docs `DuckTyping.NativeAOT*.md`. |
| Patrón de registro | Inicializador de módulo que solo se activa sin código dinámico (bajo JIT, `DuckTypeAotRegistryBootstrap.Initialize()` explícito). Registros aislados, activadores tipados y `IgnoresAccessChecksTo` (funciona en NativeAOT). |
| Integración en el build | Manual: generador + `<Import Project="$(DuckTypeAotPropsPath)">` (`DuckTyping.NativeAOT.BuildIntegration.md`). |
| CallTarget AOT | **Nada.** Solo `IntegrationMapper.WriteCreateNewProxyInstance` usa el constructor interno `(instance, Type)`. |
| `apply-aot` legacy | Roto (`Definitions = null`). Se retira (D-5). |

### 2.2 PR #8383 (CallTarget NativeAOT, managed, Mono.Cecil)

- **Pipeline:** `generate` (discovery propio + matcher + registro de adapters) → `rewrite` (AotProcessor + bootstrap en `<Module>..cctor`) → props y targets.
- **Runtime:** `CallTargetAot` público, `CallTargetAotEngine` (type handles y búsqueda por nombre) y continuaciones AOT.
- **Estado:** e2e solo con las firmas del ejemplo; 23 problemas en la revisión (sección 8).

### 2.3 PR #5736 (POC: profiler nativo offline)

- **Qué hace:** un host managed carga `Datadog.Tracer.Native` (`DllGetClassObject`, CLSID "AoT") con `ICorProfilerInfo8` + `IMetaData*` emulados sobre un fork de Cecil (DataDog/cecil#1, sin mergear). Emula carga, JIT y ReJIT, y escribe cuerpos crudos.
- **Estado:**
  - POC solo Windows, con rutas hardcodeadas;
  - 179 `NotImplementedException`;
  - sin tipos anidados;
  - parado desde julio de 2024.
- **Por qué no sirve tal cual para NativeAOT:** inyecta el loader (P/Invoke + `Assembly.Load`) y CallTarget sigue usando Reflection.Emit en runtime.

---

## 3. Análisis y arquitectura

### 3.1 Por qué el nativo (resumen; detalle en la v2 del análisis)

- **Paridad del IL:** la reescritura nativa de CallTarget son unas 5,3k líneas C++ (BubbleUp, ByRefLike, `ref`/`out`, estáticos, structs, genéricos, más de 8 argumentos, Derived/Interface entre módulos, `SkipMethodBody`, runtime-async). El `AotProcessor` managed (747 líneas) no cubre BubbleUp, ByRefLike ni runtime-async.
- **Definiciones canónicas embebidas:** `InitEmbeddedCallTargetDefinitions(categories, platform)` e `InitEmbeddedCallSiteDefinitions`, con ADO.NET, categorías Tracing, AppSec, Iast y Rasp, y kinds.
- **Incluidos sin coste:** IAST CallSite (unas 7k líneas), `[Trace]`/`DD_TRACE_METHODS` y la API manual.
- **Oráculos:**
  - `DD_DUMP_ILREWRITE_ENABLED` vuelca el IL de runtime;
  - `CallTargetInvoker` funciona bajo JIT **sin profiler** (verificado), así que el IL reescrito offline se puede ejecutar sin NativeAOT.
- **Costes:**
  - emulación de unos 61 métodos de `IMetaData*` y unos 41 de `ICorProfilerInfo*` (Anexo A); el nativo pide `ICorProfilerInfo7` (obligatorio), 8, 10 y 12;
  - binario nativo del host (el tool ya distribuye win-x64/x86, linux-x64/arm64 glibc/musl y osx; no win-arm64);
  - cambios nativos en modo AoT;
  - bloqueo de versión (el nativo emite `AssemblyRef` a la versión exacta de `Datadog.Trace`).

### 3.2 Pipeline de build (`dotnet publish -p:PublishAot=true`)

```
publish → ... → (antes de ILC; nunca en build/run)
        │
        ▼
dd-trace aot instrument   (proceso propio; incremental con Inputs/Outputs; salida en obj/<...>/datadog-aot/)
  0. Comprueba la versión de host nativo, definiciones, Datadog.Trace.dll y registros
  1. Host: carga Datadog.Tracer.Native (RID del host) directamente, sin native loader ni sus bailouts,
     con CLSID AoT → Initialize(ICorProfilerInfo emulado; GetRuntimeInformation = CoreCLR <versión del TFM de la app>)
     → InitEmbeddedCallTargetDefinitions(Tracing|AppSec|Rasp [+Iast], TFM de Datadog.Trace)
       [+ InitEmbeddedCallSiteDefinitions si IAST] [+ TraceMethods/annotations de build] [+ fichero de definiciones extra]
  2. Emulación: Datadog.Trace (solo lectura), app, referencias [+ framework de ILC, Fase 7]
     → callbacks de carga → JITCompilationStarted por método (incluidos anidados)
     → RequestReJIT → GetReJITParameters → SetILFunctionBody → CilBody de dnlib
  3. Post-proceso: llamadas a CallTargetInvoker.* → adapters en el registro CallTarget → redirección
  4. DuckType: mapeos (restricciones, cierre G5, catálogo de CI, instanciaciones) → generador DuckType (in-process)
  5. Bootstrap (inicializador del registro CallTarget): contrato → DuckType Initialize() → Instrumentation.InitializeAot()
  6. Escritura determinista (+PDB) con atributo marcador; informe; lista de items a sustituir
        │
        ▼
Sustitución de items de entrada de ILC (aplicación, referencias, ResolvedFileToPublish, IlcReference)
+ Datadog.Trace.dll completo y registros SOLO como entradas de publish/ILC (nunca referencia de compilación)
+ TrimmerRootDescriptor + nativos de runtime (libddwaf, libdatadog) por RID
        │
        ▼
ILC (NativeAOT)
```

### 3.3 Runtime: registro en el contexto genérico + handlers reutilizados (D-12, sustituye al diseño de adapters struct)

- Los handlers dinámicos (`BeginMethodHandler*`, `BeginMethodSlowHandler`, `EndMethodHandler*`, generadores de continuación y `RuntimeAsyncEndMethodHandler*`) **se reutilizan tal cual**. Su lógica de `Invoke`, que es el hot path, no cambia.
- En su **constructor estático** (ruta fría) primero consultan un holder genérico `CallTargetAot<TIntegration, TDelegate>`, con `Delegate`, `Registered` y `PreserveContext`, y `CallTargetAotContinuation<TIntegration, TTarget, TReturn>` para el generador de continuación de `Task<T>`/`ValueTask<T>`, que hoy usa `MakeGenericType`. Si no hay registro, siguen con `IntegrationMapper` como hasta ahora.
- El registro generado contiene, por cada método instrumentado:
  - los métodos estáticos que replican el IL de `IntegrationMapper` (begin, end y async end), genéricos sobre los parámetros genéricos del tipo o método objetivo;
  - una clase `Registration_N<…>` cuyo constructor estático rellena los holders (aislado con try/catch).
- El post-proceso **inserta** `call Registration_N<!0…,!!0…>::Ensure()` a la entrada de cada método reescrito. El IL nativo no se toca más, los targets genéricos funcionan porque la instanciación llega por el contexto genérico, y bajo JIT sin registro todo sigue igual.
- Paridad de la lógica de runtime (scope, continuaciones, runtime-async, `BlockException`) **por construcción**: es el mismo código.

### 3.3.1 Diseño anterior (descartado): despacho estático por adapters

- Se sustituye `call CallTargetInvoker::BeginMethod<TI, TT, TA1>(TT, TA1&)` por `call Registry::Begin_N<!0, !!0...>(TT, TA1&)`, con la misma firma de pila. El cuerpo es `CallTargetInvoker.BeginMethodAot<TI, TT, TA1, Adapter_N<...>>(instance, ref a1)` (interno, accesible con `IgnoresAccessChecksTo`).
- `Adapter_N` es un `struct` con interfaz interna; se invoca con `default(TAdapter).Invoke(...)`, sin boxing.
  - Si el tipo de destino está **cerrado en build**, los proxies duck se crean directamente (`new Proxy(instance)`).
  - Si el target es **genérico abierto**, se usa la búsqueda en runtime del registro DuckType, para las instanciaciones descubiertas (C3/C6).
- Variantes: begin con 0 a 8 argumentos, begin lento (`object[]`), end void/valor, fin async (Task, Task\<T>, ValueTask, ValueTask\<T>) y `EndMethodRuntimeAsync` (.NET 10+). El generador de continuación se crea en concreto, sin `MakeGenericType`.
- La semántica de `CallTargetInvoker` (IsIntegrationEnabled, CanExecute, LogException, IntegrationOptions, BubbleUp, SkipMethodBody) sigue en un solo sitio.
- Como las llamadas reescritas ya apuntan a los adapters, **CallTarget no necesita interruptor de modo**: funciona igual bajo JIT (tests) y en NativeAOT. El modo AOT solo afecta a DuckType y a `InitializeAot`.

### 3.4 Por qué no el registro de #8383

Con targets genéricos no se puede registrar en build un adapter por cada `T`, y `MakeGenericMethod` no funciona en NativeAOT. Además, el registro necesita reflexión (búsqueda por nombre, `MethodInfo.Invoke`) y una API pública.

### 3.5 Contrato

- Contrato único: schema, versión y MVID de `Datadog.Trace`, MVID de los registros.
- Si la validación falla, la instrumentación se desactiva con un log. **Nunca** una excepción en el inicializador de módulo de la app.

### 3.6 Alcance

- **Apps:** net8.0+ con `PublishAot=true`. Las apps solo *trimmed* siguen con el profiler normal y `Datadog.Trace.Trimming`.
- **Hosts:** los RID del home del tool. Sin cross-OS (como NativeAOT); cross-arch sí.
- **Fault tolerant** (`DD_INTERNAL_FAULT_TOLERANT_INSTRUMENTATION_ENABLED`): desactivado en AoT.

---

## 4. Inventario: todo lo que falta

Origen: **B** = base, **#8383**, **#5736**, **N** = nuevo. Los IDs se usan en el tracker.

### A. Reescritura en build
| ID | Qué | Origen | Fase |
|---|---|---|---|
| A1 | Modo AoT nativo: CLSID, rejit síncrono; sin startup hook/loader, DI, profiler, inliners, fault tolerant, exclusiones de proceso, checks de runtime ni reescritura de PInvoke maps de `Datadog.Trace` | #5736 + N | 1 |
| A2 | ~~Corregir los `return` que faltan en `environment_variables_util.cpp`~~ (falso positivo: macros con `return`) | N | — |
| A3 | Host multiplataforma en proceso propio | #5736 + N | 2 |
| A4 | `ICorProfilerInfo` 7/8/10/12 emulado (Anexo A); `GetRuntimeInformation` según el TFM; el resto E_NOTIMPL con diagnóstico | #5736 + N | 2 |
| A5 | `IMetaData*` sobre dnlib con tokens falsos; IL y firmas traducidos; cuerpos originales leídos crudos de la imagen PE (RVA) | N | 2 |
| A6 | Carga: **CoreLib primero** (el nativo captura de ella las propiedades de la referencia a corlib; en NativeAOT, la del framework de ILC), `Datadog.Trace` (solo lectura), `Datadog.Trace.Manual` (**se escribe**: el nativo reescribe `IsManualInstrumentationOnly`), app y referencias; todos los métodos, anidados incluidos. Peticiones de ReJIT **diferidas** (cola y `GetReJITParameters` desde el hilo del host, como hace el CLR) para evitar reentradas con locks del nativo | #5736 + N | 2 |
| A7 | Definiciones: embebidas por categorías + fichero extra + TraceMethods/annotations (propiedad de build) | N | 2 |
| A8 | PDB: el nativo **no** entrega mapa de IL en CallTarget → transferir sequence points alineando instrucciones originales; salida determinista | N | 2 |
| A9 | Atributo marcador `DatadogAotInstrumented` (versión + hash): el instrumentador no reescribe dos veces | N | 2 |
| A10 | Diagnóstico por método e informe | N | 2 |
| A11 | Comprobación de versión entre las piezas | N | 2 |
| A12 | Retirar `apply-aot` (`Aot/AotProcessor.cs`, `AotCommand.cs`, registro en `Program.cs`) | N | 2 |
| A13 | Ensamblados del framework de ILC | N | 7 |
| A14 | IAST CallSite opt-in | #5736 | 2/6 |
| A15 | El profiler **normal** (JIT) se salta los módulos con el marcador A9, para evitar instrumentar dos veces si se ejecutan bajo profiler | N | 1 |

### B. Adapters y despacho
| ID | Qué | Origen | Fase |
|---|---|---|---|
| B1 | Escáner de instanciaciones `CallTargetInvoker.*` con contexto genérico | N | 4 |
| B2 | Resolución de handlers idéntica a `IntegrationMapper` (handlers opcionales, parámetros concretos o genéricos, `ref`, estático, todas las formas de `OnAsyncMethodEnd`, `PreserveContext`, duck en instancia, argumentos, retorno y resultado async) | #8383 (rehecho) | 4 |
| B3 | Emisión de adapters struct + redirección; proxies directos si el tipo está cerrado, búsqueda en runtime si es genérico abierto | #8383 (IL) + N | 4 |
| B4 | Adapter deshabilitado con diagnóstico para formas no soportadas | N | 4 |
| B5 | Informe de compatibilidad honesto | #8383 (rehecho) | 4 |
| B6 | `EndMethodRuntimeAsync` (.NET 10+) | N | 3/4 |

### C. DuckType
| ID | Qué | Origen | Fase |
|---|---|---|---|
| C1 | Restricciones de integraciones → mapeos | #8383 (portado) | 5 |
| C2 | Cierre de mapeos encadenados (G5) + `DuckCast`/`DuckAs`/`CreateCache` en cuerpos | N | 5 |
| C3 | Instanciaciones genéricas del IL de la app → `GenericInstantiationsFile` | N | 5 |
| C4 | G3: interfaces compuestas | N | 5 |
| C5 | Mapeo ausente → integración deshabilitada (como `DuckTypeException`) | B + N | 5 |
| C6 | **Catálogo de mapeos grabado en CI**: ejecutar los integration tests con `DuckTypeAotDiscoveryRecorder` y obtener los pares (proxy, tipo destino) por librería y versión. Va dentro de `Datadog.Trace.Aot` y el instrumentador lo filtra por los ensamblados presentes. Cubre también a los consumidores que no son CallTarget (DiagnosticObserver de ASP.NET Core, Activity/OTel, AppSec), que el análisis estático no puede inferir | B (recorder) + N | 5/10 |

### D. Runtime del tracer
| ID | Qué | Origen | Fase |
|---|---|---|---|
| D1 | Contrato unificado; modo AOT solo para DuckType e `InitializeAot` | B + #8383 | 3 |
| D2 | `CallTargetInvoker.*Aot<..., TAdapter>` internos | N | 3 |
| D3 | Continuaciones AOT sin `MakeGenericType`; Task `null` igual que en dinámico | #8383 (adaptado) | 3 |
| D4 | Gating en runtime: `DD_TRACE_ENABLED`, `DD_DISABLED_INTEGRATIONS`, `DD_TRACE_<X>_ENABLED`, categorías AppSec/Rasp/Iast y aspectos CallSite | N | 3/6 |
| D5 | Sin ramas nuevas en el hot path JIT | N | 3 |
| D6 | `Instrumentation.InitializeAot()` (equivalente a `InitializeNoNativeParts` sin profiler) | N | 6 |
| D7 | Guardas en `NativeMethods` | N | 6 |
| D8 | Código dinámico restante (Anexo B) | N | 8 |
| D9 | Analizadores AOT/trim en `Datadog.Trace` (net8.0+) y presupuesto de tamaño | N | 8 |
| D10 | Modo AOT en telemetría y en el log de arranque | N | 6 |

### E. Dependencias nativas de runtime
| ID | Qué | Fase |
|---|---|---|
| E1 | libddwaf por RID en el publish y resolución sin tracer home | 6 |
| E2 | libdatadog (pipeline, crashtracking) | 6 |
| E3 | Documentar que el profiler nativo no se usa en runtime AOT | 6 |

### F. MSBuild y paquete `Datadog.Trace.Aot`
| ID | Qué | Origen | Fase |
|---|---|---|---|
| F1 | Paquete `Datadog.Trace.Aot` con `buildTransitive` | N | 9 |
| F2 | Hook **solo en publish con `PublishAot=true`**, antes de ILC; Inputs/Outputs (incluida la versión del tool y las propiedades); sin rutas absolutas; no toca `obj/` ni `IntermediateAssembly` | #8383 (rehecho) | 9 |
| F3 | Sustituir **las entradas de ILC** (items exactos a verificar por versión de SDK 8/9/10) | #8383 + N | 9/7 |
| F4 | `Datadog.Trace.dll` completo y registros solo como entradas de publish/ILC, **nunca referencia de compilación**: chocaría con la API manual (`Datadog.Trace.Manual`), que expone los mismos tipos públicos | N | 9 |
| F5 | Propiedades: `DatadogAotEnabled`, `DatadogAotIast`, `DatadogAotFailOnError`, `DatadogAotDefinitionsFile`, `DatadogAotTraceMethods`, `DatadogAotReportPath` | N | 9 |
| F6 | Docs de usuario, troubleshooting y matriz | #8383 (rehecho) + B | 9 |
| F7 | Diseño del paquete: el instrumentador como herramienta ligera (extraído del Runner si hace falta), nativos del host quizá en paquetes por RID (como ILCompiler) para limitar el tamaño, nativos de runtime por RID de destino | N | 9 |

### G. Tests y CI
| ID | Qué | Fase |
|---|---|---|
| G1 | Oráculo de IL: volcado de runtime frente al offline | 0/2 |
| G2 | `CallTargetNativeTest` en NativeAOT frente a JIT + profiler (definiciones por fichero; en AOT se salta `InjectCallTargetDefinitions`) | 10 |
| G3 | Adapters con el dinámico como oráculo (artefactos AOT bajo JIT) | 3/4 |
| G4 | Sondas de la revisión de #8383 | 4 |
| G5 | Variantes NativeAOT de integration tests elegidos | 10 |
| G6 | CI NativeAOT por OS/arch con skip de infraestructura; benchmarks | 10 |
| G7 | e2e de escenarios: no-op, multiproyecto, publish incremental doble, genérico, estático, ref struct, async, runtime-async, API manual (**incluida una app que la usa: conflicto de referencias F4**), ASP.NET Core, HttpClient, Kafka, AppSec, IAST opt-in, `dotnet build`/`run` sin cambios | 6–10 |
| G8 | Profiler normal sobre ensamblados marcados (A15): sin doble instrumentación | 1 |

### H. Fuera de alcance explícito
Continuous Profiler; DI, Exception Replay y Code Origin; `DD_TRACE_METHODS` en runtime (solo en build); CI Visibility; fault tolerant. En AOT se desactivan con un log claro.

---

## 5. Qué copiamos de cada PR

### 5.1 De #8383 (commits `f80aaf93ea`, `48738ef048`, `6a34bd4ca3`, `d2a3d4c1f7`, `a95593828a`, `218d0a892d`; ref local `refs/remotes/pr/8383`)

| Origen | Acción | Destino |
|---|---|---|
| Ganchos AOT en `Begin/EndMethodHandler*.cs` | Descartar el diseño | D2/D5 |
| `AotTaskResultContinuationGenerator.cs`, `AotValueTaskResultContinuationGenerator.cs` | **Adaptar** | D3 |
| `CallTargetAotEngine.cs`, `CallTargetAotHandler*.cs`, `CallTargetAotInvoker.cs` | Descartar | Despacho estático |
| `CallTargetAotContract.cs` + excepciones de contrato | **Fusionar** | D1 |
| `CallTarget.AOT.cs` (público) | Descartar | API interna |
| `CallTargetAotRegistryAssemblyEmitter.cs` | **Portar a dnlib y corregir** | B2/B3 |
| `CallTargetAotDuckTypeSupport.cs` | **Portar** | C1 |
| `CallTargetAotDefinitionDiscovery.cs`, `CallTargetAotMethodMatcher.cs`, `CallTargetAotRewriteProcessor.cs`, cambios en `AotProcessor.cs` | Descartar (salvo plan B) | Nativo |
| `CallTargetAotGenerateProcessor.cs` (props, targets, informes) | **Reutilizar partes** | F2/F3/B5 |
| `CallTargetAot*Options/Command/ArtifactPaths/Manifest/Plan.cs` | Plantilla | `aot instrument` |
| Integraciones de ejemplo en `Datadog.Trace` + exclusiones en generadores | Descartar | Definiciones por fichero (A7) |
| Tests (`EngineTests`, `GenerateProcessorTests`, `NativeAotPublishIntegrationTests`) | **Adaptar** | G3/G4/G7 |
| `CallTarget.NativeAOT.md` | Reescribir | F6 |

### 5.2 De #5736 (`b30ec21553`, ref local `refs/remotes/pr/5736`)

| Origen | Acción | Destino |
|---|---|---|
| Nativo: `class_factory.*`, `dllmain.cpp`, `cor_profiler.*`, `rejit_preprocessor.cpp` | **Portar y ampliar** | A1 |
| `interop.cpp` (`GetAssemblyAndSymbolsBytes`) | Descartar | Sin loader |
| `Interfaces/*.cs` | **Copiar** | A4/A5 |
| `NativeStubGenerator.cs` | **Copiar** | Genera vtables exportadas (CCW con `UnmanagedCallersOnly`) e invokers; el `NativeObjectsGenerator` de `dd_dotnet` solo genera invokers (corregido en la implementación, 2026-10-08) |
| `ProfilerInterop.cs` | Reescribir | Multiplataforma |
| `Runtime/*` (Rewriter, ModuleMetadata, infos) | **Portar la lógica a dnlib** | A4–A6 |
| `Instrumentation.Initialize(true)` en el tool | Descartar | Exports `InitEmbedded*`, `RegisterCallTargetDefinitions3` |
| Fork de Cecil, `Samples.Aot` | Descartar | — |

### 5.3 De la base y master
DuckType AOT, suites de paridad, harness de publish NativeAOT, `IgnoresAccessChecksTo`, inicializador de módulo, `TracerHomeCache`, `CallTargetNativeTest`, `DD_DUMP_ILREWRITE_ENABLED`, `InstrumentationDefinitions.GetIntegrationId`, `DuckTypeAotDiscoveryRecorder`.

---

## 6. Plan por fases

Convenciones:
- tests antes de cerrar cada fase;
- cada test nuevo se valida contra el commit anterior en un worktree `--detach`;
- commits firmados `[Area] ...`;
- nada de commit, push ni GitHub sin pedirlo;
- el tracker se actualiza al cerrar cada paso.

### 6.0 Dependencias y orden

```
P (prerrequisitos) ─► F0 (gate) ─► F1 ─► F2 ─► F4 ─► F5 ─► F9 ─► F7
                      F3 ────────────────────►┘      F6 ◄── F3
F8 (independiente)    F10 (continuo)    F11 (cierre)
```
Orden en serie: **P → F0 → F3 → F1 → F2 → F4 → F5 → F6 → F9 → F7 → F8 → F10 → F11**. F3 no depende del host nativo.

### Prerrequisitos (P)
1. Crear la rama apilada y su worktree (sección 10); mover allí el plan y el tracker (sin trackear).
2. Build nativo local linux-x64 (`./tracer/build.sh CompileTracerNativeSrc`, después `BuildNativeTracerHome` si hace falta). Vigilar el disco (hay unos 23 GB libres).
3. Build del tracer managed y del home para ejecutar `CallTargetNativeTest` con profiler (base del oráculo).

### Fase 0 — Spike del host nativo (gate) · M
1. Parche nativo mínimo: CLSID AoT, flag, rejit síncrono, sin startup hook ni PInvoke maps.
2. Host mínimo: carga directa del `.so`, `DllGetClassObject`, `Initialize` con `ICorProfilerInfo7+` emulado (stubs con el generador de #5736).
3. `IMetaData*` mínimo sobre dnlib (ruta CallTarget).
4. Definiciones con `RegisterCallTargetDefinitions3`: subconjunto de `CallTargetNativeTest` (`With0..9Arguments`, Generic, GenericStatic, Static, StaticStruct, tasks).
5. Oráculo: `CallTargetNativeTest` con profiler real + `DD_DUMP_ILREWRITE_ENABLED=1` frente al IL offline normalizado.
6. Ejecución del IL offline bajo JIT **sin profiler**, comparada con JIT + profiler.
7. **Salida:** IL idéntico en todo el subconjunto, ILVerify limpio y misma salida.
8. **Plan B si hay un bloqueo estructural:** tokens o firmas que no se pueden traducir, la ruta de rewrite exige estado de un runtime vivo, o diferencias de IL que la normalización no explica. El motivo se documenta.

### Fase 1 — Modo AoT en el nativo · M
1. CLSID AoT → `SetAotInstrumentation()`.
2. `Initialize` en AoT: sin exclusiones de proceso, checks de versión, debugger/DI/ER, heal, fault tolerant, telemetría nativa ni configuración estable de runtime.
3. Desactivar en AoT: startup hook (`RunILStartupHook`/`GenerateVoidILStartupMethod`) y loader, `RequestReJITWithInliners` y NGEN, colas en segundo plano (rejit síncrono), y PInvoke maps de `Datadog.Trace` (se mantiene `Ensure*Available`).
4. A2: los `return` que faltan.
5. A15: el modo normal se salta los módulos marcados.
6. Logs a un directorio que pasa el host.
7. Tests: GoogleTest de las ramas AoT, A2 y A15 (G8); integration tests para confirmar que el profiler normal no cambia.

### Fase 2 — Instrumentador offline · L
1. `Runner/Aot/` con el comando `dd-trace aot instrument` (proceso propio). A12: retirar `apply-aot`.
2. Interfaces COM y generador de stubs (#5736); código del host bajo `#if NET6_0_OR_GREATER`, porque el Runner también compila para netcoreapp2.1–5.0.
3. Carga nativa (`TracerHomeCache`, RID del host, musl, error claro).
4. A4: `ICorProfilerInfo` emulado.
5. A5: `IMetaData*` sobre dnlib:
   - tokens reales y falsos;
   - HCORENUM, `Find*`, `Get*Props`, `GetSigFromToken`;
   - `Define*` y `GetTokenFromSig`;
   - `SignatureReader`;
   - `GetILFunctionBody` con los bytes crudos por RVA.
6. A6: ciclo de vida (carga, JIT, ReJIT).
7. `SetILFunctionBody` → `MethodBodyReader` → `CilBody`; A8: sequence points por alineación.
8. A7: definiciones.
9. A9/A11: marcador y versión; escritura determinista.
10. A10: informe.
11. Tests: emulación frente a `System.Reflection.Metadata`; oráculo de IL sobre `CallTargetNativeTest` completo + HttpClient y Kafka de los samples.

### Fase 3 — Runtime CallTarget AOT · M
1. D1: contrato.
2. Interfaces internas de adapter: begin 0..8, lento, end, async (object/`TResult`/ValueTask) y runtime-async.
3. D2: `CallTargetInvoker.*Aot` con semántica idéntica.
4. D3: continuaciones `AotTask*/AotValueTask*` (de #8383) parametrizadas.
5. D4: gating cacheado por integración.
6. Tests G3: adapters escritos a mano para las integraciones NoOp de `CallTargetNativeTest` frente a `IntegrationMapper`.

### Fase 4 — Generador de adapters y post-proceso · L
1. B1 y B2 (port de `IntegrationMapper`).
2. B3: emisión dnlib en el contexto genérico (`!0`, `!!0`) + redirección.
3. B4 y B5.
4. Tests:
   - port de `GenerateProcessorTests`;
   - G4, con las sondas de `/tmp/pr8383-notes`: ref arg, target genérico, Task con solo `OnMethodEnd`, Task `null` y Task no genérico con `<TTarget, TReturn>`;
   - artefactos AOT bajo JIT frente al dinámico.

### Fase 5 — DuckType · M/L
1. C1 portado a la API in-process de la base.
2. C2: cierre G5 + análisis de los cuerpos de las integraciones.
3. C3: instanciaciones genéricas.
4. C4: G3.
5. C5.
6. C6: herramienta para grabar el catálogo con el recorder y formato del catálogo (la generación en CI va en F10).
7. Tests: suites DuckType + integraciones reales con duck.

### Fase 6 — Bootstrap y servicios · M
1. Inicializador del registro CallTarget: contrato → `DuckTypeAotRegistryBootstrap.Initialize()` explícito → `Instrumentation.InitializeAot()`. Aislado y sin stdout.
2. D6 (DiagnosticObserver, runtime metrics, telemetría, RCM, DSM, logs injection; respeta `DD_TRACE_ENABLED`), D7 y D10.
3. E1, E2 y E3.
4. D4 para aspectos CallSite.
5. Tests e2e NativeAOT: Kestrel minimal API, métricas, telemetría, AppSec básico y API manual.

### Fase 7 — Framework · M (riesgo alto)
1. Spike: ¿acepta ILC un `System.Net.Http.dll` reescrito como entrada (IL-only, sin R2R)?
2. Instrumentar las entradas de ILC con definiciones y sustituirlas. CoreLib queda excluido salvo IAST.
3. e2e: HttpClient + ASP.NET Core.

### Fase 8 — Código dinámico restante · L
1. D9.
2. Anexo B por prioridad.
3. `IsDynamicCodeSupported` + alternativa. Nunca un fallo duro.
4. Vendors.

### Fase 9 — MSBuild y `Datadog.Trace.Aot` · M
1. F1–F7.
2. Verificar los items de ILC por versión de SDK.
3. Tests: publish incremental doble, clean, multi-RID, multiproyecto, `build`/`run` sin cambios y app con la API manual (F4).

### Fase 10 — Paridad y CI · M
G2, G5, G6, G7 y la generación del catálogo C6 en CI; matriz de ILC 8/9/10 (y 11).

### Fase 11 — Limpieza · S
Retirar restos y actualizar las docs. GitHub queda en manos del usuario (D-8).

---

## 7. Notas de diseño

### 7.1 `Datadog.Trace.Aot`
- **Contenido:** `buildTransitive/Datadog.Trace.Aot.targets`; el instrumentador; los nativos del host; el `Datadog.Trace.dll` completo (net6.0, el que compila ILC); los nativos de runtime por RID de destino (libddwaf y libdatadog); el catálogo C6.
- **Activación:** `PackageReference` + `PublishAot=true`.
- **Durante el prototipo:** props generados y tool compilado del repo.

---

## 8. Trazabilidad: revisión de #8383 → cierre

| # | Problema | Cierre |
|---|---|---|
| 1 | Crash de generación con firmas normales | F4 |
| 2 | Target genérico | F0/F2 + F3/F4 |
| 3 | Doble instrumentación incremental | A9 + F2 de F9 (no toca `obj/`) |
| 4 | Crash sin coincidencias | F2 (no-op) + G7 |
| 5 | Bootstrap no aislado + stdout | F6 |
| 6 | `Task` no genérico con `<TTarget, TReturn>` | F4 |
| 7 | `Task<T>` con solo `OnMethodEnd` | F3/F4 |
| 8 | `ref TArg` | F4 |
| 9 | Task `null` | F3 |
| 10 | `ProcessDefinitions` ignorado | Desaparece + A10 |
| 11 | ADO.NET | Desaparece |
| 12 | Derived/Interface | Desaparece |
| 13 | Categorías y `DD_DISABLED_INTEGRATIONS` | A7 + D4 |
| 14 | Framework | F7 |
| 15 | Informe engañoso | A10 + B5 |
| 16 | CTAOT001–004 | Desaparece + F4 |
| 17 | `MethodInfo.Invoke`, hot path | F3 |
| 18 | Registro por nombre | F3/F4 |
| 19 | API pública | F3 |
| 20 | `Assembly.LoadFrom` | Desaparece |
| 21 | PDB | A8 |
| 22 | Ejemplos en `Datadog.Trace` | A7 |
| 23 | Rutas absolutas, `generate` manual; referencia de compilación a `Datadog.Trace` | F2/F4 de F9 |

## 9. Riesgos

| Riesgo | Mitigación |
|---|---|
| La emulación crece | Gate F0; rutas desactivadas en AoT; E_NOTIMPL con diagnóstico |
| Tokens mal traducidos | Oráculo G1 + ILVerify |
| El modo AoT afecta al profiler normal | Ramas solo AoT + GoogleTest + integration tests |
| Desajuste de versión | A11 + contrato |
| ILC rechaza el framework reescrito | Spike al inicio de F7 |
| Despacho estático distinto del dinámico | G3 |
| Mapeos DuckType incompletos para consumidores que no son CallTarget | C6 (catálogo) + C5 (degradación controlada) |
| Build lenta (IAST) | Opt-in + caché |
| Fallo en la build del cliente | D-11 |
| Disco local en el build nativo | Vigilar; limpiar worktrees de scratch |
| Tamaño del paquete | F7 (paquetes por RID) |

## 10. Ramas y PRs

- **#8252** (`fix/pr-8252-review-findings` → `codex/ducktyping-nativeaot`): solo DuckType AOT; se cierra por sí solo.
- **Prototipo**: rama local `nativeaot/tracer`, creada desde `fix/pr-8252-review-findings`, en el worktree `/home/tony/repos/DataDog/dd-trace-dotnet-worktrees/nativeaot-tracer`.
  - Se rebasa cuando cambie #8252.
  - Sin push hasta que se pida.
  - Los fixes de DuckType que salgan aquí van a #8252 (cherry-pick) si afectan a la base.
- **Troceo final** (cuando funcione F0–F6), en PRs apilados:
  1. nativo AoT (C++);
  2. runtime CallTarget AOT;
  3. instrumentador;
  4. DuckType para CallTarget + catálogo;
  5. bootstrap y servicios;
  6. `Datadog.Trace.Aot` (MSBuild);
  7. framework;
  8. código dinámico restante.

## 11. Decisiones abiertas

Ninguna bloqueante. Se decidirán sobre la marcha (y se apuntan en el tracker): nombre final de la rama, extracción del instrumentador fuera del Runner (F7 de F9) y paquetes por RID.

## 12. Registro de revisiones del plan

- **v1:** análisis inicial.
- **v2 (pasadas 1–4):**
  - categoría Rasp;
  - `Datadog.Trace` en solo lectura;
  - runtime-async y `SkipMethodBody`;
  - verificado que `CallTargetInvoker` funciona sin profiler;
  - TFM de las definiciones;
  - `return` que faltan en el nativo;
  - RID del host;
  - bloqueo de versión;
  - instrumentador fuera de proceso;
  - orden del bootstrap;
  - gating IAST;
  - telemetría;
  - determinismo;
  - política de error;
  - escenarios e2e;
  - grafo de fases;
  - estrategia de PRs.
- **v3 (revisión profunda previa a la implementación):**
  - decisiones cerradas (sección 1);
  - `Datadog.Trace.Aot`, libre en nuget.org;
  - **referencia de compilación a `Datadog.Trace` incompatible con la API manual** (F4);
  - **hook solo en publish y sin tocar `obj/`** (D-10, F2);
  - **el profiler normal se salta los módulos marcados** (A15, G8);
  - **catálogo de mapeos DuckType grabado en CI** (C6): la discovery por atributos solo cubre 6 usos y el DiagnosticObserver, Activity y AppSec también necesitan mapeos;
  - el nativo no entrega mapa de IL, así que los sequence points se transfieren por alineación (A8);
  - fault tolerant desactivado en AoT;
  - el nativo pide `ICorProfilerInfo7` (obligatorio), 8, 10 y 12;
  - CallTarget no necesita interruptor de modo gracias al despacho estático;
  - proxies directos o búsqueda en runtime según el target esté cerrado o sea genérico abierto;
  - carga directa del nativo sin native loader;
  - prerrequisitos (build nativo local, disco);
  - diseño del paquete (F7 de F9);
  - verificar los items de ILC por versión de SDK.

---

## Anexo A — API nativa usada por `Datadog.Tracer.Native`

- **IMetaDataImport**: EnumCustomAttributes, EnumFieldsWithName, EnumInterfaceImpls, EnumMemberRefs, EnumMethods, EnumMethodsWithName, EnumModuleRefs, EnumProperties, EnumTypeDefs, EnumTypeRefs, EnumTypeSpecs, FindMemberRef, FindMethod, FindTypeDefByName, FindTypeRef, GetCustomAttributeByName, GetCustomAttributeProps, GetFieldProps, GetInterfaceImplProps, GetMemberProps, GetMemberRefProps, GetMethodProps, GetModuleFromScope, GetModuleRefProps, GetNestedClassProps, GetPinvokeMap, GetPropertyProps, GetScopeProps, GetSigFromToken, GetTypeDefProps, GetTypeRefProps, GetTypeSpecFromToken, GetUserString
- **IMetaDataImport2**: EnumGenericParamConstraints, EnumGenericParams, GetGenericParamConstraintProps, GetGenericParamProps, GetMethodSpecProps
- **IMetaDataEmit**: DefineCustomAttribute, DefineField, DefineImportMember, DefineMemberRef, DefineMethod, DefineModuleRef, DefinePinvokeMap, DefineTypeDef, DefineTypeRefByName, DefineUserString, DeletePinvokeMap, GetTokenFromSig, GetTokenFromTypeSpec, SetMethodImplFlags
- **IMetaDataEmit2**: DefineGenericParam, DefineMethodSpec
- **IMetaDataAssemblyImport**: EnumAssemblyRefs, FindExportedTypeByName, GetAssemblyFromScope, GetAssemblyProps, GetAssemblyRefProps, GetExportedTypeProps
- **IMetaDataAssemblyEmit**: DefineAssemblyRef
- **ICorProfilerInfo\*** (QI a 7 obligatorio; 8, 10 y 12 opcionales): GetModuleInfo/GetModuleInfo2, GetModuleMetaData, GetAssemblyInfo, GetAppDomainInfo, GetFunctionInfo, GetFunctionFromToken, GetClassFromToken, GetTokenAndMetaDataFromFunction, GetILFunctionBody, GetILFunctionBodyAllocator, SetILFunctionBody, SetILInstrumentedCodeMap, GetEventMask/SetEventMask/SetEventMask2, GetRuntimeInformation, InitializeCurrentThread, EnumModules, RequestReJIT, RequestRevert, ApplyMetaData, GetThreadAppDomain. **Se espera que no se usen en AoT (E_NOTIMPL)**: RequestReJITWithInliners, EnumNgenModuleMethodsInliningThisMethod, inproc debugging, GetCodeInfo, GetILToNativeMapping, GetObjectSize, ForceGC, GetThreadContext/GetThreadInfo, GetFunctionFromIP, SetEnterLeaveFunctionHooks, SetFunctionReJIT, IsArrayClass, GetClassFromObject, GetHandleFromThread.

## Anexo B — Código dinámico en runtime fuera de DuckType/CallTarget (Fase 8)

- **Activity/OTel**: `Activity/ActivityListener.cs`, `ActivityListenerDelegatesBuilder.cs`, `DiagnosticObserverListener.cs`, `DiagnosticSourceEventListener.cs`, `Helpers/AllocationFreeEnumerator.cs`, `OpenTelemetry/Sdk.cs`, `DiagnosticListeners/DiagnosticManager.cs`.
- **AppSec**: `AppSec/ObjectExtractor.cs`, `AppSec/Coordinator/SecurityCoordinator.Framework.cs` (solo netfx).
- **Utilidades**: `Util/ActivatorHelper.cs`, `Util/Delegates/DelegateInstrumentation.cs`, `RuntimeMetrics/MeterObservableUpDownCounterReflection.cs`, `CallTarget/Handlers/Continuations/ValueTaskActivator*.cs`.
- **Integraciones**: AWS SNS/SQS `CachedMessageHeadersHelper`, Azure Functions isolated (`NullableStringHelper`, `TypedDataHelper`), gRPC legacy `CachedMetadataHelper`, Kafka (`CachedMessageHeadersHelper`, `KafkaHelper`, `KafkaProduceSyncDeliveryHandlerIntegration`, `ProducerCache`), logging (ILogger, NLog, Serilog), MongoDB `BsonSerializationHelper`, OpenTelemetry (`ResourceAttributeProcessorHelper`, `TracerProviderBuilderIntegration`), RabbitMQ `CachedBasicPropertiesHelper`. Testing/Selenium/MsTest: fuera de alcance.
- **Debugger**: fuera de alcance.
- **Vendors**: `Vendors/MessagePack/*` (resolvers dinámicos) y `Vendors/Newtonsoft.Json/*`, por verificar.

## Anexo C — Material de la revisión de #8383

`/tmp/pr8383-notes/` (efímero; convertir en tests en F4): sondas, diff del test de publish, salidas NativeAOT (primer publish e incremental), superficie de API nativa y `DumpIl.cs`. Refs locales fijados: `refs/remotes/pr/5736` (`b30ec21553`) y `refs/remotes/pr/8383` (`218d0a892d`).
