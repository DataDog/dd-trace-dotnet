// <copyright file="WafConfigurator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.AppSec.WafEncoding;
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
                        var conditionsArray = ev.Value<JArray>("conditions");
                        var addresses = conditionsArray?
                            .SelectMany(x => x.Value<JObject>("parameters")?.Value<JArray>("inputs") ?? [])
                            .ToList() ?? [];

                        Log.Debug("DDAS-0007-00: Loaded rule: {Id} - {Name} on addresses: {Addresses}", idProp.Value, nameProp.Value, string.Join(", ", addresses));
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

        /// <summary>
        /// Deserialize rules for the waf as Jtoken
        /// If null is passed, will deserialize embedded rule file in the app
        /// If a path is given but file isn't found, it won't fallback on the embedded rule file
        /// </summary>
        /// <param name="rulesFilePath">if null, will fallback on embedded rules file</param>
        /// <returns>the rules, might be null if file not found</returns>
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

        internal UpdateResult Configure(ConfigurationState configurationState, IEncoder encoder, ref DdwafConfigStruct configStruct, ref DdwafObjectStruct diagnostics, string? rulesFile)
        {
            return Update(_wafLibraryInvoker.InitBuilder(ref configStruct), configurationState, encoder, ref diagnostics, rulesFile, false);
        }

        internal UpdateResult Update(IntPtr wafBuilderHandle, ConfigurationState configurationState, IEncoder encoder, ref DdwafObjectStruct diagnostics, string? rulesFile = null, bool updating = true)
        {
            var wafHandle = IntPtr.Zero;
            if (wafBuilderHandle == IntPtr.Zero)
            {
                Log.Warning("DDAS-0005-00: WAF builder initialization failed."); // Check were all these error codes are defined
            }
            else
            {
                // Apply the stored configuration
                var configs = configurationState.GetWafConfigurations(updating);

                if (configs.HasData)
                {
                    if (configs.Removes != null)
                    {
                        foreach (var path in configs.Removes)
                        {
                            if (!_wafLibraryInvoker.BuilderRemoveConfig(wafBuilderHandle, path))
                            {
                                Log.Warning("DDAS-0005-00: WAF builder initialization failed. Config failed to load"); // Check were all these error codes are defined
                            }
                        }
                    }

                    if (configs.Updates != null)
                    {
                        foreach (var config in configs.Updates)
                        {
                            using (var encoded = encoder.Encode(config.Value, applySafetyLimits: config.Key != AsmDdProduct.DefaultConfigKey))
                            {
                                var configObj = encoded.ResultDdwafObject;
                                var path = config.Key;
                                if (!_wafLibraryInvoker.BuilderAddOrUpdateConfig(wafBuilderHandle, path, ref configObj, ref diagnostics))
                                {
                                    Log.Warning("DDAS-0005-00: WAF builder initialization failed. Config failed to load"); // Check were all these error codes are defined
                                }
                            }
                        }
                    }

                    wafHandle = _wafLibraryInvoker.BuilderBuildInstance(wafBuilderHandle);
                    if (wafHandle == IntPtr.Zero)
                    {
                        Log.Warning("DDAS-0005-00: WAF initialization failed.");
                    }
                }
                else if (!updating)
                {
                    Log.Warning("DDAS-0005-00: WAF initialization failed. No valid rules found.");
                    return UpdateResult.FromFailed("DDAS-0005-00: WAF initialization failed. No valid rules found.");
                }
            }

            var result = UpdateResult.FromSuccess(diagnostics, wafBuilderHandle, wafHandle, _wafLibraryInvoker, encoder);
            if (result.Errors is { Count: > 0 } || result.Warnings is { Count: > 0 })
            {
                var sb = StringBuilderCache.Acquire();
                if (result.Errors is { Count: > 0 })
                {
                    foreach (var item in result.Errors)
                    {
                        sb.Append($"ERR: {item.Key}: [{string.Join(", ", item.Value)}] ");
                    }
                }

                if (result.Warnings is { Count: > 0 })
                {
                    foreach (var item in result.Warnings!)
                    {
                        sb.Append($"WRN: {item.Key}: [{string.Join(", ", item.Value)}] ");
                    }
                }

                var errorMess = StringBuilderCache.GetStringAndRelease(sb);
                Log.Warning("Some issues were found while applying waf configuration (RulesFile: {RulesFile}): {ErroringRules}", rulesFile, errorMess);
            }

            return result;
        }
    }
}
