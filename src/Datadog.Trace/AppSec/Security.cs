// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Datadog.Trace.Agent;
using Datadog.Trace.AppSec.Agent;
using Datadog.Trace.AppSec.EventModel;
using Datadog.Trace.AppSec.Transport;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec
{
    /// <summary>
    /// The Secure is responsible cooridating app sec
    /// </summary>
    public class Security : IDatadogSecurity
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<Security>();

        private static Security _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();

        private readonly IPowerWaf _powerWaf;
        private readonly Datadog.Trace.AppSec.Agent.IAgentWriter _agentWriter;
        private readonly InstrumentationGateway _instrumentationGateway;
        private readonly ConcurrentDictionary<Guid, Action> toExecute = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="Security"/> class with default settings.
        /// </summary>
        public Security()
            : this(null, null)
        {
        }

        private Security(InstrumentationGateway instrumentationGateway = null, IPowerWaf powerWaf = null, Datadog.Trace.AppSec.Agent.IAgentWriter agentWriter = null)
        {
            try
            {
                var enabled = Environment.GetEnvironmentVariable(ConfigurationKeys.AppSecEnabled)?.ToBoolean();
                Enabled = enabled.GetValueOrDefault() && AreArchitectureAndOsSupported();

                var blockingEnabled = Environment.GetEnvironmentVariable(ConfigurationKeys.AppSecBlockingEnabled)?.ToBoolean();
                BlockingEnabled = blockingEnabled == true;

                Log.Information($"Security.Enabled: {Enabled}, Security.BlockingEnabled {BlockingEnabled}");

                _instrumentationGateway = instrumentationGateway ?? new InstrumentationGateway();

                if (Enabled)
                {
                    _powerWaf = powerWaf ?? PowerWaf.Initialize();
                    if (_powerWaf != null)
                    {
                        _agentWriter = agentWriter ?? new Datadog.Trace.AppSec.Agent.AgentWriter();
                        _instrumentationGateway.InstrumentationGatewayEvent += InstrumentationGatewayInstrumentationGatewayEvent;
                    }
                    else
                    {
                        Enabled = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Enabled = false;
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
        /// Gets a value indicating whether security is enabled
        /// </summary>
        public bool Enabled { get; }

        /// <summary>
        /// Gets a value indicating whether blocking is enabled
        /// </summary>
        public bool BlockingEnabled { get; }

        /// <summary>
        /// Gets <see cref="InstrumentationGateway"/> instance
        /// </summary>
        InstrumentationGateway IDatadogSecurity.InstrumentationGateway => _instrumentationGateway;

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
                additiveContext = _powerWaf.CreateAdditiveContext();
                transport.SetAdditiveContext(additiveContext);
            }

            // run the WAF and execute the results
            using var wafResult = additiveContext.Run(args);
            if (wafResult.ReturnCode == ReturnCode.Monitor || wafResult.ReturnCode == ReturnCode.Block)
            {
                Log.Warning($"AppSec: Attack detected! Action: {wafResult.ReturnCode} " + string.Join(", ", args.Select(x => $"{x.Key}: {x.Value}. Blocking enabled : {BlockingEnabled}")));
                var managedWafResult = Waf.ReturnTypes.Managed.Return.From(wafResult);
                if (BlockingEnabled && wafResult.ReturnCode == ReturnCode.Block)
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
            var supportedOs = new[] { OSPlatforms.Linux, OSPlatforms.MacOS, OSPlatforms.Windows };
            if (supportedOs.Contains(frameworkDescription.OSPlatform))
            {
                osSupported = true;
            }
            else
            {
                Log.Warning("AppSec could not start because of unsupported operating system platform {OS}", FrameworkDescription.Instance.OSPlatform);
            }

            var archSupported = false;
            var supportedArchs = new[] { ProcessArchitecture.Arm, ProcessArchitecture.X64, ProcessArchitecture.X86 };
            if (supportedArchs.Contains(frameworkDescription.ProcessArchitecture))
            {
                archSupported = true;
            }
            else
            {
                Log.Warning($"AppSec could not start because the current environment is not supported. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help. Host information: {{ operating_system:{frameworkDescription.OSPlatform} }}, arch:{{ {frameworkDescription.ProcessArchitecture} }}, runtime_infos: {{ {frameworkDescription.ProductVersion} }}");
            }

            return osSupported && archSupported;
        }
    }
}
