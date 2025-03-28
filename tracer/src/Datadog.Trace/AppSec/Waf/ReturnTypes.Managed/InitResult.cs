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

        private InitResult(ref UpdateResult updateResult)
        {
            UpdateResult = updateResult;
            if (updateResult.WafBuilderHandle != IntPtr.Zero && updateResult.WafHandle != IntPtr.Zero && updateResult.Success && updateResult.ReportedDiagnostics.Rules.Loaded > 0)
            {
                Waf = new Waf(updateResult.WafBuilderHandle, updateResult.WafHandle, updateResult.WafLibraryInvoker!, updateResult.Encoder!);
            }
        }

        internal UpdateResult UpdateResult { get; }

        internal Waf? Waf { get; } = null;

        internal bool Success => Waf is not null;

        /// <summary>
        /// Gets the number of rules successfully loaded
        /// </summary>
        internal ushort LoadedRules => UpdateResult.ReportedDiagnostics.Rules.Loaded;

        internal ushort FailedToLoadRules => UpdateResult.ReportedDiagnostics.Rules.Failed;

        internal IReadOnlyDictionary<string, object>? Errors => UpdateResult.RuleErrors;

        internal string ErrorMessage => UpdateResult.ErrorMessage;

        internal bool HasErrors => UpdateResult.HasRuleErrors;

        internal bool UnusableRuleFile => UpdateResult.ReportedDiagnostics.Rest.Total == 0;

        // internal bool IncompatibleWaf { get; }

        internal string RuleFileVersion => UpdateResult.RuleFileVersion;

        internal bool Reported { get; set; }

        internal static InitResult From(ref UpdateResult result)
        {
            return new(ref result);
        }
    }
}
