// <copyright file="InitResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class InitResult
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InitResult));

        private InitResult(ushort failedToLoadRules, ushort loadedRules, string ruleFileVersion, IReadOnlyDictionary<string, object> errors, JToken? embeddedRules = null, bool unusableRuleFile = false, IntPtr? wafHandle = null, WafLibraryInvoker? wafLibraryInvoker = null, bool shouldEnableWaf = true, bool incompatibleWaf = false)
        {
            HasErrors = errors.Count > 0;
            Errors = errors;
            EmbeddedRules = embeddedRules;
            FailedToLoadRules = failedToLoadRules;
            LoadedRules = loadedRules;
            RuleFileVersion = ruleFileVersion;
            UnusableRuleFile = unusableRuleFile;
            ErrorMessage = string.Empty;
            IncompatibleWaf = incompatibleWaf;
            if (HasErrors)
            {
                ErrorMessage = JsonConvert.SerializeObject(errors);
            }

            shouldEnableWaf &= !incompatibleWaf && !unusableRuleFile && wafHandle.HasValue && wafHandle.Value != IntPtr.Zero;
            if (shouldEnableWaf)
            {
                Waf = new Waf(wafHandle!.Value, wafLibraryInvoker!);
                Success = true;
            }
        }

        internal bool Success { get; }

        internal Waf? Waf { get; }

        internal ushort FailedToLoadRules { get; }

        /// <summary>
        /// Gets the number of rules successfully loaded
        /// </summary>
        internal ushort LoadedRules { get; }

        internal IReadOnlyDictionary<string, object> Errors { get; }

        public JToken? EmbeddedRules { get; set; }

        internal string ErrorMessage { get; }

        internal bool HasErrors { get; }

        internal bool UnusableRuleFile { get; }

        internal bool IncompatibleWaf { get; }

        internal string RuleFileVersion { get; }

        internal bool Reported { get; set; }

        internal static InitResult FromUnusableRuleFile() => new(0, 0, string.Empty, new Dictionary<string, object>(), unusableRuleFile: true);

        internal static InitResult FromIncompatibleWaf() => new(0, 0, string.Empty, new Dictionary<string, object>(), incompatibleWaf: true);

        internal static InitResult From(IntPtr diagnostics, IntPtr? wafHandle, WafLibraryInvoker? wafLibraryInvoker)
        {
            ushort failedCount = 0;
            ushort loadedCount = 0;
            string rulesetVersion = string.Empty;
            Dictionary<string, object>? errors = null;
            try
            {
                if (diagnostics != IntPtr.Zero)
                {
                    var diagObject = Obj.Wrap(diagnostics);  // Do not free the pointer on dispose
                    if (diagObject.ArgsType == ObjType.Invalid)
                    {
                        errors = new Dictionary<string, object> { { "diagnostics-error", "Waf didn't provide a valid diagnostics object at initialization, most likely due to an older waf version < 1.11.0" } };
                        return new(failedCount, loadedCount, rulesetVersion, errors, wafHandle: wafHandle, wafLibraryInvoker: wafLibraryInvoker, shouldEnableWaf: false);
                    }

                    var diagnosticsData = (Dictionary<string, object>)Encoder.Decode(diagObject);
                    if (diagnosticsData.Count > 0)
                    {
                        var valueExist = diagnosticsData.TryGetValue("rules", out var rulesObj);
                        if (!valueExist)
                        {
                            errors = new Dictionary<string, object> { { "diagnostics-error", "Waf could not provide diagnostics on rules" } };
                            return new(failedCount, loadedCount, rulesetVersion, errors, wafHandle: wafHandle, wafLibraryInvoker: wafLibraryInvoker, shouldEnableWaf: false);
                        }

                        var rules = rulesObj as Dictionary<string, object>;
                        if (rules == null)
                        {
                            errors = new Dictionary<string, object> { { "diagnostics-error", "Waf could not provide diagnostics on rules as a dictionary key-value" } };
                            return new(failedCount, loadedCount, rulesetVersion, errors, wafHandle: wafHandle, wafLibraryInvoker: wafLibraryInvoker, shouldEnableWaf: false);
                        }

                        failedCount = (ushort)((object[])rules["failed"]).Length;
                        loadedCount = (ushort)((object[])rules["loaded"]).Length;
                        errors = (Dictionary<string, object>)rules["errors"];
                        rulesetVersion = (string)diagnosticsData["ruleset_version"];
                    }
                }
            }
            catch (Exception err)
            {
                Log.Error(err, "AppSec could not read Waf diagnostics. Disabling AppSec");
                errors ??= new Dictionary<string, object>();
                errors.Add("diagnostics-error", err.Message);
            }

            return new(failedCount, loadedCount, rulesetVersion, errors ?? new(), wafHandle: wafHandle, wafLibraryInvoker: wafLibraryInvoker);
        }
    }
}
