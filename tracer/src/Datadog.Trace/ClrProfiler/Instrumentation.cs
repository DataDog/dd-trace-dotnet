// <copyright file="Instrumentation.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Agent.DiscoveryService;
using Datadog.Trace.Ci;
using Datadog.Trace.Configuration;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Logging;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.ServiceFabric;
using Datadog.Trace.Telemetry;
using Datadog.Trace.Telemetry.Metrics;
using Datadog.Trace.Util;

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
        /// Gets a value indicating the version of the native Datadog profiler. This method
        /// is rewritten by the profiler.
        /// </summary>
        /// <returns>In a managed-only context, where the profiler is not attached, <c>None</c>,
        /// otherwise the version of the Datadog native tracer library.</returns>
        // [Instrumented] This is auto-rewritten, not instrumented with calltarget
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static string GetNativeTracerVersion() => "None";

        /// <summary>
        /// Initializes global instrumentation values.
        /// </summary>
        public static void Initialize()
        {
            using var cd = CodeDurationRef.Create();

            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                // Initialize() was already called before
                return;
            }

            try
            {
                TracerDebugger.WaitForDebugger();

                var swTotal = RefStopwatch.Create();
                Log.Debug("Initialization started.");

                var sw = RefStopwatch.Create();

                bool versionMismatch = GetNativeTracerVersion() != TracerConstants.ThreePartVersion;
                if (versionMismatch)
                {
                    Log.Error("Version mismatch detected. This scenario should not exist. Native: {Native} Managed: {Managed}", GetNativeTracerVersion(), TracerConstants.ThreePartVersion);
                }
                else
                {
                    InitializeNoNativeParts(ref sw);

                    try
                    {
                        Log.Debug("Enabling CallTarget integration definitions in native library.");

                        InstrumentationCategory enabledCategories = InstrumentationCategory.Tracing;
                        var defs = NativeMethods.InitEmbeddedCallTargetDefinitions(enabledCategories, ConfigTelemetryData.TargetFramework);
                        Log.Information<int>("The profiler has been initialized with {Count} definitions.", defs);
                        TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.CallTarget, defs);

                        var iastEnabled = Iast.Iast.Instance.Settings.Enabled;

                        if (iastEnabled)
                        {
                            Log.Debug("Enabling Iast call target category");
                            Iast.Iast.Instance.InitAnalyzers();
                            EnableTracerInstrumentations(InstrumentationCategory.Iast);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error sending definitions to native library");
                    }

                    TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.CallTargetDefsPinvoke, sw.ElapsedMilliseconds);
                    sw.Restart();

                    InitializeTracer(ref sw);
                }

                LifetimeManager.Instance.AddShutdownTask(RunShutdown);

                Log.Debug("Initialization finished.");

                TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Total, swTotal.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                // if we're in SSI we want to be as safe as possible, so catching to avoid the possibility of a crash
                try
                {
                    Log.Error(ex, "Error in Datadog.Trace.ClrProfiler.Managed.Loader.Startup.Startup(). Functionality may be impacted.");
                    return;
                }
                catch
                {
                    // Swallowing any errors here, as something went _very_ wrong, even with logging
                    // and we 100% don't want to crash in SSI. Outside of SSI we do want to see the crash
                    // so that it's visible to the user.
                    if (IsInSsi())
                    {
                        return;
                    }
                }

                throw;
            }

            static bool IsInSsi()
            {
                try
                {
                    // Not using the ReadEnvironmentVariable method here to avoid logging (which could cause a crash itself)
                    return !string.IsNullOrEmpty(EnvironmentHelpersNoLogging.SsiDeployedEnvVar());
                }
                catch
                {
                    // sigh, nothing works, _pretend_ we're in SSI so that we don't crash the app
                    return true;
                }
            }
        }

        private static void RunShutdown(Exception ex)
        {
            InstrumentationDefinitions.Dispose();
            NativeCallTargetUnmanagedMemoryHelper.Free();
        }

        internal static void InitializeNoNativeParts(ref RefStopwatch sw)
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

#if NET6_0_OR_GREATER
            if (TrimmingDetector.DetectedTrimmingState == TrimmingDetector.TrimState.TrimmedAppMissingTrimmingFile)
            {
                Log.Warning(
                    "Application trimming detected: a standard .NET type could not be loaded. "
                  + "Some Datadog instrumentation may not work correctly. "
                  + "To make your app compatible with trimming, add a reference to the "
                  + "Datadog.Trace.Trimming NuGet package.");
            }
#endif

            // Eagerly initialize the root session ID so child processes
            // inherit it even if spawned before the first telemetry flush.
            _ = RuntimeId.GetRootSessionId();

            try
            {
                // ensure global instance is created if it's not already
                var testOptimization = TestOptimization.Instance;
                if (testOptimization.Enabled)
                {
                    testOptimization.Initialize();
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
#if !NETFRAMEWORK
            // we only support Service Fabric Service Remoting instrumentation on .NET Core (including .NET 5+)
            if (FrameworkDescription.Instance.IsCoreClr())
            {
                Log.Debug("Initializing ServiceFabric instrumentation");

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
#endif // #if !NETFRAMEWORK

            Log.Debug("Initialization of non native parts finished.");

            var tracer = Tracer.Instance;
            if (tracer is null)
            {
                Log.Debug("Tracer.Instance is null after InitializeNoNativeParts was invoked");
            }

            TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Managed, sw.ElapsedMilliseconds);
            sw.Restart();
        }

        private static void InitializeTracer(ref RefStopwatch sw)
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

                TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.TraceMethodsPinvoke, sw.ElapsedMilliseconds);
                sw.Restart();
            }
        }

        private static void StartDiagnosticManager()
        {
            var observers = new List<DiagnosticObserver>();

#if !NETFRAMEWORK
            if (!SkipAspNetCoreDiagnosticObserver())
            {
                observers.Add(GetAspNetCoreDiagnosticObserver());
            }
#endif

            observers.Add(new QuartzDiagnosticObserver());

            var diagnosticManager = new DiagnosticManager(observers);
            diagnosticManager.Start();
            DiagnosticManager.Instance = diagnosticManager;
        }

#if !NETFRAMEWORK
#if NET6_0_OR_GREATER
        private static DiagnosticObserver GetAspNetCoreDiagnosticObserver()
#else
        private static AspNetCoreDiagnosticObserver GetAspNetCoreDiagnosticObserver()
#endif
        {
#if NET6_0_OR_GREATER
            if (Tracer.Instance.Settings.SingleSpanAspNetCoreEnabled)
            {
                return new SingleSpanAspNetCoreDiagnosticObserver(Tracer.Instance, Iast.Iast.Instance);
            }
#endif // #if NET6_0_OR_GREATER

            return new AspNetCoreDiagnosticObserver(Tracer.Instance, Iast.Iast.Instance);
        }

        [Pure]
        private static bool SkipAspNetCoreDiagnosticObserver() => false;
#endif

        // /!\ This method is called by reflection in the SampleHelpers
        // If you remove it then you need to provide an alternative way to wait for the discovery service
        private static async Task<bool> WaitForDiscoveryService(IDiscoveryService discoveryService)
        {
            var tc = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            // Stop waiting if we're shutting down
            LifetimeManager.Instance.AddShutdownTask(_ => tc.TrySetResult(false));

            discoveryService.SubscribeToChanges(Callback);
            return await tc.Task.ConfigureAwait(false);

            void Callback(AgentConfiguration x)
            {
                tc.TrySetResult(true);
                discoveryService.RemoveSubscription(Callback);
            }
        }

        internal static void EnableTracerInstrumentations(InstrumentationCategory categories, Stopwatch sw = null)
        {
            var defs = NativeMethods.EnableCallTargetDefinitions((uint)categories);
            TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.CallTarget, defs);
            EnableCallSiteInstrumentations(categories, sw);
        }

        private static void EnableCallSiteInstrumentations(InstrumentationCategory categories, Stopwatch sw)
        {
            var isIast = categories.HasFlag(InstrumentationCategory.Iast);

            if (isIast)
            {
                Log.Debug("Registering IAST Callsite Dataflow Aspects into native library.");

                var aspects = NativeMethods.InitEmbeddedCallSiteDefinitions(categories, ConfigTelemetryData.TargetFramework);
                Log.Information<int>("IAST: {Aspects} Callsite Dataflow Aspects added to the profiler.", aspects);
                TelemetryFactory.Metrics.RecordGaugeInstrumentations(MetricTags.InstrumentationComponent.IastAspects, aspects);

                if (sw != null)
                {
                    TelemetryFactory.Metrics.RecordDistributionSharedInitTime(MetricTags.InitializationComponent.Iast, sw.ElapsedMilliseconds);
                    sw.Restart();
                }
            }
        }

        internal static void DisableTracerInstrumentations(InstrumentationCategory categories, Stopwatch sw = null)
        {
            NativeMethods.DisableCallTargetDefinitions((uint)categories);
        }
    }
}
