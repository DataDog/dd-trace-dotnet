// <copyright file="DiagnosticResultUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;

#pragma warning disable SA1402 // File may only contain a single type

internal static class DiagnosticResultUtils
{
    internal static ReportedDiagnostics ExtractReportedDiagnostics(DdwafObjectStruct diagObject, bool noRuleDiagnoticsIsError)
    {
        ushort failedCount = 0;
        ushort loadedCount = 0;
        var rulesetVersion = string.Empty;
        IReadOnlyDictionary<string, object>? errors = null;
        try
        {
            if (diagObject.Type == DDWAF_OBJ_TYPE.DDWAF_OBJ_INVALID)
            {
                errors = new Dictionary<string, object> { { "diagnostics-error", "Waf didn't provide a valid diagnostics object at initialization, most likely due to an older waf version < 1.11.0" } };
                return new ReportedDiagnostics { FailedCount = failedCount, LoadedCount = loadedCount, RulesetVersion = rulesetVersion, Errors = errors };
            }

            var diagResult = new DiagnosticResult(diagObject);

            var rules = diagResult.Rules;
            if (rules == null)
            {
                if (noRuleDiagnoticsIsError)
                {
                    errors = new Dictionary<string, object> { { "diagnostics-error", "Waf could not provide diagnostics on rules or diagnostic format is incorrect" } };
                }

                return new ReportedDiagnostics { FailedCount = failedCount, LoadedCount = loadedCount, RulesetVersion = rulesetVersion, Errors = errors };
            }

            failedCount = (ushort)rules.Failed.Count;
            loadedCount = (ushort)rules.Loaded.Count;
            errors = rules.Errors;
            rulesetVersion = diagResult.RulesVersion ?? string.Empty;
        }
        catch (Exception err)
        {
            Log.Error(err, "AppSec could not read Waf diagnostics. Disabling AppSec");
            var localErrors = errors as Dictionary<string, object> ?? new Dictionary<string, object>();
            localErrors.Add("diagnostics-error", err.Message);
            errors = localErrors;
        }

        return new ReportedDiagnostics { FailedCount = failedCount, LoadedCount = loadedCount, RulesetVersion = rulesetVersion, Errors = errors };
    }
}

#pragma warning disable SA1201

internal struct ReportedDiagnostics
{
    public ushort FailedCount;
    public ushort LoadedCount;
    public string RulesetVersion;
    public IReadOnlyDictionary<string, object>? Errors;
}
