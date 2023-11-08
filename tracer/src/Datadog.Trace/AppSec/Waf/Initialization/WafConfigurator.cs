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

        public bool CheckVersionCompatibility()
        {
            var versionWaf = _wafLibraryInvoker.GetVersion();
            var versionWafSplit = versionWaf.Split('.');
            if (versionWafSplit.Length != 3)
            {
                Log.Warning("Waf version {WafVersion} has a non expected format", versionWaf);
                return false;
            }

            var canParse = int.TryParse(versionWafSplit[1], out var wafMinor);
            canParse &= int.TryParse(versionWafSplit[0], out var wafMajor);
            var tracerVersion = GetType().Assembly.GetName().Version;
            if (tracerVersion is null || !canParse)
            {
                Log.Warning("Waf version {WafVersion} or tracer version {TracerVersion} have a non expected format", versionWaf, tracerVersion);
                return false;
            }

            // tracer >= 2.34.0 needs waf >= 1.11 cause it passes a ddwafobject for diagnostics instead of a ruleset info struct which causes unpredictable unmanaged crashes
            if ((tracerVersion is { Minor: >= 34, Major: >= 2 } && wafMajor == 1 && wafMinor <= 10) ||
                (tracerVersion is { Minor: >= 38, Major: >= 2 } && wafMajor == 1 && wafMinor < 13))
            {
                Log.Warning("Waf version {WafVersion} is not compatible with tracer version {TracerVersion}", versionWaf, tracerVersion);
                return false;
            }

            return true;
        }

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
            return assembly.GetManifestResourceStream("Datadog.Trace.AppSec.Waf.ConfigFiles.rule-set.json");
        }

        private static Stream? GetSchemaExtractionConfigStream()
        {
            var assembly = typeof(Waf).Assembly;
            return assembly.GetManifestResourceStream("Datadog.Trace.AppSec.Waf.ConfigFiles.apisecurity-config.json");
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

        internal static JToken? DeserializeSchemaExtractionConfig()
        {
            using var stream = GetSchemaExtractionConfigStream();
            if (stream == null)
            {
                return null;
            }

            using var reader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(reader);
            var root = JToken.ReadFrom(jsonReader);
            return root;
        }

        internal static JToken? DeserializeEmbeddedOrStaticRules(string? rulesFilePath)
        {
            JToken root;
            try
            {
                using var stream = GetRulesStream(rulesFilePath);

                if (stream == null)
                {
                    return null;
                }

                using var reader = new StreamReader(stream);
                using var jsonReader = new JsonTextReader(reader);
                root = JToken.ReadFrom(jsonReader);
                LogRuleDetailsIfDebugEnabled(root);
            }
            catch (Exception ex)
            {
                if (rulesFilePath != null)
                {
                    Log.Error(ex, "DDAS-0003-02: AppSec could not read the rule file \"{RulesFile}\". Reason: Invalid file format. AppSec will not run any protections in this application.", rulesFilePath);
                }
                else
                {
                    Log.Error(ex, "DDAS-0003-02: AppSec could not read the rule file embedded in the manifest. Reason: Invalid file format. AppSec will not run any protections in this application.");
                }

                return null;
            }

            return root;
        }

        internal InitResult ConfigureAndDispose(DdwafObjectStruct? rulesObj, string? rulesFile, string obfuscationParameterKeyRegex, string obfuscationParameterValueRegex)
        {
            if (rulesObj == null)
            {
                Log.Error("Waf couldn't initialize properly because of an unusable rule file. If you set the environment variable {AppsecruleEnv}, check the path and content of the file are correct.", ConfigurationKeys.AppSec.Rules);
                return InitResult.FromUnusableRuleFile();
            }

            var keyRegex = IntPtr.Zero;
            var valueRegex = IntPtr.Zero;
            var diagnostics = new DdwafObjectStruct { Type = DDWAF_OBJ_TYPE.DDWAF_OBJ_MAP };

            try
            {
                DdwafConfigStruct args = default;
                keyRegex = Marshal.StringToHGlobalAnsi(obfuscationParameterKeyRegex);
                valueRegex = Marshal.StringToHGlobalAnsi(obfuscationParameterValueRegex);
                args.KeyRegex = keyRegex;
                args.ValueRegex = valueRegex;
                args.FreeWafFunction = IntPtr.Zero;

                var rules = rulesObj.Value;
                var wafHandle = _wafLibraryInvoker.Init(ref rules, ref args, ref diagnostics);
                if (wafHandle == IntPtr.Zero)
                {
                    Log.Warning("DDAS-0005-00: WAF initialization failed.");
                }

                var initResult = InitResult.From(diagnostics, wafHandle, _wafLibraryInvoker);
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
                else
                {
                    // sometimes loaded rules will be 0 if other errors happen above, that's why it should be the fallback log
                    if (initResult.LoadedRules == 0)
                    {
                        Log.Error("DDAS-0003-03: AppSec could not read the rule file {RulesFile}. Reason: All rules are invalid. AppSec will not run any protections in this application.", rulesFile);
                    }
                    else
                    {
                        Log.Information("DDAS-0015-00: AppSec loaded {LoadedRules} rules from file {RulesFile}.", initResult.LoadedRules, rulesFile);
                    }
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

                if (diagnostics.Array != IntPtr.Zero)
                {
                    _wafLibraryInvoker.ObjectFreePtr(ref diagnostics.Array);
                }
            }
        }
    }
}
