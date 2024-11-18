// <copyright file="WafLibraryRequiredTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.Asm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Rcm.Models.AsmDd;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.Waf.NativeBindings;
using Datadog.Trace.AppSec.Waf.ReturnTypes.Managed;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Moq;
using Xunit;
using Action = Datadog.Trace.AppSec.Rcm.Models.Asm.Action;

namespace Datadog.Trace.Security.Unit.Tests.Utils;

[Collection(nameof(SecuritySequentialTests))]
public class WafLibraryRequiredTest : SettingsTestsBase
{
    /// <summary>
    /// 15 seconds timeout for the waf. It shouldn't happen, but with a 1sec timeout, the tests are flaky.
    /// </summary>
    public const int TimeoutMicroSeconds = 15_000_000;

    static WafLibraryRequiredTest()
    {
        var result = WafLibraryInvoker.Initialize();
        WafLibraryInvoker = result.WafLibraryInvoker;
    }

    internal static WafLibraryInvoker? WafLibraryInvoker { get; }

    internal static ConfigurationState CreateConfigurationState(string? ruleFile, Dictionary<string, RuleOverride[]>? ruleOverrides = null, Dictionary<string, RuleData[]>? rulesData = null, Dictionary<string, RuleSet>? ruleSet = null, Dictionary<string, Action[]>? actions = null)
    {
        return CreateConfigurationState(creation: true, ruleFile, ruleOverrides, rulesData, ruleSet, actions);
    }

    internal static ConfigurationState UpdateConfigurationState(string? ruleFile = null, Dictionary<string, RuleOverride[]>? ruleOverrides = null, Dictionary<string, RuleData[]>? rulesData = null, Dictionary<string, RuleSet>? ruleSet = null, Dictionary<string, Action[]>? actions = null)
    {
        return CreateConfigurationState(creation: false, ruleFile, ruleOverrides, rulesData, ruleSet, actions);
    }

    internal InitResult CreateWaf(bool useUnsafeEncoder = false, string? ruleFile = null, string? obfuscationParameterKeyRegex = null, string? obfuscationParameterValueRegex = null, bool expectWafNull = false)
    {
        var configurationState = CreateConfigurationState(ruleFile);
        return CreateWaf(configurationState, useUnsafeEncoder, ruleFile, obfuscationParameterKeyRegex, obfuscationParameterValueRegex, expectWafNull);
    }

    internal InitResult CreateWaf(ConfigurationState configurationState, bool useUnsafeEncoder = false, string? ruleFile = null, string? obfuscationParameterKeyRegex = null, string? obfuscationParameterValueRegex = null, bool expectWafNull = false)
    {
        var initResult = Waf.Create(
            WafLibraryInvoker!,
            obfuscationParameterKeyRegex ?? string.Empty,
            obfuscationParameterValueRegex ?? string.Empty,
            configurationState,
            useUnsafeEncoder: useUnsafeEncoder);
        initResult.Should().NotBeNull();
        if (expectWafNull)
        {
            initResult.Waf.Should().BeNull();
        }
        else
        {
            initResult.Waf.Should().NotBeNull();
        }

        return initResult;
    }

    private static ConfigurationState CreateConfigurationState(bool creation, string? ruleFile = null, Dictionary<string, RuleOverride[]>? ruleOverrides = null, Dictionary<string, RuleData[]>? rulesData = null, Dictionary<string, RuleSet>? ruleSet = null, Dictionary<string, Action[]>? actions = null)
    {
        var source = CreateConfigurationSource((ConfigurationKeys.AppSec.Rules, ruleFile), (ConfigurationKeys.AppSec.Enabled, "1"));
        var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);
        var configurationState = new ConfigurationState(settings, creation, ruleSet, rulesData, ruleOverrides, actions);
        return configurationState;
    }
}
