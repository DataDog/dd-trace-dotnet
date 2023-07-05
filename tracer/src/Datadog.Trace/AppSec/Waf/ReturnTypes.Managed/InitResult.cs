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

        private InitResult(ushort failedToLoadRules, ushort loadedRules, string ruleFileVersion, IReadOnlyDictionary<string, object> errors, JToken? embeddedRules = null, bool unusableRuleFile = false, IntPtr? wafHandle = null, WafLibraryInvoker? wafLibraryInvoker = null)
        {
            HasErrors = errors.Count > 0;
            Errors = errors;
            EmbeddedRules = embeddedRules;
            FailedToLoadRules = failedToLoadRules;
            LoadedRules = loadedRules;
            RuleFileVersion = ruleFileVersion;
            UnusableRuleFile = unusableRuleFile;
            ErrorMessage = string.Empty;
            if (HasErrors)
            {
                ErrorMessage = JsonConvert.SerializeObject(errors);
            }

            if (!unusableRuleFile && wafHandle.HasValue && wafHandle.Value != IntPtr.Zero)
            {
                Waf = new Waf(wafHandle.Value, wafLibraryInvoker!);
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

        internal string RuleFileVersion { get; }

        internal bool Reported { get; set; }

        internal static InitResult FromUnusableRuleFile() => new(0, 0, string.Empty, new Dictionary<string, object>(), unusableRuleFile: true);

        internal static InitResult From(IntPtr diagnostics, IntPtr? wafHandle, WafLibraryInvoker? wafLibraryInvoker)
        {
            ushort failedCount = 0;
            ushort loadedCount = 0;
            string rulesetVersion = string.Empty;
            Dictionary<string, object>? errors = null;
            string errorMsg = string.Empty;
            try
            {
                if (diagnostics != IntPtr.Zero)
                {
                    var diagnosticsData = (Dictionary<string, object>)Encoder.Decode(new Obj(diagnostics));
                    if (diagnosticsData.Count > 0)
                    {
                        var rules = (Dictionary<string, object>)diagnosticsData["rules"];
                        failedCount = (ushort)((object[])rules["failed"]).Length;
                        loadedCount = (ushort)((object[])rules["loaded"]).Length;
                        errors = (Dictionary<string, object>)rules["errors"];
                        rulesetVersion = (string)diagnosticsData["ruleset_version"];
                    }
                }
            }
            catch
            {
                Log.Error("DDAS-0003-04: AppSec could not read the Waf diagnostics.");
            }

            return new(failedCount, loadedCount, rulesetVersion, errors ?? new(), wafHandle: wafHandle, wafLibraryInvoker: wafLibraryInvoker);
        }
    }
}
