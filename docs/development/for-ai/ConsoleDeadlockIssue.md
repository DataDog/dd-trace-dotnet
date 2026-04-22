# Console App Deadlock with ConfigurationManager Config Builders (APMS-19239)

## ROOT CAUSE CONFIRMED (from customer memory dump, 2026-04-22)

The customer provided a memory dump of the hung process (`C:\Temp\APMS-19239\Dump\ConsoleApp1.exe_260421_142451.dmp`).
Analysis with `dotnet-dump analyze` identified the **actual** deadlock, which is different from the
`ConfigurationManager` lock theory below.

**The real deadlock is a classic CLR type-initializer (`.cctor`) deadlock between the Managed Loader's
`Startup` class and the `AppDomain.AssemblyResolve` handler it registers.**

### Thread A (main thread — `0x4a78`)

The native profiler's module-initializer IL runs at the top of `Program.Main()` and calls
`Assembly.CreateInstance("Datadog.Trace.ClrProfiler.Managed.Loader.Startup")`, which triggers the
`Startup..cctor()`. Inside the `.cctor`:

```
Startup..cctor()
  TryInvokeManagedMethod("Datadog.Trace.ClrProfiler.Instrumentation", "Initialize", ...)
    Activator.CreateInstance(InstrumentationLoader)
      Instrumentation..cctor()
        DatadogLogging.GetLoggerFor(...)
          DatadogLogging..cctor()
            GlobalSettings.get_Instance()
              GlobalSettings..cctor()
                GlobalSettings.CreateFromDefaultSources()
                  GlobalConfigurationSource.get_CreationResult()
                    GlobalConfigurationSource..cctor()
                      CreateDefaultConfigurationSource(...)
                        ConfigurationManager.get_AppSettings()      ← triggers configBuilder
                          KeyValueConfigBuilder.ProcessConfigurationSection
                            KeyValueConfigBuilder.EnsureGreedyInitialized
                              AzureAppConfigurationBuilder.GetAllValues
                                Task<...>.GetResultCore(true)        ← sync-over-async
                                  Task.InternalWait(...)
                                    Task.SpinThenBlockingWait(...)
                                      ManualResetEventSlim.Wait(...)  ← BLOCKED
```

**State**: main thread holds the type-init lock for `Startup` (and every type in the chain above,
most importantly `Startup` itself, because that's what was just instantiated). It is blocking on a
`Task` spawned by `AzureAppConfigurationBuilder`.

### Thread B (ThreadPool thread — `0x2bd4`)

Running the async continuation of `AzureAppConfigurationBuilder.GetAllValuesAsync` while it enumerates
the Azure App Config response and lazily builds a `SecretClient` for each Key Vault reference:

```
ThreadPoolWorkQueue.Dispatch
  ... Azure.Core pipeline (HttpPipeline, RetryPolicy, BearerTokenAuthenticationPolicy, ...) ...
    AzureAppConfigurationBuilder+<GetAllValuesAsync>d__39.MoveNext
      AzureAppConfigurationBuilder.GetKeyVaultValue
        AzureAppConfigurationBuilder+<>c__DisplayClass41_0.<GetSecretClient>b__0(Uri)
          new DefaultAzureCredential()
            DefaultAzureCredentialFactory.CreateFullDefaultCredentialChain()
              DefaultAzureCredentialFactory.CreateVisualStudioCodeCredential()
                new VisualStudioCodeCredential(...)
                  CredentialOptionsMapper.GetBrokerOptions(...)
                    DefaultAzureCredentialFactory.TryCreateDevelopmentBrokerOptions(...)
                      Type.GetType("Microsoft.Identity.Client.Broker.PublicClientApplicationBuilderExtensions, ...")
                        RuntimeTypeHandle.GetTypeByName (via P/Invoke)
                          AppDomain.OnAssemblyResolveEvent
                            Startup.AssemblyResolve_ManagedProfilerDependencies ← static method on Startup
                              [HelperMethodFrame]                                 ← BLOCKED waiting for
                                                                                    Startup..cctor to finish
```

**State**: threadpool thread is blocked waiting for `Startup`'s class initializer to complete (the CLR
requires the type to be initialized before its static methods can execute). But `Startup..cctor` is
running on Thread A.

### The Deadlock

- Thread A holds `Startup`'s type-init lock and waits for a `Task`.
- Thread B is running that `Task`; it needs to invoke a static method on `Startup` to resolve an
  assembly, which requires `Startup`'s type-init lock.
- Neither thread can make progress. **Classic `.cctor` × sync-over-async deadlock.**

This is why every earlier mitigation failed:

- `IsLoadingConfigurationManagerAppSettings` guard — wrong mechanism. The deadlock is not about
  re-entrancy into CallTarget, it is purely a `.cctor` + `AssemblyResolve` interaction.
- `Lazy<IDatadogLogger>` in `IntegrationOptions` / `IntegrationMapper` — wrong type chain. The
  `.cctor` chain that matters is `Startup → Instrumentation → DatadogLogging → GlobalSettings →
  GlobalConfigurationSource → ConfigurationManager.AppSettings`, and the blocker is on `Startup`
  itself because that's where the `AssemblyResolve` handler is a static member.
- Skipping `ConfigurationManager.AppSettings` during static init — was on the right track but
  incomplete: even if we don't call it directly, any sync-over-async work inside the `Startup.cctor`
  chain that ends up resolving an assembly will deadlock.

### Fix Direction

Two orthogonal root-cause fixes, either of which would break the deadlock. Doing both is safer.

1. **Move `AssemblyResolve_ManagedProfilerDependencies` off of `Startup`.** Put the handler on a
   separate type (e.g. `ManagedProfilerAssemblyResolver`) that has no `.cctor` dependency on any
   long-running initialization. Register it from `Startup..cctor` via a delegate to that other type's
   static method. Then Thread B can invoke the handler without needing `Startup` to be fully
   initialized.

2. **Stop reading `ConfigurationManager.AppSettings` from inside `Startup..cctor`'s transitive chain
   on .NET Framework.** The customer's repro needs it gone from the `GlobalConfigurationSource`
   static-init path; a deferred read (on first actual use, not during class init) is enough.

Fix #1 is the more general protection — it defends against *any* sync-over-async inside the
`.cctor` chain, not just the `ConfigurationManager.AppSettings` case. Fix #2 is a pragmatic
narrowing: `ConfigurationManager.AppSettings` is the specific trigger for this customer.
