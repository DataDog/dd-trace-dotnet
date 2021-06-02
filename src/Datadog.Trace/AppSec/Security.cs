// <copyright file="Security.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.AppSec.Transport;
using Datadog.Trace.AppSec.Waf;

namespace Datadog.Trace.AppSec
{
    /// <summary>
    /// The Secure is responsible cooridating app sec
    /// </summary>
    public class Security : IDatadogSecurity
    {
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
            _instrumentationGateway = instrumentationGateway ?? new InstrumentationGateway();

            _powerWaf = powerWaf ?? new PowerWaf();

            _instrumentationGateway.InstrumentationGetwayEvent += InstrumentationGateway_InstrumentationGetwayEvent;

            var found = bool.TryParse(Environment.GetEnvironmentVariable("DD_DISABLE_SECURITY"), out var disabled);
            Enabled = (!found || !disabled);
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
            _powerWaf.Dispose();
        }

        private void RunWafAndReact(IDictionary<string, object> args, ITransport transport)
        {
            var additiveContext = _powerWaf.CreateAdditiveContext();

            // run the WAF and execute the results
            using var result = additiveContext.Run(args);
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
