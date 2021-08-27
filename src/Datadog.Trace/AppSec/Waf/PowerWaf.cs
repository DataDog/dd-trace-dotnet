// <copyright file="PowerWaf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using YamlDotNet.Deserializer.Serialization;
using YamlDotNet.Deserializer.Serialization.NamingConventions;

namespace Datadog.Trace.AppSec.Waf
{
    internal class PowerWaf : IPowerWaf
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(PowerWaf));

        private readonly Rule rule;
        private bool disposed = false;

        private PowerWaf(Rule rule)
        {
            this.rule = rule;
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

        public static PowerWaf Initialize()
        {
            var rule = NewRule();
            return rule == null ? null : new PowerWaf(rule);
        }

        public IAdditiveContext CreateAdditiveContext()
        {
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

        private static Args DynamicRuleSet()
        {
            var sr = typeof(PowerWaf).Assembly.GetManifestResourceStream("Datadog.Trace.AppSec.Waf.rules.yml");
            using (var reader = new StreamReader(sr))
            {
                var deserializer = new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build();
                var res = deserializer.Deserialize(reader);
                var encoded = Encoder.Encode(res);
                return encoded;
            }
        }

        private static Rule NewRule()
        {
            try
            {
                string message = null;
                PWConfig args = default;

                var rules = DynamicRuleSet();

                var ruleHandle = Native.pw_initH(rules.RawArgs, ref args);

                if (ruleHandle == IntPtr.Zero)
                {
                    Log.Error("Failed to create rules: {Message}", message);
                    return null;
                }
                else
                {
                    Log.Information("Rules successfully created: {Message}", message);
                }

                Log.Information("Successfully create rules {ruleHandle}", ruleHandle);

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
