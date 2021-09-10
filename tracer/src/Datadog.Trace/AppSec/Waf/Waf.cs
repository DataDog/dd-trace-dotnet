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

        public static Waf Initialize(string rulesFile)
        {
            var wafHandle = CreateWafHandle(rulesFile);

            if (wafHandle == null)
            {
                Log.Error("Failed to create rules.");
            }
            else
            {
                Log.Information("Rules successfully created.");
            }

            return wafHandle == null ? null : new Waf(wafHandle);
        }

        public IContext CreateContext()
        {
            var handle = WafNative.InitContext(wafHandle.Handle, WafNative.ObjectFreeFuncPtr);
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

            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                LogRuleDetails(root);
            }

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
                Log.Error($"AppSec could not find the rules file in path \"{rulesFile}\". AppSec will not run any protections in this application.");
                return null;
            }

            return File.OpenRead(rulesFile);
        }

        // null rulesFile means use rules embedded in the manifest
        private static WafHandle CreateWafHandle(string rulesFile)
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
                var message =
                    rulesFile != null ?
                    $"AppSec could not read the rule file \"{rulesFile}\" as it was invalid. AppSec will not run any protections in this application." :
                    "AppSec could not read the rule file emmbeded in the manifest as it was invalid. AppSec will not run any protections in this application.";
                Log.Error(ex, message);

                return null;
            }

            try
            {
                DdwafConfigStruct args = default;
                var ruleHandle = WafNative.Init(configObj.RawPtr, ref args);
                return new WafHandle(ruleHandle);
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

        private static void LogRuleDetails(JToken root)
        {
            try
            {
                var eventsProp = root.Value<JArray>("events");
                foreach (var ev in eventsProp)
                {
                    var idProp = ev.Value<JValue>("id");
                    var nameProp = ev.Value<JValue>("name");
                    var addresses = ev.Value<JArray>("conditions").SelectMany(x => x.Value<JObject>("parameters").Value<JArray>("inputs"));
                    Log.Debug($"Loaded rule: {idProp.Value} - {nameProp.Value} on addresses: {string.Join(", ", addresses)}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error occured logging the ddwaf rules");
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
