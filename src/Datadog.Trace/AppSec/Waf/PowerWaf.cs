// <copyright file="PowerWaf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;

namespace Datadog.Trace.AppSec.Waf
{
    internal class PowerWaf : IPowerWaf
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(PowerWaf));

        private readonly Rule rule;
        private bool disposed = false;

        public PowerWaf()
        {
            rule = NewRule();
        }

        ~PowerWaf()
        {
            Dispose(false);
        }

        public Version Version
        {
            get
            {
                var ver = Native.pw_getVersion();
                return new Version(ver.Major, ver.Minor, ver.Patch);
            }
        }

        public IAdditiveContext CreateAdditiveContext()
        {
            if (rule == null)
            {
                return new NullAdditiveContext();
            }

            var handle = Native.pw_initAdditiveH(rule.Handle);
            return new AdditiveContext(handle);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            rule?.Dispose();
        }

        private Rule NewRule()
        {
            try
            {
                string message = null;
                PWConfig args = default;

                var assembly = GetType().Assembly;
                var resource = assembly.GetManifestResourceStream("Datadog.Trace.AppSec.Waf.rule-set.json");
                var reader = new StreamReader(resource);
                var rules = reader.ReadToEnd();

                Log.Debug($"NewRule: got {rules.Length} bytes");

                var ruleHandle = Native.pw_initH(rules, ref args, ref message);

                if (ruleHandle == IntPtr.Zero)
                {
                    Log.Error("Failed to create rules: {Message}", message);
                    return null;
                }
                else
                {
                    Log.Information("Rules successfully created: {Message}", message);
                }

                return new Rule(ruleHandle);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading the power WAF rules");
                return null;
            }
        }
    }
}
