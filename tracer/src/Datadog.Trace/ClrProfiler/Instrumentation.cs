// <copyright file="Instrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;

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

        private static bool legacyMode = false;

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

            var swTotal = Stopwatch.StartNew();
            Log.Debug("Initialization started.");

            var sw = Stopwatch.StartNew();
            legacyMode = GetNativeTracerVersion() != TracerConstants.ThreePartVersion;
            if (legacyMode)
            {
                InitializeLegacy();
            }
            else
            {
                InitializeNoNativeParts(sw);

                try
                {
                    Log.Debug("Enabling CallTarget integration definitions in native library.");

                    InstrumentationCategory enabledCategories = InstrumentationCategory.Tracing;
                    if (Security.Instance.Enabled)
                    {
                        Log.Debug("Enabling AppSec call target category");
                        enabledCategories |= InstrumentationCategory.AppSec;
                    }

                    var defs = NativeMethods.RegisterCallTargetDefinitions("Tracing", InstrumentationDefinitions.Instrumentations, (uint)enabledCategories);
                    Log.Information<int>("The profiler has been initialized with {Count} definitions.", defs);
                    TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.CallTarget, defs);

                    if (Iast.Iast.Instance.Settings.Enabled)
                    {
                        Log.Debug("Enabling Iast call target category");
                        EnableTracerInstrumentations(InstrumentationCategory.Iast);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error sending CallTarget integration definitions to native library");
                }

                TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.CallTargetDefsPinvoke, sw.ElapsedMilliseconds);
                sw.Restart();

                InitializeTracer(sw);

                InitializeServerless(sw);
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

            TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.Total, swTotal.ElapsedMilliseconds);
        }

        /// <summary>
        /// Initializes global instrumentation values.
        /// </summary>
        public static void InitializeLegacy()
        {
            var sw = Stopwatch.StartNew();
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

            TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.ByRefPinvoke, sw.ElapsedMilliseconds);
            sw.Restart();

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

            TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.CallTargetStateByRefPinvoke, sw.ElapsedMilliseconds);
            sw.Restart();

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

            TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.TraceAttributesPinvoke, sw.ElapsedMilliseconds);
            sw.Restart();

            InitializeNoNativeParts(sw);

            InitializeInstrumentationsLegacy(InstrumentationCategory.Tracing, sw);

            InitializeTracer(sw);

            InitializeServerless(sw);

            InitializeAppSecLegacy(sw);

            InitializeIastLegacy(sw);

            Log.Debug("Legacy Initialization finished.");
        }

        private static void RunShutdown()
        {
            InstrumentationDefinitions.Dispose();
            NativeCallTargetUnmanagedMemoryHelper.Free();
        }

        internal static void InitializeNoNativeParts(Stopwatch sw = null)
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

            var tracer = Tracer.Instance;
            if (tracer is null)
            {
                Log.Debug("Tracer.Instance is null after InitializeNoNativeParts was invoked");
            }

            if (sw != null)
            {
                TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.Managed, sw.ElapsedMilliseconds);
                sw.Restart();
            }
        }

        private static void InitializeTracer(Stopwatch sw)
        {
            var tracer = Tracer.Instance;
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

                // RCM isn't _actually_ initialized at this point, as we do it in the background, so we record that separately
                sw.Restart();

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

                TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.TraceMethodsPinvoke, sw.ElapsedMilliseconds);
                sw.Restart();
            }
        }

        private static void InitializeServerless(Stopwatch sw)
        {
            try
            {
                Serverless.InitIfNeeded();
            }
            catch (Exception ex)
            {
                Serverless.Error("Error while loading Serverless definitions", ex);
            }

            if (sw != null)
            {
                TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.Serverless, sw.ElapsedMilliseconds);
                sw.Restart();
            }
        }

        private static void InitializeAppSecLegacy(Stopwatch sw)
        {
            if (!legacyMode) { return; }

            if (!Security.Instance.Settings.Enabled)
            {
                Log.Debug("Skipping AppSec initialization because AppSec is disabled");
            }
            else
            {
                InitializeInstrumentationsLegacy(InstrumentationCategory.AppSec, sw);
            }
        }

        private static void InitializeIastLegacy(Stopwatch sw)
        {
            if (!legacyMode) { return; }

            if (!Iast.Iast.Instance.Settings.Enabled)
            {
                Log.Debug("Skipping Iast initialization because Iast is disabled");
            }
            else
            {
                InitializeInstrumentationsLegacy(InstrumentationCategory.Iast, sw);
            }
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
            var serviceName = TraceUtil.NormalizeTag(tracer.Settings.ServiceNameInternal ?? tracer.DefaultServiceName);
            var discoveryService = tracer.TracerManager.DiscoveryService;

            Task.Run(
                async () =>
                {
                    // TODO: LiveDebugger should be initialized in TracerManagerFactory so it can respond
                    // to changes in ExporterSettings etc.

                    var sw = Stopwatch.StartNew();
                    var isDiscoverySuccessful = await WaitForDiscoveryService(discoveryService).ConfigureAwait(false);
                    TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.DiscoveryService, sw.ElapsedMilliseconds);

                    if (isDiscoverySuccessful)
                    {
                        var liveDebugger = LiveDebuggerFactory.Create(discoveryService, RcmSubscriptionManager.Instance, tracer.Settings, serviceName, tracer.TracerManager.Telemetry);

                        Log.Debug("Initializing live debugger.");

                        await InitializeLiveDebugger(liveDebugger).ConfigureAwait(false);
                    }
                });
        }

        // /!\ This method is called by reflection in the SampleHelpers
        // If you remove it then you need to provide an alternative way to wait for the discovery service
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

        internal static async Task InitializeLiveDebugger(LiveDebugger liveDebugger)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                await liveDebugger.InitializeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize Live Debugger");
            }

            TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.DynamicInstrumentation, sw.ElapsedMilliseconds);
        }

        internal static void EnableTracerInstrumentations(InstrumentationCategory categories, Stopwatch sw = null)
        {
            if (legacyMode)
            {
                InitializeInstrumentationsLegacy(categories, sw);
            }
            else
            {
                var defs = NativeMethods.EnableCallTargetDefinitions((uint)categories);
                TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.CallTarget, defs);

                if (categories.HasFlag(InstrumentationCategory.Iast))
                {
                    Log.Debug("Registering IAST Callsite Dataflow Aspects into native library.");
                    var aspects = NativeMethods.RegisterIastAspects(AspectDefinitions.Aspects);
                    Log.Information<int>("{Aspects} IAST Callsite Dataflow Aspects added to the profiler.", aspects);
                    TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.IastAspects, aspects);

                    if (sw != null)
                    {
                        TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.Iast, sw.ElapsedMilliseconds);
                        sw.Restart();
                    }
                }
            }
        }

        internal static void DisableTracerInstrumentations(InstrumentationCategory categories, Stopwatch sw = null)
        {
            if (legacyMode)
            {
                RemoveTracerInstrumentationsLegacy(categories);
            }
            else
            {
                NativeMethods.DisableCallTargetDefinitions((uint)categories);
            }
        }

        private static void InitializeInstrumentationsLegacy(InstrumentationCategory categories, Stopwatch sw = null)
        {
            if (categories.HasFlag(InstrumentationCategory.Tracing))
            {
                try
                {
                    Log.Debug("Sending CallTarget integration definitions to native library.");
                    var payload = InstrumentationDefinitions.GetAllDefinitions();
                    NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);
                    Log.Information<int>("The profiler has been initialized with {Count} definitions.", payload.Definitions.Length);
                    TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.CallTarget, payload.Definitions.Length);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error sending CallTarget integration definitions to native library");
                }

                if (sw != null)
                {
                    TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.CallTargetDefsPinvoke, sw.ElapsedMilliseconds);
                    sw.Restart();
                }

                try
                {
                    Log.Debug("Sending CallTarget derived integration definitions to native library.");
                    var payload = InstrumentationDefinitions.GetDerivedDefinitions();
                    NativeMethods.AddDerivedInstrumentations(payload.DefinitionsId, payload.Definitions);
                    Log.Information<int>("The profiler has been initialized with {Count} derived definitions.", payload.Definitions.Length);
                    TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.CallTargetDerived, payload.Definitions.Length);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error sending CallTarget derived integration definitions to native library");
                }

                if (sw != null)
                {
                    TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.CallTargetDerivedDefsPinvoke, sw.ElapsedMilliseconds);
                    sw.Restart();
                }

                try
                {
                    Log.Debug("Sending CallTarget interface integration definitions to native library.");
                    var payload = InstrumentationDefinitions.GetInterfaceDefinitions();
                    NativeMethods.AddInterfaceInstrumentations(payload.DefinitionsId, payload.Definitions);
                    Log.Information<int>("The profiler has been initialized with {Count} interface definitions.", payload.Definitions.Length);
                    TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.CallTargetInterfaces, payload.Definitions.Length);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error sending CallTarget interface integration definitions to native library");
                }

                if (sw != null)
                {
                    TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.CallTargetInterfaceDefsPinvoke, sw.ElapsedMilliseconds);
                    sw.Restart();
                }
            }

            if (categories.HasFlag(InstrumentationCategory.AppSec))
            {
                int defs = 0, derived = 0;
                try
                {
                    Log.Debug("Adding CallTarget AppSec integration definitions to native library.");
                    var payload = InstrumentationDefinitions.GetAllDefinitions(InstrumentationCategory.AppSec);
                    NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);
                    defs = payload.Definitions.Length;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error adding CallTarget AppSec integration definitions to native library");
                }

                try
                {
                    Log.Debug("Adding CallTarget appsec derived integration definitions to native library.");
                    var payload = InstrumentationDefinitions.GetDerivedDefinitions(InstrumentationCategory.AppSec);
                    NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);
                    derived = payload.Definitions.Length;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error adding CallTarget appsec derived integration definitions to native library");
                }

                Log.Information<int, int>("{DefinitionCount} AppSec definitions and {DerivedCount} AppSec derived definitions added to the profiler.", defs, derived);
            }

            if (categories.HasFlag(InstrumentationCategory.Iast))
            {
                try
                {
                    int defs = 0, derived = 0;
                    Log.Debug("Adding CallTarget IAST integration definitions to native library.");
                    var payload = InstrumentationDefinitions.GetAllDefinitions(InstrumentationCategory.Iast);
                    NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);
                    defs = payload.Definitions.Length;
                    TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.Iast, defs);

                    Log.Debug("Adding CallTarget IAST derived integration definitions to native library.");
                    payload = InstrumentationDefinitions.GetDerivedDefinitions(InstrumentationCategory.Iast);
                    NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);
                    derived = payload.Definitions.Length;
                    TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.IastDerived, derived);

                    Log.Information<int, int>("{Defs} IAST definitions and {Derived} IAST derived definitions added to the profiler.", defs, derived);

                    Log.Debug("Registering IAST Callsite Dataflow Aspects into native library.");
                    var aspects = NativeMethods.RegisterIastAspects(AspectDefinitions.Aspects);
                    Log.Information<int>("{Aspects} IAST Callsite Dataflow Aspects added to the profiler.", aspects);
                    TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.IastAspects, aspects);
                }
                catch (Exception ex)
                {
                    Iast.Iast.Instance.Settings.Enabled = false;
                    Log.Error(ex, "DDIAST-0001-01: IAST could not start because of an unexpected error. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help.");
                }

                if (sw != null)
                {
                    TelemetryFactory.Metrics.RecordDistributionInitTime(MetricTags.InitializationComponent.Iast, sw.ElapsedMilliseconds);
                    sw.Restart();
                }
            }
        }

        private static void RemoveTracerInstrumentationsLegacy(InstrumentationCategory categories)
        {
            if (categories.HasFlag(InstrumentationCategory.AppSec))
            {
                int defs = 0, derived = 0;
                try
                {
                    Log.Debug("Removing CallTarget AppSec integration definitions from native library.");
                    var payload = InstrumentationDefinitions.GetAllDefinitions(InstrumentationCategory.AppSec);
                    NativeMethods.RemoveCallTargetDefinitions(payload.DefinitionsId, payload.Definitions);
                    defs = payload.Definitions.Length;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error removing CallTarget AppSec integration definitions from native library");
                }

                try
                {
                    Log.Debug("Removing CallTarget appsec derived integration definitions from native library.");
                    var payload = InstrumentationDefinitions.GetDerivedDefinitions(InstrumentationCategory.AppSec);
                    NativeMethods.RemoveCallTargetDefinitions(payload.DefinitionsId, payload.Definitions);
                    derived = payload.Definitions.Length;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error removing CallTarget appsec derived integration definitions from native library");
                }

                Log.Information<int, int>("{DefinitionCount} AppSec definitions and {DerivedCount} AppSec derived definitions removed from the profiler.", defs, derived);
            }
        }
    }
}
