// <copyright file="WafConfigurator.cs" company="Datadog">
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

namespace Datadog.Trace.AppSec.Waf.Initialization
{
    internal static class WafConfigurator
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(WafConfigurator));

        internal static IntPtr? Configure(string rulesFile, WafNative wafNative)
        {
            var argCache = new List<Obj>();
            var configObj = GetConfigObj(rulesFile, argCache, wafNative.Encoder);
            if (configObj == null)
            {
                return null;
            }

            try
            {
                DdwafConfigStruct args = default;
                var ruleHandle = wafNative.Init(configObj.RawPtr, ref args);
                return ruleHandle;
            }
            finally
            {
                configObj.Dispose();
                foreach (var arg in argCache)
                {
                    arg.Dispose();
                }
            }
        }

        private static Obj GetConfigObj(string rulesFile, List<Obj> argCache, Encoder encoder)
        {
            Obj configObj;
            try
            {
                using var stream = GetRulesStream(rulesFile);

                if (stream == null)
                {
                    return null;
                }

                using var reader = new StreamReader(stream);
                using var jsonReader = new JsonTextReader(reader);
                var root = JToken.ReadFrom(jsonReader);

                LogRuleDetailsIfDebugEnabled(root);
                configObj = encoder.Encode(root, argCache);
            }
            catch (Exception ex)
            {
                if (rulesFile != null)
                {
                    Log.Error(ex, "AppSec could not read the rule file \"{RulesFile}\" as it was invalid. AppSec will not run any protections in this application.", rulesFile);
                }
                else
                {
                    Log.Error(ex, "AppSec could not read the rule file embedded in the manifest as it was invalid. AppSec will not run any protections in this application.");
                }

                return null;
            }

            return configObj;
        }

        private static void LogRuleDetailsIfDebugEnabled(JToken root)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                try
                {
                    var eventsProp = root.Value<JArray>("rules");
                    Log.Debug($"eventspropo {eventsProp.Count}");
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

        private static Stream GetRulesStream(string rulesFile) => string.IsNullOrWhiteSpace(rulesFile) ?
                                                                      GetRulesManifestStream() :
                                                                      GetRulesFileStream(rulesFile);

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
    }
}
