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

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class InitResult
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InitResult));

        private InitResult(ushort failedToLoadRules, ushort loadedRules, string ruleFileVersion, IReadOnlyDictionary<string, object> errors, bool unusableRuleFile = false, IntPtr wafBuilderHandle = default(IntPtr), IntPtr wafHandle = default(IntPtr), WafLibraryInvoker? wafLibraryInvoker = null, IEncoder? encoder = null, bool shouldEnableWaf = true, bool incompatibleWaf = false)
        {
            HasErrors = errors.Count > 0;
            Errors = errors;
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

            shouldEnableWaf &= !incompatibleWaf && !unusableRuleFile && wafBuilderHandle != IntPtr.Zero && wafHandle != IntPtr.Zero;
            if (shouldEnableWaf)
            {
                Waf = new Waf(wafBuilderHandle, wafHandle, wafLibraryInvoker!, encoder!);
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

        internal string ErrorMessage { get; }

        internal bool HasErrors { get; }

        internal bool UnusableRuleFile { get; }

        internal bool IncompatibleWaf { get; }

        internal string RuleFileVersion { get; }

        internal bool Reported { get; set; }

        internal static InitResult FromUnusableRuleFile() => new(0, 0, string.Empty, new Dictionary<string, object>(), unusableRuleFile: true);

        internal static InitResult FromIncompatibleWaf() => new(0, 0, string.Empty, new Dictionary<string, object>(), incompatibleWaf: true);

        internal static InitResult From(ref DdwafObjectStruct diagObject, IntPtr wafBuilderHandle, IntPtr wafHandle, WafLibraryInvoker? wafLibraryInvoker, IEncoder encoder)
        {
            var reportedDiag = DiagnosticResultUtils.ExtractReportedDiagnostics(diagObject, true);

            return new(reportedDiag.FailedCount, reportedDiag.LoadedCount, reportedDiag.RulesetVersion, reportedDiag.Errors ?? new Dictionary<string, object>(), wafBuilderHandle: wafBuilderHandle, wafHandle: wafHandle, wafLibraryInvoker: wafLibraryInvoker, encoder: encoder);
        }

        internal static InitResult From(ref UpdateResult result)
        {
            return new(result.FailedToLoadRules ?? 0, result.LoadedRules ?? 0, result.RuleFileVersion ?? string.Empty, result.Errors ?? new Dictionary<string, object>(), result.UnusableRules, result.WafBuilderHandle, result.WafHandle, result.WafLibraryInvoker, result.Encoder);
        }
    }
}
