// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf
{
    internal class Waf : IWaf
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(Waf));

        private readonly WafHandle rule;
        private bool disposed = false;

        private Waf(WafHandle rule)
        {
            this.rule = rule;
        }

        ~Waf()
        {
            Dispose(false);
        }

        public Version Version
        {
            get
            {
                var ver = WafNative.GetVersion();
                return new Version(ver.Major, ver.Minor, ver.Patch);
            }
        }

        public static Waf Initialize()
        {
            var rule = NewRule();
            return rule == null ? null : new Waf(rule);
        }

        public IContext CreateContext()
        {
            var handle = WafNative.InitContext(rule.Handle, WafNative.ObjectFreeFuncPtr);
            return new Context(handle);
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

        private static Obj RuleSetFromManifest(List<Obj> argCache)
        {
            var assembly = typeof(Waf).Assembly;
            var resource = assembly.GetManifestResourceStream("Datadog.Trace.AppSec.Waf.rule-set.json");
            using var reader = new JsonTextReader(new StreamReader(resource));
            var root = JToken.ReadFrom(reader);

            return Encoder.Encode(root, argCache);
        }

        private static WafHandle NewRule()
        {
            try
            {
                DdwafConfigStruct args = default;

                var argCache = new List<Obj>();
                var rules = RuleSetFromManifest(argCache);

                var ruleHandle = WafNative.Init(rules.RawPtr, ref args);

                rules.Dispose();
                foreach (var arg in argCache)
                {
                    arg.Dispose();
                }

                if (ruleHandle == IntPtr.Zero)
                {
                    Log.Error("Failed to create rules.");
                    return null;
                }
                else
                {
                    Log.Information("Rules successfully created.");
                }

                return new WafHandle(ruleHandle);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading the power WAF rules");
                return null;
            }
        }
    }
}
