// <copyright file="WafLibraryRequiredTest.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
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
        var result = WafLibraryInvoker.Initialize(Environment.GetEnvironmentVariable(ConfigurationKeys.DotnetTracerHome), Environment.GetEnvironmentVariable(ConfigurationKeys.InternalTraceNativeEnginePath));
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

    internal InitResult CreateWaf(bool useUnsafeEncoder = false, string? ruleFile = null, string? obfuscationParameterKeyRegex = null, string? obfuscationParameterValueRegex = null, bool expectWafNull = false, bool wafDebugEnabled = false)
    {
        var configurationState = CreateConfigurationState(ruleFile);
        return CreateWaf(configurationState, useUnsafeEncoder, obfuscationParameterKeyRegex, obfuscationParameterValueRegex, expectWafNull, wafDebugEnabled);
    }

    internal InitResult CreateWaf(ConfigurationState configurationState, bool useUnsafeEncoder = false, string? obfuscationParameterKeyRegex = null, string? obfuscationParameterValueRegex = null, bool expectWafNull = false, bool wafDebugEnabled = false)
    {
        var initResult = Waf.Create(
            WafLibraryInvoker!,
            obfuscationParameterKeyRegex ?? string.Empty,
            obfuscationParameterValueRegex ?? string.Empty,
            configurationState,
            useUnsafeEncoder: useUnsafeEncoder,
            wafDebugEnabled: wafDebugEnabled);
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

    internal UpdateResult UpdateWaf(ConfigurationState configurationState, Waf waf)
    {
        configurationState.ApplyStoredFiles();
        var updateRes = waf.Update(configurationState);
        return updateRes;
    }

    internal UpdateResult UpdateWaf(ConfigurationState configurationState, Waf waf, ref IContext? context)
    {
        var res = UpdateWaf(configurationState, waf);
        context?.Dispose();
        context = waf.CreateContext();
        return res;
    }

    internal void AddAsmRemoteConfig(ConfigurationState configurationState, AppSec.Rcm.Models.Asm.Payload payload, string id, params string[] removeIds)
    {
        var binPayload = SerializePayload(payload);
        var rcPathTxt = $"datadog/00/ASM/{id}/config";
        var rcPath = RemoteConfigurationPath.FromPath(rcPathTxt);
        var rc = new RemoteConfiguration(rcPath, binPayload, binPayload.Length, new(), 1);
        var product = RcmProducts.Asm;
        var remoteConfigs = new Dictionary<string, List<RemoteConfiguration>> { { product, new List<RemoteConfiguration> { rc } } };
        var removeList = CreateRemoteRemovedConfigsByProduct(product, removeIds);
        configurationState.ReceivedNewConfig(remoteConfigs, removeList);
    }

    internal void RemoveAsmRemoteConfig(ConfigurationState configurationState, params string[] removeIds)
    {
        var removeList = CreateRemoteRemovedConfigsByProduct(RcmProducts.Asm, removeIds);
        configurationState.ReceivedNewConfig(new(), removeList);
    }

    internal void AddAsmDataRemoteConfig(ConfigurationState configurationState, AppSec.Rcm.Models.AsmData.Payload payload, string id, params string[] removeIds)
    {
        var binPayload = SerializePayload(payload);
        var rcPathTxt = $"datadog/00/ASM_DATA/{id}/config";
        var rcPath = RemoteConfigurationPath.FromPath(rcPathTxt);
        var rc = new RemoteConfiguration(rcPath, binPayload, binPayload.Length, new(), 1);
        var product = RcmProducts.AsmData;
        var remoteConfigs = new Dictionary<string, List<RemoteConfiguration>> { { product, new List<RemoteConfiguration> { rc } } };
        var removeList = CreateRemoteRemovedConfigsByProduct(product, removeIds);
        configurationState.ReceivedNewConfig(remoteConfigs, removeList);
    }

    internal void RemoveAsmDataRemoteConfig(ConfigurationState configurationState, params string[] removeIds)
    {
        var removeList = CreateRemoteRemovedConfigsByProduct(RcmProducts.AsmData, removeIds);
        configurationState.ReceivedNewConfig(new(), removeList);
    }

    internal void AddAsmDDRemoteConfig(ConfigurationState configurationState, RuleSet payload, string id, params string[] removeIds)
    {
        var binPayload = SerializePayload(payload);
        var rcPathTxt = $"datadog/00/ASM_DD/{id}/config";
        var rcPath = RemoteConfigurationPath.FromPath(rcPathTxt);
        var rc = new RemoteConfiguration(rcPath, binPayload, binPayload.Length, new(), 1);
        var product = RcmProducts.AsmDd;
        var remoteConfigs = new Dictionary<string, List<RemoteConfiguration>> { { product, new List<RemoteConfiguration> { rc } } };
        var removeList = CreateRemoteRemovedConfigsByProduct(product, removeIds);
        configurationState.ReceivedNewConfig(remoteConfigs, removeList);
    }

    internal void RemoveDefaultRemoteConfig(ConfigurationState configurationState)
    {
        var removeList = new Dictionary<string, List<RemoteConfigurationPath>> { { RcmProducts.AsmDd, new List<RemoteConfigurationPath> { RemoteConfigurationPath.FromPath(AsmDdProduct.DefaultConfigKey) } } };
        configurationState.ReceivedNewConfig(new(), removeList);
    }

    private static Dictionary<string, List<RemoteConfigurationPath>>? CreateRemoteRemovedConfigsByProduct(string product, params string[] removeIds)
    {
        if (removeIds.Length == 0)
        {
            return null;
        }

        var list = removeIds.Select(i => RemoteConfigurationPath.FromPath($"datadog/00/{product}/{i}/config")).ToList();
        return new Dictionary<string, List<RemoteConfigurationPath>> { { product, list } };
    }

    private static byte[] SerializePayload(object payload)
    {
        var serializedPayload = JsonConvert.SerializeObject(payload, new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        return Encoding.UTF8.GetBytes(serializedPayload);
    }

    private static ConfigurationState CreateConfigurationState(bool creation, string? ruleFile, Dictionary<string, RuleOverride[]>? ruleOverrides = null, Dictionary<string, RuleData[]>? rulesData = null, Dictionary<string, RuleSet>? ruleSet = null, Dictionary<string, Action[]>? actions = null)
    {
        Dictionary<string, AppSec.Rcm.Models.Asm.Payload> asmConfigs = new();
        if (ruleOverrides is { Count: > 0 })
        {
            foreach (var pair in ruleOverrides)
            {
                asmConfigs[$"datadog/00/ASM/{pair.Key}/config"] = new AppSec.Rcm.Models.Asm.Payload
                {
                    RuleOverrides = pair.Value,
                };
            }
        }

        if (actions is { Count: > 0 })
        {
            foreach (var pair in actions)
            {
                asmConfigs[$"datadog/00/ASM/{pair.Key}/config"] = new AppSec.Rcm.Models.Asm.Payload
                {
                    Actions = pair.Value,
                };
            }
        }

        Dictionary<string, AppSec.Rcm.Models.AsmData.Payload> asmDataConfigs = new();
        if (rulesData is { Count: > 0 })
        {
            foreach (var pair in rulesData)
            {
                asmDataConfigs[$"datadog/00/ASM_DATA/{pair.Key}/config"] = new AppSec.Rcm.Models.AsmData.Payload
                {
                    RulesData = pair.Value,
                };
            }
        }

        return CreateConfigurationState(creation, ruleFile, null, asmConfigs, asmDataConfigs);
    }

    private static ConfigurationState CreateConfigurationState(bool creation, string? ruleFile = null, List<KeyValuePair<string, RuleSet>>? rulesetConfigs = null, Dictionary<string, AppSec.Rcm.Models.Asm.Payload>? asmConfigs = null, Dictionary<string, AppSec.Rcm.Models.AsmData.Payload>? asmDataConfigs = null)
    {
        var source = CreateConfigurationSource((ConfigurationKeys.AppSec.Rules, ruleFile), (ConfigurationKeys.AppSec.Enabled, "1"));
        var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);
        var configurationState = new ConfigurationState(settings, NullConfigurationTelemetry.Instance, creation, rulesetConfigs, asmConfigs, asmDataConfigs);
        return configurationState;
    }
}
