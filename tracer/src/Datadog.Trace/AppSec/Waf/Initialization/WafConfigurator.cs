// <copyright file="WafConfigurator.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Logging;
using Datadog.Trace.Util;
using Datadog.Trace.Util.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using Datadog.Trace.Vendors.Serilog.Events;

#nullable enable

namespace Datadog.Trace.AppSec.Waf.Initialization
{
    internal sealed class WafConfigurator
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
                using var jsonReader = new JsonTextReader(reader) { ArrayPool = JsonArrayPool.Shared };
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
                Log.Error("rc::asm_dd::diagnostic Error: WAF builder initialization failed."); // Check were all these error codes are defined
                return UpdateResult.FromFailed("rc::asm_dd::diagnostic Error: WAF builder initialization failed.");
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
                            Log.Debug("WAF: Removing config: {Path}", path);

                            if (!_wafLibraryInvoker.BuilderRemoveConfig(wafBuilderHandle, path))
                            {
                                Log.Debug("WAF builder: Config failed to be removed : {0}", path); // Check were all these error codes are defined
                            }
                        }
                    }

                    if (configs.Updates != null)
                    {
                        foreach (var config in configs.Updates)
                        {
                            Log.Debug("WAF: Applying config: {Path}", config.Key);

                            using (var encoded = encoder.Encode(config.Value, applySafetyLimits: false))
                            {
                                var configObj = encoded.ResultDdwafObject;
                                var path = config.Key;
                                if (!_wafLibraryInvoker.BuilderAddOrUpdateConfig(wafBuilderHandle, path, ref configObj, ref diagnostics))
                                {
                                    Log.Debug("WAF builder: Config failed to load : {0}", path); // Check were all these error codes are defined
                                }
                            }
                        }
                    }

                    wafHandle = _wafLibraryInvoker.BuilderBuildInstance(wafBuilderHandle);
                }
            }

            UpdateResult result;

            if (wafHandle == IntPtr.Zero)
            {
                Log.Error("rc::asm_dd::diagnostic Error: Failed to build WAF instance: no valid rules or processors available");
                result = UpdateResult.FromFailed("DDAS-0005-00: WAF initialization failed. No valid rules found.", diagnostics, wafBuilderHandle, _wafLibraryInvoker, encoder);
            }
            else
            {
                result = UpdateResult.FromSuccess(diagnostics, wafBuilderHandle, wafHandle, _wafLibraryInvoker, encoder);
            }

            if (result.ReportedDiagnostics.Rules.Errors is { Count: > 0 } ||
                result.ReportedDiagnostics.Rules.Warnings is { Count: > 0 } ||
                result.ReportedDiagnostics.Rest.Errors is { Count: > 0 } ||
                result.ReportedDiagnostics.Rest.Warnings is { Count: > 0 })
            {
                var diags = result.ReportedDiagnostics;
                DumpStatsMessages(ref diags.Rules);
                DumpStatsMessages(ref diags.Rest);

                if (diags.HasErrors)
                {
#pragma warning disable DDLOG004 // Message templates should be constant
                    Log.Error($"Some errors were found while applying waf configuration (RulesFile: {rulesFile})");
#pragma warning restore DDLOG004 // Message templates should be constant
                }
                else
                {
                    Log.Debug("Some warnings were found while applying waf configuration (RulesFile: {RulesFile})", rulesFile);
                }

                void DumpStatsMessages(ref WafStats stats)
                {
                    DumpMessages(stats.Errors, true);
                    DumpMessages(stats.Warnings, false);
                }

                void DumpMessages(IReadOnlyDictionary<string, object>? messages, bool isError)
                {
                    if (messages is { Count: > 0 })
                    {
                        foreach (var item in messages)
                        {
                            var message = $"{item.Key}: [{string.Join(", ", item.Value)}]";
                            var severity = isError ? "Error" : "Warning";
#pragma warning disable DDLOG004 // Message templates should be constant
                            Log.Error($"rc::asm_dd::diagnostic {severity}: {message}");
#pragma warning restore DDLOG004 // Message templates should be constant
                        }
                    }
                }
            }

            if (result.Success && !updating)
            {
                Log.Information("DDAS-0015-00: AppSec loaded {LoadedRules} rules from file {RulesFile}.", result.ReportedDiagnostics.Rules.Loaded, rulesFile ?? "Embedded rules file");
                Log.Debug("                          WAF config stats: {LoadedRules} loaded, {SkippedRules} skipped, {FailedRules} failed items", result.ReportedDiagnostics.Rest.Loaded, result.ReportedDiagnostics.Rest.Skipped, result.ReportedDiagnostics.Rest.Failed);
            }

            return result;
        }
    }
}
