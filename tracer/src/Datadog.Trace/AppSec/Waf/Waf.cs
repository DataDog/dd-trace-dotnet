// <copyright file="Waf.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private readonly WafHandle wafHandle;
        private bool disposed = false;

        private Waf(WafHandle wafHandle)
        {
            this.wafHandle = wafHandle;
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

        // null rulesFile means use rules embedded in the manifest
        public static Waf Initialize(string rulesFile)
        {
            var argCache = new List<Obj>();
            Obj configObj;
            try
            {
                using var stream = GetRulesStream(rulesFile);

                if (stream == null)
                {
                    return null;
                }

                configObj = CreatObjFromRulesStream(argCache, stream);
            }
            catch (Exception ex)
            {
                if (rulesFile != null)
                {
                    Log.Error(ex, "AppSec could not read the rule file \"{RulesFile}\" as it was invalid. AppSec will not run any protections in this application.", rulesFile);
                }
                else
                {
                    Log.Error(ex, "AppSec could not read the rule file emmbeded in the manifest as it was invalid. AppSec will not run any protections in this application.");
                }

                return null;
            }

            try
            {
                DdwafConfigStruct args = default;
                var ruleHandle = WafNative.Init(configObj.RawPtr, ref args);
                return new Waf(new WafHandle(ruleHandle));
            }
            finally
            {
                configObj?.Dispose();
                foreach (var arg in argCache)
                {
                    arg.Dispose();
                }
            }
        }

        public IContext CreateContext()
        {
            var handle = WafNative.InitContext(wafHandle.Handle, WafNative.ObjectFreeFuncPtr);

            if (handle == IntPtr.Zero)
            {
                Log.Error("WAF initialization failed.");
                throw new Exception("WAF initialization failed.");
            }

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

            wafHandle?.Dispose();
        }

        private static Obj CreatObjFromRulesStream(List<Obj> argCache, Stream stream)
        {
            using var reader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(reader);
            var root = JToken.ReadFrom(jsonReader);

            LogRuleDetailsIfDebugEnabled(root);

            return Encoder.Encode(root, argCache);
        }

        private static Stream GetRulesManifestStream()
        {
            var assembly = typeof(Waf).Assembly;
            return assembly.GetManifestResourceStream("Datadog.Trace.AppSec.Waf.rule-set.json");
        }

        private static Stream GetRulesFileStream(string rulesFile)
        {
            if (!File.Exists(rulesFile))
            {
                Log.Error("AppSec could not find the rules file in path \"{RulesFile}\". AppSec will not run any protections in this application.", rulesFile);
                return null;
            }

            return File.OpenRead(rulesFile);
        }

        private static void LogRuleDetailsIfDebugEnabled(JToken root)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                try
                {
                    var eventsProp = root.Value<JArray>("rules");
                    foreach (var ev in eventsProp)
                    {
                        var idProp = ev.Value<JValue>("id");
                        var nameProp = ev.Value<JValue>("name");
                        var addresses = ev.Value<JArray>("conditions").SelectMany(x => x.Value<JObject>("parameters").Value<JArray>("inputs"));
                        Log.Debug("Loaded rule: {id} - {name} on addresses: {addresses}", idProp.Value, nameProp.Value, string.Join(", ", addresses));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error occured logging the ddwaf rules");
                }
            }
        }

        private static Stream GetRulesStream(string rulesFile)
        {
            return string.IsNullOrWhiteSpace(rulesFile) ?
                    GetRulesManifestStream() :
                    GetRulesFileStream(rulesFile);
        }
    }
}
