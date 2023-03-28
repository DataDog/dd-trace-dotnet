// <copyright file="Instrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.AppSec;
using Datadog.Trace.Ci;
using Datadog.Trace.ClrProfiler.ServerlessInstrumentation;
using Datadog.Trace.Configuration;
using Datadog.Trace.Debugger;
using Datadog.Trace.Debugger.Helpers;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Logging;
using Datadog.Trace.Processors;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Transport;
using Datadog.Trace.ServiceFabric;

namespace Datadog.Trace.ClrProfiler
{
    /// <summary>
    /// Provides access to the profiler CLSID and whether it is attached to the process.
    /// </summary>
    [Browsable(false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class Instrumentation
    {
        /// <summary>
        /// Indicates whether we're initializing Instrumentation for the first time
        /// </summary>
        private static int _firstInitialization = 1;
        private static int _firstNonNativePartsInitialization = 1;

        /// <summary>
        /// Gets the CLSID for the Datadog .NET profiler
        /// </summary>
        public static readonly string ProfilerClsid = "{846F5F1C-F9AE-4B07-969E-05C26BC060D8}";

        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Instrumentation));

        /// <summary>
        /// Gets a value indicating whether Datadog's profiler is attached to the current process.
        /// </summary>
        /// <value>
        ///   <c>true</c> if the profiler is currently attached; <c>false</c> otherwise.
        /// </value>
        public static bool ProfilerAttached
        {
            get
            {
                try
                {
                    return NativeMethods.IsProfilerAttached();
                }
                catch (DllNotFoundException)
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Gets a value indicating the version of the native Datadog profiler. This method
        /// is rewritten by the profiler.
        /// </summary>
        /// <returns>In a managed-only context, where the profiler is not attached, <c>None</c>,
        /// otherwise the version of the Datadog native tracer library.</returns>
        public static string GetNativeTracerVersion() => "None";

        /// <summary>
        /// Initializes global instrumentation values.
        /// </summary>
        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                // Initialize() was already called before
                return;
            }

            TracerDebugger.WaitForDebugger();

            Log.Debug("Initialization started.");

            try
            {
                Log.Debug("Enabling by ref instrumentation.");
                NativeMethods.EnableByRefInstrumentation();
                Log.Information("ByRef instrumentation enabled.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "ByRef instrumentation cannot be enabled: ");
            }

            try
            {
                Log.Debug("Enabling calltarget state by ref.");
                NativeMethods.EnableCallTargetStateByRef();
                Log.Information("CallTarget State ByRef enabled.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "CallTarget state ByRef cannot be enabled: ");
            }

            try
            {
                Log.Debug("Initializing TraceAttribute instrumentation.");
                var payload = InstrumentationDefinitions.GetTraceAttributeDefinitions();
                NativeMethods.AddTraceAttributeInstrumentation(payload.DefinitionsId, payload.AssemblyName, payload.TypeName);
                Log.Information("TraceAttribute instrumentation enabled with Assembly={AssemblyName} and Type={TypeName}.", payload.AssemblyName, payload.TypeName);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing TraceAttribute instrumentation");
            }

            InitializeNoNativeParts();
            var tracer = Tracer.Instance;

            try
            {
                Log.Debug("Sending CallTarget integration definitions to native library.");
                var payload = InstrumentationDefinitions.GetAllDefinitions();
                NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);

                Log.Information<int>("The profiler has been initialized with {Count} definitions.", payload.Definitions.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending CallTarget integration definitions to native library");
            }

            try
            {
                Serverless.InitIfNeeded();
            }
            catch (Exception ex)
            {
                Serverless.Error("Error while loading Serverless definitions", ex);
            }

            try
            {
                Log.Debug("Sending CallTarget derived integration definitions to native library.");
                var payload = InstrumentationDefinitions.GetDerivedDefinitions();
                NativeMethods.AddDerivedInstrumentations(payload.DefinitionsId, payload.Definitions);

                Log.Information<int>("The profiler has been initialized with {Count} derived definitions.", payload.Definitions.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending CallTarget derived integration definitions to native library");
            }

            try
            {
                Log.Debug("Sending CallTarget interface integration definitions to native library.");
                var payload = InstrumentationDefinitions.GetInterfaceDefinitions();
                NativeMethods.AddInterfaceInstrumentations(payload.DefinitionsId, payload.Definitions);

                Log.Information<int>("The profiler has been initialized with {Count} interface definitions.", payload.Definitions.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending CallTarget interface integration definitions to native library");
            }

            if (tracer is null)
            {
                Log.Debug("Skipping TraceMethods initialization because Tracer.Instance was null after InitializeNoNativeParts was invoked");
            }
            else
            {
                try
                {
                    InitRemoteConfigurationManagement(tracer);
                }
                catch (Exception e)
                {
                    Log.Error(e, "Failed to initialize Remote Configuration Management.");
                }

                try
                {
                    Log.Debug("Initializing TraceMethods instrumentation.");
                    var traceMethodsConfiguration = tracer.Settings.TraceMethods;
                    var payload = InstrumentationDefinitions.GetTraceMethodDefinitions();
                    NativeMethods.InitializeTraceMethods(payload.DefinitionsId, payload.AssemblyName, payload.TypeName, traceMethodsConfiguration);
                    Log.Information("TraceMethods instrumentation enabled with Assembly={AssemblyName}, Type={TypeName}, and Configuration={Configuration}.", payload.AssemblyName, payload.TypeName, traceMethodsConfiguration);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error initializing TraceMethods instrumentation");
                }
            }

            if (!Iast.Iast.Instance.Settings.Enabled)
            {
                Log.Debug("Skipping Iast initialization because Iast is disabled");
            }
            else
            {
                try
                {
                    int defs = 0, derived = 0;
                    Log.Debug("Adding CallTarget IAST integration definitions to native library.");
                    var payload = InstrumentationDefinitions.GetAllDefinitions(InstrumentationCategory.Iast);
                    NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);
                    defs = payload.Definitions.Length;

                    Log.Debug("Adding CallTarget IAST derived integration definitions to native library.");
                    payload = InstrumentationDefinitions.GetDerivedDefinitions(InstrumentationCategory.Iast);
                    NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);
                    derived = payload.Definitions.Length;

                    Log.Information<int, int>("{Defs} IAST definitions and {Derived} IAST derived definitions added to the profiler.", defs, derived);

                    Log.Debug("Registering IAST Callsite Dataflow Aspects into native library.");
                    var aspects = NativeMethods.RegisterIastAspects(AspectDefinitions.Aspects);
                    Log.Information<int>("{Aspects} IAST Callsite Dataflow Aspects added to the profiler.", aspects);
                }
                catch (Exception ex)
                {
                    Iast.Iast.Instance.Settings.Enabled = false;
                    Log.Error(ex, "DDIAST-0001-01: IAST could not start because of an unexpected error. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help.");
                }
            }

#if NETSTANDARD2_0 || NETCOREAPP3_1
            try
            {
                // On .NET Core 2.0-3.0 we see an occasional hang caused by OpenSSL being loaded
                // while the app is shutting down, which results in flaky tests due to the short-
                // lived nature of our apps. This appears to be a bug in the runtime (although
                // we haven't yet confirmed that). Calling the `ToUuid()` method uses an MD5
                // hash which calls into the native library, triggering the load.
                _ = string.Empty.ToUUID();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error triggering eager OpenSSL load");
            }
#endif
            LifetimeManager.Instance.AddShutdownTask(RunShutdown);

            Log.Debug("Initialization finished.");
        }

        private static void RunShutdown()
        {
            InstrumentationDefinitions.Dispose();
        }

        internal static void InitializeNoNativeParts()
        {
            if (Interlocked.Exchange(ref _firstNonNativePartsInitialization, 0) != 1)
            {
                // InitializeNoNativeParts() was already called before
                return;
            }

            TracerDebugger.WaitForDebugger();
            Log.Debug("Initialization of non native parts started.");

            try
            {
                var asm = typeof(Instrumentation).Assembly;
                // intentionally using string interpolation, as this is only called once, and avoids array allocation
#pragma warning disable DDLOG004 // Message templates should be constant
#if NET5_0_OR_GREATER
                // Can't use asm.CodeBase or asm.GlobalAssemblyCache in .NET 5+
                Log.Information($"[Assembly metadata] Location: {asm.Location}, HostContext: {asm.HostContext}, SecurityRuleSet: {asm.SecurityRuleSet}");
#else
                Log.Information($"[Assembly metadata] Location: {asm.Location}, CodeBase: {asm.CodeBase}, GAC: {asm.GlobalAssemblyCache}, HostContext: {asm.HostContext}, SecurityRuleSet: {asm.SecurityRuleSet}");
#endif
#pragma warning restore DDLOG004 // Message templates should be constant
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error printing assembly metadata");
            }

            try
            {
                // ensure global instance is created if it's not already
                if (CIVisibility.Enabled)
                {
                    CIVisibility.Initialize();
                }
                else
                {
                    Log.Debug("Initializing tracer singleton instance.");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing CIVisibility");
            }

            try
            {
                Log.Debug("Initializing security singleton instance.");
                _ = Security.Instance;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing Security");
            }

#if !NETFRAMEWORK
            try
            {
                if (GlobalSettings.Instance.DiagnosticSourceEnabled)
                {
                    // check if DiagnosticSource is available before trying to use it
                    var type = Type.GetType("System.Diagnostics.DiagnosticSource, System.Diagnostics.DiagnosticSource", throwOnError: false);

                    if (type == null)
                    {
                        Log.Warning("DiagnosticSource type could not be loaded. Skipping diagnostic observers.");
                    }
                    else
                    {
                        // don't call this method unless DiagnosticSource is available
                        StartDiagnosticManager();
                    }
                }
            }
            catch
            {
                // ignore
            }

            // we only support Service Fabric Service Remoting instrumentation on .NET Core (including .NET 5+)
            if (FrameworkDescription.Instance.IsCoreClr())
            {
                Log.Information("Initializing ServiceFabric instrumentation");

                try
                {
                    ServiceRemotingClient.StartTracing();
                }
                catch
                {
                    // ignore
                }

                try
                {
                    ServiceRemotingService.StartTracing();
                }
                catch
                {
                    // ignore
                }
            }
#endif

            try
            {
                if (Tracer.Instance.Settings.IsActivityListenerEnabled)
                {
                    Log.Debug("Initializing activity listener.");
                    Activity.ActivityListener.Initialize();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error initializing activity listener");
            }

            Log.Debug("Initialization of non native parts finished.");
        }

#if !NETFRAMEWORK
        private static void StartDiagnosticManager()
        {
            var observers = new List<DiagnosticObserver>();

            if (Tracer.Instance.Settings.AzureAppServiceMetadata?.IsFunctionsApp is not true)
            {
                // Not adding the `AspNetCoreDiagnosticObserver` is particularly important for Azure Functions.
                // The AspNetCoreDiagnosticObserver will be loaded in a separate Assembly Load Context, breaking the connection of AsyncLocal
                // This is because user code is loaded within the functions host in a separate context
                observers.Add(new AspNetCoreDiagnosticObserver());
            }

            var diagnosticManager = new DiagnosticManager(observers);
            diagnosticManager.Start();
            DiagnosticManager.Instance = diagnosticManager;
        }
#endif

        private static void InitRemoteConfigurationManagement(Tracer tracer)
        {
            // Service Name must be lowercase, otherwise the agent will not be able to find the service
            var serviceName = TraceUtil.NormalizeTag(tracer.Settings.ServiceName ?? tracer.DefaultServiceName);
            var discoveryService = tracer.TracerManager.DiscoveryService;

            Task.Run(
                async () =>
                {
                    // TODO: RCM and LiveDebugger should be initialized in TracerManagerFactory so they can respond
                    // to changes in ExporterSettings etc.
                    var isDiscoverySuccessful = await WaitForDiscoveryService(discoveryService).ConfigureAwait(false);
                    if (isDiscoverySuccessful)
                    {
                        var rcmSettings = RemoteConfigurationSettings.FromDefaultSource();
                        var rcmApi = RemoteConfigurationApiFactory.Create(tracer.Settings.Exporter, rcmSettings, discoveryService);

                        var configurationManager = RemoteConfigurationManager.Create(discoveryService, rcmApi, rcmSettings, serviceName, tracer.Settings, tracer.TracerManager.GitMetadataTagsProvider);
                        // see comment above
                        configurationManager.RegisterProduct(AsmRemoteConfigurationProducts.AsmFeaturesProduct);
                        configurationManager.RegisterProduct(AsmRemoteConfigurationProducts.AsmDataProduct);
                        configurationManager.RegisterProduct(AsmRemoteConfigurationProducts.AsmDDProduct); // This should be activated by Security only when Asm is active, but there is no visibility right now
                        configurationManager.RegisterProduct(AsmRemoteConfigurationProducts.AsmProduct);

                        var liveDebugger = LiveDebuggerFactory.Create(discoveryService, configurationManager, tracer.Settings, serviceName);

                        Log.Debug("Initializing Remote Configuration management.");

                        await Task
                             .WhenAll(
                                  InitializeRemoteConfigurationManager(configurationManager),
                                  InitializeLiveDebugger(liveDebugger))
                             .ConfigureAwait(false);
                    }
                });
        }

        private static async Task<bool> WaitForDiscoveryService(IDiscoveryService discoveryService)
        {
            var tc = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Stop waiting if we're shutting down
            LifetimeManager.Instance.AddShutdownTask(() => tc.TrySetResult(false));

            discoveryService.SubscribeToChanges(Callback);
            return await tc.Task.ConfigureAwait(false);

            void Callback(AgentConfiguration x)
            {
                tc.TrySetResult(true);
                discoveryService.RemoveSubscription(Callback);
            }
        }

        internal static async Task InitializeRemoteConfigurationManager(IRemoteConfigurationManager remoteConfigurationManager)
        {
            try
            {
                await remoteConfigurationManager.StartPollingAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Remote Configuration management.");
            }
        }

        internal static async Task InitializeLiveDebugger(LiveDebugger liveDebugger)
        {
            try
            {
                await liveDebugger.InitializeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Live Debugger");
            }
        }
    }
}
