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
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Serilog.Events;

#nullable enable

namespace Datadog.Trace.AppSec.Waf.Initialization
{
    internal class WafConfigurator
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(WafConfigurator));
        private readonly WafLibraryInvoker _wafLibraryInvoker;

        public WafConfigurator(WafLibraryInvoker wafLibraryInvoker) => _wafLibraryInvoker = wafLibraryInvoker;

        private static void LogRuleDetailsIfDebugEnabled(JToken root)
        {
            if (Log.IsEnabled(LogEventLevel.Debug))
            {
                try
                {
                    var eventsProp = root.Value<JArray>("rules");
                    Log.Debug<int>("eventspropo {Count}", eventsProp!.Count);
                    foreach (var ev in eventsProp)
                    {
                        var emptyJValue = JValue.CreateString(string.Empty);
                        var idProp = ev.Value<JValue>("id") ?? emptyJValue;
                        var nameProp = ev.Value<JValue>("name") ?? emptyJValue;
                        var addresses = ev.Value<JArray>("conditions")?.SelectMany(x => x.Value<JObject>("parameters")?.Value<JArray>("inputs")!);
                        Log.Debug("DDAS-0007-00: Loaded rule: {Id} - {Name} on addresses: {Addresses}", idProp.Value, nameProp.Value, string.Join(", ", addresses ?? Enumerable.Empty<JToken>()));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error occured logging the ddwaf rules");
                }
            }
        }

        private static Stream? GetRulesStream(string? rulesFile) => string.IsNullOrWhiteSpace(rulesFile) ? GetRulesManifestStream() : GetRulesFileStream(rulesFile!);

        private static Stream? GetRulesManifestStream()
        {
            var assembly = typeof(Waf).Assembly;
            return assembly.GetManifestResourceStream("Datadog.Trace.AppSec.Waf.rule-set.json");
        }

        private static Stream? GetRulesFileStream(string rulesFile)
        {
            if (!File.Exists(rulesFile))
            {
                Log.Error("DDAS-0003-01: AppSec could not read the rule file \"{RulesFile}\". Reason: File not found. AppSec will not run any protections in this application.", rulesFile);
                return null;
            }

            return File.OpenRead(rulesFile);
        }

        internal InitResult Configure(string? rulesFile, string obfuscationParameterKeyRegex, string obfuscationParameterValueRegex)
        {
            var argsToDispose = new List<Obj>();
            var rulesObj = GetConfigObj(rulesFile, argsToDispose);
            return ConfigureAndDispose(rulesObj, rulesFile, argsToDispose, obfuscationParameterKeyRegex, obfuscationParameterValueRegex);
        }

        internal InitResult ConfigureFromRemoteConfig(string rulesJson, string obfuscationParameterKeyRegex, string obfuscationParameterValueRegex)
        {
            var argCache = new List<Obj>();
            return ConfigureAndDispose(GetConfigObjFromRemoteJson(rulesJson, argCache), "RemoteConfig", argCache, obfuscationParameterKeyRegex, obfuscationParameterValueRegex);
        }

        private InitResult ConfigureAndDispose(Obj? rulesObj, string? rulesFile, List<Obj> argsToDispose, string obfuscationParameterKeyRegex, string obfuscationParameterValueRegex)
        {
            if (rulesObj == null)
            {
                Log.Error("Waf couldn't initialize properly because of an unusable rule file. If you set the environment variable {AppsecruleEnv}, check the path and content of the file are correct.", ConfigurationKeys.AppSec.Rules);
                return InitResult.FromUnusableRuleFile();
            }

            var ruleSetInfo = new DdwafRuleSetInfo();
            var keyRegex = IntPtr.Zero;
            var valueRegex = IntPtr.Zero;

            try
            {
                DdwafConfigStruct args = default;
                keyRegex = Marshal.StringToHGlobalAnsi(obfuscationParameterKeyRegex);
                valueRegex = Marshal.StringToHGlobalAnsi(obfuscationParameterValueRegex);
                args.KeyRegex = keyRegex;
                args.ValueRegex = valueRegex;
                args.FreeWafFunction = _wafLibraryInvoker.ObjectFreeFuncPtr;

                var wafHandle = _wafLibraryInvoker.Init(rulesObj.RawPtr, ref args, ruleSetInfo);
                if (wafHandle == IntPtr.Zero)
                {
                    Log.Warning("DDAS-0005-00: WAF initialization failed.");
                }

                var initResult = InitResult.From(ruleSetInfo, wafHandle, _wafLibraryInvoker);
                if (initResult.LoadedRules == 0)
                {
                    Log.Error("DDAS-0003-03: AppSec could not read the rule file {RulesFile}. Reason: All rules are invalid. AppSec will not run any protections in this application.", rulesFile);
                }
                else
                {
                    Log.Information("DDAS-0015-00: AppSec loaded {LoadedRules} rules from file {RulesFile}.", initResult.LoadedRules, rulesFile);
                }

                if (initResult.HasErrors)
                {
                    var sb = StringBuilderCache.Acquire(StringBuilderCache.MaxBuilderSize);
                    foreach (var item in initResult.Errors)
                    {
                        sb.Append($"{item.Key}: [{string.Join(", ", item.Value)}] ");
                    }

                    var errorMess = StringBuilderCache.GetStringAndRelease(sb);
                    Log.Warning("WAF initialization failed. Some rules are invalid in rule file {RulesFile}: {ErroringRules}", rulesFile, errorMess);
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

                _wafLibraryInvoker.RuleSetInfoFree(ruleSetInfo);
                _wafLibraryInvoker.ObjectFreePtr(rulesObj.RawPtr);
                rulesObj.Dispose();
                foreach (var arg in argsToDispose)
                {
                    arg.Dispose();
                }
            }
        }

        private Obj? GetConfigObj(string? rulesFile, List<Obj> argCache)
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
                configObj = GetConfigObj(reader, argCache);
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

        internal Obj? GetConfigObjFromRemoteJson(string rulesJson, List<Obj>? argCache)
        {
            Obj configObj;
            try
            {
                using var reader = new StringReader(rulesJson);
                configObj = GetConfigObj(reader, argCache);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "DDAS-0003-02: AppSec could not read the rule file sent by remote config. Reason: Invalid file format. AppSec will not run any protections in this application.");
                return null;
            }

            return configObj;
        }

        private Obj GetConfigObj(TextReader reader, List<Obj>? argCache)
        {
            using var jsonReader = new JsonTextReader(reader);
            var root = JToken.ReadFrom(jsonReader);

            LogRuleDetailsIfDebugEnabled(root);
            // applying safety limits during rule parsing could result in truncated rules
            var configObj = Encoder.Encode(root, _wafLibraryInvoker, argCache, applySafetyLimits: false);
            return configObj;
        }
    }
}
