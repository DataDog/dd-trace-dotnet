// <copyright file="WafConfigurator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypesManaged;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Serilog.Events;

namespace Datadog.Trace.AppSec.Waf.Initialization
{
    internal static class WafConfigurator
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(WafConfigurator));

        internal static InitializationResult Configure(string rulesFile, WafNative wafNative, Encoder encoder, string obfuscationParameterKeyRegex, string obfuscationParameterValueRegex)
        {
            var argCache = new List<Obj>();
            var configObj = GetConfigObj(rulesFile, argCache, encoder);
            if (configObj == null)
            {
                return InitializationResult.FromUnusableRuleFile();
            }

            DdwafRuleSetInfoStruct ruleSetInfo = default;
            var keyRegex = IntPtr.Zero;
            var valueRegex = IntPtr.Zero;

            try
            {
                DdwafConfigStruct args = default;
                keyRegex = Marshal.StringToHGlobalAnsi(obfuscationParameterKeyRegex);
                valueRegex = Marshal.StringToHGlobalAnsi(obfuscationParameterValueRegex);
                args.KeyRegex = keyRegex;
                args.ValueRegex = valueRegex;

                var ruleHandle = wafNative.Init(configObj.RawPtr, ref args, ref ruleSetInfo);
                if (ruleHandle == IntPtr.Zero)
                {
                    Log.Warning("DDAS-0005-00: WAF initialization failed.");
                }

                var initResult = InitializationResult.From(ruleSetInfo, ruleHandle);
                if (initResult.LoadedRules == 0)
                {
                    Log.Error("DDAS-0003-03: AppSec could not read the rule file {rulesFile}. Reason: All rules are invalid. AppSec will not run any protections in this application.", rulesFile);
                }
                else
                {
                    Log.Information("DDAS-0015-00: AppSec loaded {loadedRules} from file {rulesFile}.", initResult.LoadedRules, rulesFile);
                }

                if (initResult.HasErrors)
                {
                    var sb = StringBuilderCache.Acquire(0);
                    sb.Append($"WAF initialization failed. Some rules are invalid in rule file {rulesFile}:");
                    foreach (var item in initResult.Errors)
                    {
                        sb.Append($"{item.Key}: [{string.Join(", ", item.Value)}] ");
                    }

                    var errorMess = StringBuilderCache.GetStringAndRelease(sb);
                    Log.Warning(errorMess);
                }

                return initResult;
            }
            finally
            {
                if (keyRegex != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(keyRegex);
                }

                if (valueRegex != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(valueRegex);
                }

                wafNative.RuleSetInfoFree(ref ruleSetInfo);
                wafNative.ObjectFreePtr(configObj.RawPtr);
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
                // applying safety limits during rule parsing could result in trucated rules
                configObj = encoder.Encode(root, argCache, applySafetyLimits: false);
            }
            catch (Exception ex)
            {
                if (rulesFile != null)
                {
                    Log.Error(ex, "DDAS-0003-02: AppSec could not read the rule file \"{RulesFile}\". Reason: Invalid file format. AppSec will not run any protections in this application.", rulesFile);
                }
                else
                {
                    Log.Error(ex, "DDAS-0003-02: AppSec could not read the rule file embedded in the manifest. Reason: Invalid file format. AppSec will not run any protections in this application.");
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
                        var emptyJValue = JValue.CreateString(string.Empty);
                        var idProp = ev.Value<JValue>("id") ?? emptyJValue;
                        var nameProp = ev.Value<JValue>("name") ?? emptyJValue;
                        var addresses = ev.Value<JArray>("conditions")?.SelectMany(x => x.Value<JObject>("parameters")?.Value<JArray>("inputs"));
                        Log.Debug("DDAS-0007-00: Loaded rule: {id} - {name} on addresses: {addresses}", idProp.Value, nameProp.Value, string.Join(", ", addresses ?? Enumerable.Empty<JToken>()));
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
                Log.Error("DDAS-0003-01: AppSec could not read the rule file \"{RulesFile}\". Reason: File not found. AppSec will not run any protections in this application.", rulesFile);
                return null;
            }

            return File.OpenRead(rulesFile);
        }
    }
}
