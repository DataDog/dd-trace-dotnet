// <copyright file="DiagnosticResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;

internal class DiagnosticResult
{
    private readonly Dictionary<string, object?> _diagnosticsData;
    private readonly Lazy<DiagnosticFeatureResult?> _customRules;
    private readonly Lazy<DiagnosticFeatureResult?> _exclusions;
    private readonly Lazy<DiagnosticFeatureResult?> _rules;
    private readonly Lazy<DiagnosticFeatureResult?> _rulesData;
    private readonly Lazy<DiagnosticFeatureResult?> _rulesOverride;

    public DiagnosticResult(DdwafObjectStruct diagObject)
    {
        _diagnosticsData = diagObject.DecodeMap();
        _customRules = MakeLazy("custom_rules");
        _exclusions = MakeLazy("exclusions");
        _rules = MakeLazy("rules");
        _rulesData = MakeLazy("rules_data");
        _rulesOverride = MakeLazy("rules_override");
    }

    public DiagnosticFeatureResult? CustomRules => _customRules.Value;

    public DiagnosticFeatureResult? Exclusions => _exclusions.Value;

    public DiagnosticFeatureResult? Rules => _rules.Value;

    public DiagnosticFeatureResult? RulesData => _rulesData.Value;

    public DiagnosticFeatureResult? RulesOverride => _rulesOverride.Value;

    public string? RulesVersion => _diagnosticsData["ruleset_version"] as string;

    private Lazy<DiagnosticFeatureResult?> MakeLazy(string key) => new(() => DiagnosticFeatureResult.From(key, _diagnosticsData));
}
