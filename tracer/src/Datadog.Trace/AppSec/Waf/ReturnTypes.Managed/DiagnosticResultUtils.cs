// <copyright file="DiagnosticResultUtils.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.Vendors.dnlib.IO;
using Datadog.Trace.Vendors.MessagePack;
using Datadog.Trace.Vendors.MessagePack.Resolvers;
using Datadog.Trace.Vendors.Serilog;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;

#pragma warning disable SA1201 // A struct should not follow a class
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1402 // File may only contain a single type

internal static class DiagnosticResultUtils
{
    internal static ReportedDiagnostics ExtractReportedDiagnostics(DdwafObjectStruct diagObject, bool noRuleDiagnoticsIsError)
    {
        WafStats total = new();
        WafStats rules = new();
        var rulesetVersion = string.Empty;
        Dictionary<string, object>? errors = null;
        Dictionary<string, object>? warnings = null;
        try
        {
            if (diagObject.Type == DDWAF_OBJ_TYPE.DDWAF_OBJ_INVALID)
            {
                errors = new Dictionary<string, object> { { "diagnostics-error", "Waf didn't provide a valid diagnostics object at initialization, most likely due to an older waf version < 1.11.0" } };
                return new ReportedDiagnostics { Errors = errors };
            }

            var diagResult = new DiagnosticResult(diagObject);
            if (diagResult.Rules is not null)
            {
                total.LoadedCount = (ushort)(diagResult.Rules.Loaded?.Count ?? 0);
                total.SkippedCount = (ushort)(diagResult.Rules.Skipped?.Count ?? 0);
                total.FailedCount = (ushort)(diagResult.Rules.Failed?.Count ?? 0);

                if (diagResult.Rules.Errors is { Count: > 0 })
                {
                    if (errors is null) { errors = new Dictionary<string, object>(); }
                    errors = diagResult.Rules.Errors.ToDictionary(p => "rule: " + p.Key, p => p.Value);
                }
            }

            rules = UpdateStats(diagResult.Rules, "rules");
            UpdateStats(diagResult.RulesData, "rules_data");
            UpdateStats(diagResult.Actions, "actions");
            UpdateStats(diagResult.RulesOverride, "rules_override");
            UpdateStats(diagResult.Exclusions, "exclusions");
            UpdateStats(diagResult.CustomRules, "custom_rules");

            if (noRuleDiagnoticsIsError && rules.TotalCount == 0)
            {
                errors = new Dictionary<string, object> { { "diagnostics-error", "Waf could not provide diagnostics on rules or diagnostic format is incorrect" } };
            }

            rulesetVersion = diagResult.RulesetVersion;
        }
        catch (Exception err)
        {
            Log.Error(err, "AppSec could not read Waf diagnostics. Disabling AppSec");
            var localErrors = errors as Dictionary<string, object> ?? new Dictionary<string, object>();
            localErrors.Add("diagnostics-error", err.Message);
            errors = localErrors;
        }

        return new ReportedDiagnostics { Rules = rules, Total = total, RulesetVersion = rulesetVersion, Errors = errors, Warnings = warnings };

        WafStats UpdateStats(DiagnosticFeatureResult? feature, string name)
        {
            WafStats res = new();
            if (feature is not null)
            {
                res.LoadedCount = (ushort)(feature.Loaded?.Count ?? 0);
                res.SkippedCount = (ushort)(feature.Skipped?.Count ?? 0);
                res.FailedCount = (ushort)(feature.Failed?.Count ?? 0);

                total.LoadedCount += res.LoadedCount;
                total.SkippedCount += res.SkippedCount;
                total.FailedCount += res.FailedCount;

                if (feature.Errors is { Count: > 0 })
                {
                    errors = Merge(errors, feature.Errors, name);
                }

                if (feature.Warnings is { Count: > 0 })
                {
                    warnings = Merge(warnings, feature.Warnings, name);
                }
            }

            return res;

            Dictionary<string, object>? Merge(Dictionary<string, object>? d1, IReadOnlyDictionary<string, object>? d2, string prefix)
            {
                if (d2 is null) { return d1; }
                if (d1 is null) { d1 = new(); }

                foreach (var p in d2)
                {
                    d1[$"{prefix}: {p.Key}"] = p.Value;
                }

                return d1;
            }
        }
    }
}

internal struct ReportedDiagnostics
{
    public WafStats Total = new();
    public WafStats Rules = new();
    public string RulesetVersion = string.Empty;
    public IReadOnlyDictionary<string, object>? Errors = null;
    public IReadOnlyDictionary<string, object>? Warnings = null;

    public ReportedDiagnostics()
    {
    }
}

internal struct WafStats
{
    public ushort FailedCount = 0;
    public ushort LoadedCount = 0;
    public ushort SkippedCount = 0;

    public WafStats()
    {
    }

    public int TotalCount => (FailedCount + LoadedCount + SkippedCount);
}

#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore SA1201 // A struct should not follow a class
