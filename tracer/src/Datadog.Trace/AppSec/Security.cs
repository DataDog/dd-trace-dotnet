// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.AppSec.Agent;
using Datadog.Trace.AppSec.EventModel;
using Datadog.Trace.AppSec.Transport;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec
{
    /// <summary>
    /// The Secure is responsible coordinating app sec
    /// </summary>
    internal class Security : IDatadogSecurity, IDisposable
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Security>();

        private static Security _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();

        private readonly IWaf _powerWaf;
        private readonly IAppSecAgentWriter _agentWriter;
        private readonly InstrumentationGateway _instrumentationGateway;
        private readonly SecuritySettings _settings;
        private readonly ConcurrentDictionary<Guid, Action> toExecute = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Security"/> class with default settings.
        /// </summary>
        public Security()
            : this(null, null)
        {
        }

        private Security(SecuritySettings settings = null, InstrumentationGateway instrumentationGateway = null, IWaf powerWaf = null, IAppSecAgentWriter agentWriter = null)
        {
            try
            {
                _settings = settings ?? SecuritySettings.FromDefaultSources();

                _instrumentationGateway = instrumentationGateway ?? new InstrumentationGateway();

                _settings.Enabled = _settings.Enabled && AreArchitectureAndOsSupported();
                if (_settings.Enabled)
                {
                    _powerWaf = powerWaf ?? Waf.Waf.Initialize();
                    if (_powerWaf != null)
                    {
                        _agentWriter = agentWriter ?? new AppSecAgentWriter();
                        _instrumentationGateway.InstrumentationGatewayEvent += InstrumentationGatewayInstrumentationGatewayEvent;
                    }
                    else
                    {
                        _settings.Enabled = false;
                    }

                    RegisterShutdownTasks();
                }
            }
            catch (Exception ex)
            {
                _settings.Enabled = false;
                Log.Error(ex, "Datadog AppSec failed to initialize, your application is NOT protected");
            }
        }

        /// <summary>
        /// Gets or sets the global <see cref="Security"/> instance.
        /// </summary>
        public static Security Instance
        {
            get
            {
                return LazyInitializer.EnsureInitialized(ref _instance, ref _globalInstanceInitialized, ref _globalInstanceLock);
            }

            set
            {
                lock (_globalInstanceLock)
                {
                    _instance = value;
                    _globalInstanceInitialized = true;
                }
            }
        }

        /// <summary>
        /// Gets <see cref="InstrumentationGateway"/> instance
        /// </summary>
        InstrumentationGateway IDatadogSecurity.InstrumentationGateway => _instrumentationGateway;

        /// <summary>
        /// Gets <see cref="SecuritySettings"/> instance
        /// </summary>
        SecuritySettings IDatadogSecurity.Settings => _settings;

        internal SecuritySettings Settings => _settings;

        /// <summary>
        /// Frees resouces
        /// </summary>
        public void Dispose() => _powerWaf?.Dispose();

        internal void Execute(Guid guid)
        {
            if (toExecute.TryRemove(guid, out var value))
            {
                value();
            }
        }

        private void RunWafAndReact(IDictionary<string, object> args, ITransport transport, Span span)
        {
            void Report(ITransport transport, Span span, Waf.ReturnTypes.Managed.Return result)
            {
                var attack = Attack.From(result, span, transport);
                _agentWriter.AddEvent(attack);
            }

            var additiveContext = transport.GetAdditiveContext();

            if (additiveContext == null)
            {
                additiveContext = _powerWaf.CreateContext();
                transport.SetAdditiveContext(additiveContext);
            }

            // run the WAF and execute the results
            using var wafResult = additiveContext.Run(args);
            if (wafResult.ReturnCode == ReturnCode.Monitor || wafResult.ReturnCode == ReturnCode.Block)
            {
                Log.Information($"AppSec: Attack detected! Action: {wafResult.ReturnCode}, Blocking enabled : {_settings.BlockingEnabled}");
                if (Log.IsEnabled(LogEventLevel.Debug))
                {
                    Log.Information($"AppSec: Attack arguments " + Encoder.FormatArgs(args));
                }

                var managedWafResult = Waf.ReturnTypes.Managed.Return.From(wafResult);
                if (_settings.BlockingEnabled && wafResult.ReturnCode == ReturnCode.Block)
                {
                    transport.Block();
#if !NETFRAMEWORK
                    var guid = Guid.NewGuid();
                    toExecute.TryAdd(guid, () => Report(transport, span, managedWafResult));
                    transport.AddRequestScope(guid);
#else
                    Report(transport, span, managedWafResult);
#endif
                }
                else
                {
                    Report(transport, span, managedWafResult);
                }
            }
        }

        private void InstrumentationGatewayInstrumentationGatewayEvent(object sender, InstrumentationGatewayEventArgs e)
        {
            try
            {
                RunWafAndReact(e.EventData, e.Transport, e.RelatedSpan);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Call into the security module failed");
            }
        }

        private bool AreArchitectureAndOsSupported()
        {
            var frameworkDescription = FrameworkDescription.Instance;
            var osSupported = false;
            var supportedOs = new[] { OSPlatform.Linux, OSPlatform.MacOS, OSPlatform.Windows };
            if (supportedOs.Contains(frameworkDescription.OSPlatform))
            {
                osSupported = true;
            }

            var archSupported = false;
            var supportedArchs = new[] { ProcessArchitecture.Arm, ProcessArchitecture.X64, ProcessArchitecture.X86 };
            if (supportedArchs.Contains(frameworkDescription.ProcessArchitecture))
            {
                archSupported = true;
            }

            if (!osSupported || !archSupported)
            {
                Log.Warning($"AppSec could not start because the current environment is not supported. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help. Host information: {{ operating_system:{frameworkDescription.OSPlatform} }}, arch:{{ {frameworkDescription.ProcessArchitecture} }}, runtime_infos: {{ {frameworkDescription.ProductVersion} }}");
            }

            return osSupported && archSupported;
        }

        private void RunShutdown()
        {
            _agentWriter?.Shutdown();
            if (_instrumentationGateway != null)
            {
                _instrumentationGateway.InstrumentationGatewayEvent -= InstrumentationGatewayInstrumentationGatewayEvent;
            }

            Dispose();
        }

        private void RegisterShutdownTasks()
        {
            // Register callbacks to make sure we flush the traces before exiting
            AppDomain.CurrentDomain.ProcessExit += ProcessExit;
            AppDomain.CurrentDomain.DomainUnload += DomainUnload;

            try
            {
                // Registering for the AppDomain.UnhandledException event cannot be called by a security transparent method
                // This will only happen if the Tracer is not run full-trust
                AppDomain.CurrentDomain.UnhandledException += UnhandledException;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to register a callback to the AppDomain.UnhandledException event.");
            }

            try
            {
                // Registering for the cancel key press event requires the System.Security.Permissions.UIPermission
                Console.CancelKeyPress += CancelKeyPress;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Unable to register a callback to the Console.CancelKeyPress event.");
            }
        }

        private void ProcessExit(object sender, EventArgs e)
        {
            AppDomain.CurrentDomain.ProcessExit -= ProcessExit;
            RunShutdown();
        }

        private void DomainUnload(object sender, EventArgs e)
        {
            AppDomain.CurrentDomain.DomainUnload -= DomainUnload;
            RunShutdown();
        }

        private void CancelKeyPress(object sender, EventArgs e)
        {
            Console.CancelKeyPress -= CancelKeyPress;
            RunShutdown();
        }

        private void UnhandledException(object sender, EventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException -= UnhandledException;
            RunShutdown();
        }
    }
}
