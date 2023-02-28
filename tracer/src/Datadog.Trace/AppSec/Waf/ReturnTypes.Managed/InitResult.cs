// <copyright file="InitResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Logging;
using Datadog.Trace.Vendors.Newtonsoft.Json;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed
{
    internal class InitResult
    {
        private static readonly IDatadogLogger Log = DatadogLogging.GetLoggerFor(typeof(InitResult));

        private InitResult(ushort failedToLoadRules, ushort loadedRules, string ruleFileVersion, IReadOnlyDictionary<string, string[]> errors, bool unusableRuleFile = false, IntPtr? wafHandle = null, WafLibraryInvoker? wafLibraryInvoker = null)
        {
            HasErrors = errors.Count > 0;
            Errors = errors;
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

        internal IReadOnlyDictionary<string, string[]> Errors { get; }

        internal string ErrorMessage { get; }

        internal bool HasErrors { get; }

        internal bool UnusableRuleFile { get; }

        internal string RuleFileVersion { get; }

        internal bool Reported { get; set; }

        internal static InitResult FromUnusableRuleFile() => new(0, 0, string.Empty, new Dictionary<string, string[]>(), unusableRuleFile: true);

        internal static InitResult From(DdwafRuleSetInfo ddwaRuleSetInfo, IntPtr? wafHandle, WafLibraryInvoker wafLibraryInvoker)
        {
            var ddwafObjectStruct = ddwaRuleSetInfo.Errors;
            var errors = ddwafObjectStruct.Decode();
            var ruleFileVersion = Marshal.PtrToStringAnsi(ddwaRuleSetInfo.Version);
            return new(ddwaRuleSetInfo.Failed, ddwaRuleSetInfo.Loaded, ruleFileVersion!, errors, wafHandle: wafHandle, wafLibraryInvoker: wafLibraryInvoker);
        }
    }
}
