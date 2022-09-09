// <copyright file="IAST.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Datadog.Trace.AppSec.Transports;
using Datadog.Trace.AppSec.Transports.Http;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.ClrProfiler;
using Datadog.Trace.Headers;
using Datadog.Trace.Logging;
using Datadog.Trace.Propagators;
using Datadog.Trace.Sampling;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Serilog.Events;
using Datadog.Trace.Vendors.StatsdClient;

namespace Datadog.Trace.IAST
{
    /// <summary>
    /// The Secure is responsible coordinating IAST
    /// </summary>
    internal class IAST
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor<IAST>();

        private static IAST _instance;
        private static bool _globalInstanceInitialized;
        private static object _globalInstanceLock = new();

        private readonly IASTSettings _settings;

        static IAST()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IAST"/> class with default settings.
        /// </summary>
        public IAST()
            : this(null)
        {
        }

        private IAST(IASTSettings settings = null)
        {
            try
            {
                _settings = settings ?? IASTSettings.FromDefaultSources();

                if (_settings.Enabled)
                {
                    // AddAppsecSpecificInstrumentations();
                }
            }
            catch (Exception ex)
            {
                _settings = new(source: null) { Enabled = false };
                Log.Error(ex, "DDIAST-0001-01: IAST could not start because of an unexpected error. No security activities will be collected. Please contact support at https://docs.datadoghq.com/help/ for help.");
            }
        }

        /// <summary>
        /// Gets or sets the global <see cref="IAST"/> instance.
        /// </summary>
        public static IAST Instance
        {
            get => LazyInitializer.EnsureInitialized(ref _instance, ref _globalInstanceInitialized, ref _globalInstanceLock);

            set
            {
                lock (_globalInstanceLock)
                {
                    _instance = value;
                    _globalInstanceInitialized = true;
                }
            }
        }

        internal IASTSettings Settings => _settings;
    }
}
