// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

        private InstrumentationGateway _instrumentationGateway;
        private IPowerWaf _powerWaf;

        /// <summary>
        /// Initializes a new instance of the <see cref="Security"/> class with default settings.
        /// </summary>
        public Security()
            : this(null, null)
        {
        }

        internal Security(InstrumentationGateway instrumentationGateway = null, IPowerWaf powerWaf = null)
        {
            var found = Environment.GetEnvironmentVariable(ConfigurationKeys.AppSecEnabled)?.ToBoolean();
            Enabled = found == true;

            Log.Information($"Security.Enabled: {Enabled} ");

            _instrumentationGateway = instrumentationGateway ?? new InstrumentationGateway();

            _powerWaf = powerWaf ?? (Enabled ? new PowerWaf() : new NullPowerWaf());

            _instrumentationGateway.InstrumentationGetwayEvent += InstrumentationGateway_InstrumentationGetwayEvent;
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
        /// Gets <see cref="InstrumentationGateway"/> instance
        /// </summary>
        InstrumentationGateway IDatadogSecurity.InstrumentationGateway => _instrumentationGateway;

        /// <summary>
        /// Frees resouces
        /// </summary>
        public void Dispose()
        {
            _powerWaf?.Dispose();
        }

        private void RunWafAndReact(IDictionary<string, object> args, ITransport transport)
        {
            var additiveContext = transport.GetAdditiveContext();

            if (additiveContext == null)
            {
                additiveContext = _powerWaf.CreateAdditiveContext();
                transport.SetAdditiveContext(additiveContext);
            }

            // run the WAF and execute the results
            using var result = additiveContext.Run(args);
            if (result.ReturnCode == ReturnCode.Monitor || result.ReturnCode == ReturnCode.Block)
            {
                Log.Warning($"Attack detetected! Action: {result.ReturnCode} " + string.Join(", ", args.Select(x => $"{x.Key}: {x.Value}")));
            }

            if (result.ReturnCode == ReturnCode.Block)
            {
                transport.Block();
            }
        }

        private void InstrumentationGateway_InstrumentationGetwayEvent(object sender, InstrumentationGatewayEventArgs e)
        {
            RunWafAndReact(e.EventData, e.Transport);
        }
    }
}
