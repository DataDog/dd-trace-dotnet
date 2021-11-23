// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec.Waf.Initialization;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Waf : IWaf
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Waf));

        private readonly IntPtr ruleHandle;
        private readonly WafNative wafNative;
        private readonly Encoder encoder;
        private bool disposed = false;

        private Waf(IntPtr ruleHandle, WafNative wafNative, Encoder encoder)
        {
            this.ruleHandle = ruleHandle;
            this.wafNative = wafNative;
            this.encoder = encoder;
        }

        ~Waf()
        {
            Dispose(false);
        }

        public Version Version
        {
            get
            {
                var ver = wafNative.GetVersion();
                return new Version(ver.Major, ver.Minor, ver.Patch);
            }
        }

        /// <summary>
        /// Loads library and configure it with the ruleset file
        /// </summary>
        /// <param name="rulesFile">can be null, means use rules embedded in the manifest </param>
        /// <returns>the waf wrapper around waf native</returns>
        internal static Waf Create(string rulesFile)
        {
            var libraryHandle = LibraryLoader.LoadAndGetHandle();
            if (libraryHandle == IntPtr.Zero)
            {
                return null;
            }

            var wafNative = new WafNative(libraryHandle);
            var encoder = new Encoder(wafNative);
            var ruleHandle = WafConfigurator.Configure(rulesFile, wafNative, encoder);
            return ruleHandle == null ? null : new Waf(ruleHandle.Value, wafNative, encoder);
        }

        public IContext CreateContext()
        {
            var handle = wafNative.InitContext(ruleHandle, wafNative.ObjectFreeFuncPtr);

            if (handle == IntPtr.Zero)
            {
                Log.Error("WAF initialization failed.");
                throw new Exception("WAF initialization failed.");
            }

            return new Context(handle, wafNative, encoder);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            this.wafNative.Destroy(this.ruleHandle);
        }
    }
}
