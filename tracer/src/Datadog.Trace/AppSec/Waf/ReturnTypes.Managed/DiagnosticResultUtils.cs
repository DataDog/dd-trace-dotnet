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

#pragma warning disable SA1201 // A struct should not follow a class
#pragma warning disable SA1401 // Fields should be private
#pragma warning disable SA1402 // File may only contain a single type

internal static class DiagnosticResultUtils
{
    internal static ReportedDiagnostics ExtractReportedDiagnostics(in DdwafObjectStruct diagObject, bool noRuleDiagnoticsIsError)
    {
        WafStats rules = new(); // Rules only stats
        var rulesetVersion = string.Empty;
        WafStats rest = new(); // Rest of the ites stats
        Dictionary<string, object>? errors = null;
        Dictionary<string, object>? warnings = null;
        try
        {
            if (diagObject.Type == DDWAF_OBJ_TYPE.DDWAF_OBJ_INVALID)
            {
                errors = new Dictionary<string, object> { { "diagnostics-error", "Waf didn't provide a valid diagnostics object at initialization, most likely due to an older waf version < 1.11.0" } };
                return new ReportedDiagnostics(errors);
            }

            var diagResult = new DiagnosticResult(diagObject);
            if (diagResult.Rules is not null)
            {
                rules.Loaded = (ushort)(diagResult.Rules.Loaded?.Count ?? 0);
                rules.Skipped = (ushort)(diagResult.Rules.Skipped?.Count ?? 0);
                rules.Failed = (ushort)(diagResult.Rules.Failed?.Count ?? 0);

                rules.Errors = diagResult.Rules.Errors;
                rules.Warnings = diagResult.Rules.Warnings;
            }

            UpdateStats(diagResult.RulesData, "rules_data");
            UpdateStats(diagResult.Actions, "actions");
            UpdateStats(diagResult.RulesOverride, "rules_override");
            UpdateStats(diagResult.Exclusions, "exclusions");
            UpdateStats(diagResult.CustomRules, "custom_rules");

            rest.Errors = errors;
            rest.Warnings = warnings;

            if (noRuleDiagnoticsIsError && rules.Total == 0)
            {
                rules.Errors = new Dictionary<string, object> { { "diagnostics-error", "Waf could not provide diagnostics on rules or diagnostic format is incorrect" } };
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

        return new ReportedDiagnostics { Rules = rules, RulesetVersion = rulesetVersion, Rest = rest };

        WafStats UpdateStats(DiagnosticFeatureResult? feature, string name)
        {
            WafStats res = new();
            if (feature is not null)
            {
                res.Loaded = (ushort)(feature.Loaded?.Count ?? 0);
                res.Skipped = (ushort)(feature.Skipped?.Count ?? 0);
                res.Failed = (ushort)(feature.Failed?.Count ?? 0);

                rest.Loaded += res.Loaded;
                rest.Skipped += res.Skipped;
                rest.Failed += res.Failed;

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
    public string RulesetVersion = string.Empty;
    public WafStats Rules = new();
    public WafStats Rest = new();

    public ReportedDiagnostics()
    {
    }

    public ReportedDiagnostics(IReadOnlyDictionary<string, object> errors)
    {
        Rules.Errors = errors;
    }

    public bool HasErrors => Rules.HasErrors || Rest.HasErrors;
}

internal struct WafStats
{
    public ushort Failed = 0;
    public ushort Loaded = 0;
    public ushort Skipped = 0;

    public IReadOnlyDictionary<string, object>? Errors = null;
    public IReadOnlyDictionary<string, object>? Warnings = null;

    public WafStats()
    {
    }

    public int Total => (Failed + Loaded + Skipped);

    public bool HasErrors => Errors is { Count: > 0 } || Warnings is { Count: > 0 };
}

#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore SA1401 // Fields should be private
#pragma warning restore SA1201 // A struct should not follow a class
