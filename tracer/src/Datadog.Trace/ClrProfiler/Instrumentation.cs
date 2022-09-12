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
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.Logging;
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
        /// Initializes global instrumentation values.
        /// </summary>
        public static void Initialize()
        {
            if (Interlocked.Exchange(ref _firstInitialization, 0) != 1)
            {
                // Initialize() was already called before
                return;
            }

            if (CIVisibility.Enabled)
            {
                CIVisibility.Initialize();
                return;
            }

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
                Log.Debug("Sending CallTarget integration definitions to native library.");
                var payload = InstrumentationDefinitions.GetAllDefinitions();
                NativeMethods.InitializeProfiler(payload.DefinitionsId, payload.Definitions);

                Log.Information<int>("The profiler has been initialized with {count} definitions.", payload.Definitions.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
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

                Log.Information<int>("The profiler has been initialized with {count} derived definitions.", payload.Definitions.Length);
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
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
                Log.Error(ex, ex.Message);
            }

            InitializeNoNativeParts();
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
                    Log.Error(e, e.Message);
                }

                try
                {
                    Log.Debug("Initializing TraceMethods instrumentation.");
                    var traceMethodsConfiguration = tracer.Settings.TraceMethods;
                    var payload = InstrumentationDefinitions.GetTraceMethodDefinitions();
                    NativeMethods.InitializeTraceMethods(payload.DefinitionsId, payload.AssemblyName, payload.TypeName, traceMethodsConfiguration);
                    Log.Information("TraceMethods instrumentation enabled with Assembly={AssemblyName}, Type={TypeName}, and Configuration={}.", payload.AssemblyName, payload.TypeName, traceMethodsConfiguration);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, ex.Message);
                }
            }

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

            Log.Debug("Initialization of non native parts started.");

            try
            {
                var asm = typeof(Instrumentation).Assembly;
#if NET5_0_OR_GREATER
                // Can't use asm.CodeBase or asm.GlobalAssemblyCache in .NET 5+
                Log.Information($"[Assembly metadata] Location: {asm.Location}, HostContext: {asm.HostContext}, SecurityRuleSet: {asm.SecurityRuleSet}");
#else
                Log.Information($"[Assembly metadata] Location: {asm.Location}, CodeBase: {asm.CodeBase}, GAC: {asm.GlobalAssemblyCache}, HostContext: {asm.HostContext}, SecurityRuleSet: {asm.SecurityRuleSet}");
#endif
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }

            try
            {
                // ensure global instance is created if it's not already
                Log.Debug("Initializing tracer singleton instance.");
                _ = Tracer.Instance;
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }

            try
            {
                Log.Debug("Initializing security singleton instance.");
                _ = Security.Instance;
            }
            catch (Exception ex)
            {
                Log.Error(ex, ex.Message);
            }

#if !NETFRAMEWORK
            try
            {
                if (GlobalSettings.Source.DiagnosticSourceEnabled)
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
        internal static void StartDiagnosticManager()
        {
            var observers = new List<DiagnosticObserver>();

            if (!PlatformHelpers.AzureAppServices.Metadata.IsFunctionsApp)
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
            var serviceName = tracer.Settings.ServiceName ?? tracer.DefaultServiceName;
            var exporterSettings = tracer.Settings.Exporter;
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
                        var rcmApi = RemoteConfigurationApiFactory.Create(exporterSettings, rcmSettings, discoveryService);

                        var configurationManager = RemoteConfigurationManager.Create(discoveryService, rcmApi, rcmSettings, serviceName);
                        configurationManager.RegisterProduct(SharedRemoteConfiguration.FeaturesProduct);

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

        internal static async Task<bool> WaitForDiscoveryService(IDiscoveryService discoveryService)
        {
            var tc = new TaskCompletionSource<bool>();
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
