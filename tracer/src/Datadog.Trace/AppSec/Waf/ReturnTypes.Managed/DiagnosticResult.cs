// <copyright file="DiagnosticResult.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.Waf.NativeBindings;

namespace Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;

internal sealed class DiagnosticResult
{
    private readonly Dictionary<string, object?> _diagnosticsData;
    private readonly DiagnosticFeatureResult? _actions;
    private readonly DiagnosticFeatureResult? _customRules;
    private readonly DiagnosticFeatureResult? _exclusions;
    private readonly DiagnosticFeatureResult? _rules;
    private readonly DiagnosticFeatureResult? _rulesData;
    private readonly DiagnosticFeatureResult? _rulesOverride;
    private readonly string _rulesetVersion = string.Empty;

    public DiagnosticResult(in DdwafObjectStruct diagObject)
    {
        _diagnosticsData = diagObject.DecodeMap();
        _actions = DiagnosticFeatureResult.From("actions", _diagnosticsData);
        _customRules = DiagnosticFeatureResult.From("custom_rules", _diagnosticsData);
        _exclusions = DiagnosticFeatureResult.From("exclusions", _diagnosticsData);
        _rules = DiagnosticFeatureResult.From("rules", _diagnosticsData);
        _rulesData = DiagnosticFeatureResult.From("rules_data", _diagnosticsData);
        _rulesOverride = DiagnosticFeatureResult.From("rules_override", _diagnosticsData);
        if (_diagnosticsData.TryGetValue("ruleset_version", out var value))
        {
            _rulesetVersion = value as string ?? string.Empty;
        }
    }

    public DiagnosticFeatureResult? Actions => _actions;

    public DiagnosticFeatureResult? CustomRules => _customRules;

    public DiagnosticFeatureResult? Exclusions => _exclusions;

    public DiagnosticFeatureResult? Rules => _rules;

    public DiagnosticFeatureResult? RulesData => _rulesData;

    public DiagnosticFeatureResult? RulesOverride => _rulesOverride;

    public string RulesetVersion => _rulesetVersion;
}
