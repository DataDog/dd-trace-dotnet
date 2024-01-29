// <copyright file="InitResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class InitResult
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InitResult));

        private InitResult(ushort failedToLoadRules, ushort loadedRules, string ruleFileVersion, IReadOnlyDictionary<string, object> errors, JToken? embeddedRules = null, bool unusableRuleFile = false, IntPtr? wafHandle = null, WafLibraryInvoker? wafLibraryInvoker = null, IEncoder? encoder = null, bool shouldEnableWaf = true, bool incompatibleWaf = false)
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
                Waf = new Waf(wafHandle!.Value, wafLibraryInvoker!, encoder!);
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

        internal static InitResult From(DdwafObjectStruct diagObject, IntPtr? wafHandle, WafLibraryInvoker? wafLibraryInvoker, IEncoder encoder)
        {
            var reportedDiag = DiagnosticResultUtils.ExtractReportedDiagnostics(diagObject, true);

            return new(reportedDiag.FailedCount, reportedDiag.LoadedCount, reportedDiag.RulesetVersion, reportedDiag.Errors ?? new Dictionary<string, object>(), wafHandle: wafHandle, wafLibraryInvoker: wafLibraryInvoker, encoder: encoder);
        }
    }
}
