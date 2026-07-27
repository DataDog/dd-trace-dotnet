// <copyright file="ConfigurationStateTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests;

public class ConfigurationStateTests
{
    private const string AsmDdPath1 = "datadog/00/ASM_DD/rules1/config";
    private const string AsmDdPath2 = "datadog/00/ASM_DD/rules2/config";
    private const string AsmPath1 = "datadog/00/ASM/overrides1/config";
    private const string AsmDataPath1 = "datadog/00/ASM_DATA/blocked_ips/config";
    private const string AsmDataPath2 = "datadog/00/ASM_DATA/blocked_users/config";
    private const string AsmFeaturesPath = "datadog/00/ASM_FEATURES/asm/config";

    // Regression test for the bug where pending ASM_DD configs received while ASM is
    // disabled were wiped by `_fileUpdates.Clear()` on every ReceivedNewConfig call.
    // RCM only forwards deltas, so a later ASM_FEATURES-only poll wouldn't re-include
    // the ruleset — and ASM would initialize without the custom rules.
    [Fact]
    public void PendingAsmDdConfig_IsApplied_WhenAsmFeaturesEnablesAsmInLaterPoll()
    {
        var configurationState = CreateTogglableConfigurationState();

        // Poll 1: backend sends an ASM_DD ruleset; ASM still disabled so nothing is applied yet.
        configurationState.ReceivedNewConfig(BuildAsmDdUpdate(AsmDdPath1), removedConfigs: null);
        configurationState.RulesetConfigs.Should().BeEmpty("ASM is disabled, so nothing has been applied yet");
        configurationState.IncomingUpdateState.ShouldInitAppsec.Should().BeFalse();

        // Poll 2: backend now sends ASM_FEATURES enabling ASM. RCM does NOT re-send the
        // unchanged ASM_DD config — it's already in _appliedConfigurations on the RCM side.
        configurationState.ReceivedNewConfig(BuildAsmFeaturesUpdate(enabled: true), removedConfigs: null);

        configurationState.IncomingUpdateState.ShouldInitAppsec.Should().BeTrue();
        configurationState.RulesetConfigs.Should().ContainSingle(r => r.Key == AsmDdPath1, "the pending ASM_DD config from poll 1 must survive into the enable transition");
    }

    [Fact]
    public void PendingRemove_CancelsEarlierPendingUpdate_ForSamePath()
    {
        var configurationState = CreateTogglableConfigurationState();

        // Poll 1: add an ASM_DD config.
        configurationState.ReceivedNewConfig(BuildAsmDdUpdate(AsmDdPath1), removedConfigs: null);

        // Poll 2: backend removes that same config (e.g. user deleted it).
        configurationState.ReceivedNewConfig(new(), BuildRemoves(RcmProducts.AsmDd, AsmDdPath1));

        // Poll 3: ASM_FEATURES enables ASM. The earlier update + later remove for the
        // same path must NOT result in the config being applied.
        configurationState.ReceivedNewConfig(BuildAsmFeaturesUpdate(enabled: true), removedConfigs: null);

        configurationState.IncomingUpdateState.ShouldInitAppsec.Should().BeTrue();
        configurationState.RulesetConfigs.Should().BeEmpty("the pending update must have been cancelled by the subsequent remove");
    }

    [Fact]
    public void PendingUpdate_CancelsEarlierPendingRemove_ForSamePath()
    {
        var configurationState = CreateTogglableConfigurationState();

        // Poll 1: a remove arrives (e.g. ASM was previously enabled and a config got removed).
        configurationState.ReceivedNewConfig(new(), BuildRemoves(RcmProducts.AsmDd, AsmDdPath1));

        // Poll 2: backend re-adds a config at the same path.
        configurationState.ReceivedNewConfig(BuildAsmDdUpdate(AsmDdPath1), removedConfigs: null);

        // Poll 3: ASM_FEATURES enables ASM. The remove from poll 1 must NOT wipe the update from poll 2.
        configurationState.ReceivedNewConfig(BuildAsmFeaturesUpdate(enabled: true), removedConfigs: null);

        configurationState.IncomingUpdateState.ShouldInitAppsec.Should().BeTrue();
        configurationState.RulesetConfigs.Should().ContainSingle(r => r.Key == AsmDdPath1, "the later update must override the earlier remove");
    }

    [Fact]
    public void RepeatedUpdatesForSamePath_DoNotAccumulateInPendingState()
    {
        var configurationState = CreateTogglableConfigurationState();

        // Push three updates for the same path across three polls.
        configurationState.ReceivedNewConfig(BuildAsmDdUpdate(AsmDdPath1), removedConfigs: null);
        configurationState.ReceivedNewConfig(BuildAsmDdUpdate(AsmDdPath1), removedConfigs: null);
        configurationState.ReceivedNewConfig(BuildAsmDdUpdate(AsmDdPath1), removedConfigs: null);

        // And one update for a different path to confirm both coexist correctly.
        configurationState.ReceivedNewConfig(BuildAsmDdUpdate(AsmDdPath2), removedConfigs: null);

        configurationState.ReceivedNewConfig(BuildAsmFeaturesUpdate(enabled: true), removedConfigs: null);

        configurationState.RulesetConfigs.Should().HaveCount(2);
        configurationState.RulesetConfigs.Should().Contain(r => r.Key == AsmDdPath1);
        configurationState.RulesetConfigs.Should().Contain(r => r.Key == AsmDdPath2);
    }

    [Fact]
    public void PendingAsmConfig_IsApplied_WhenAsmFeaturesEnablesAsmInLaterPoll()
    {
        var configurationState = CreateTogglableConfigurationState();

        // Same scenario as the ASM_DD regression test, but for the ASM product (rule overrides, exclusions, etc.).
        configurationState.ReceivedNewConfig(BuildAsmUpdate(AsmPath1), removedConfigs: null);
        configurationState.AsmConfigs.Should().BeEmpty("ASM is disabled, so nothing has been applied yet");

        configurationState.ReceivedNewConfig(BuildAsmFeaturesUpdate(enabled: true), removedConfigs: null);

        configurationState.IncomingUpdateState.ShouldInitAppsec.Should().BeTrue();
        configurationState.AsmConfigs.Should().ContainKey(AsmPath1, "the pending ASM config must survive into the enable transition");
    }

    [Fact]
    public void PendingAsmDataConfig_IsApplied_WhenAsmFeaturesEnablesAsmInLaterPoll()
    {
        var configurationState = CreateTogglableConfigurationState();

        configurationState.ReceivedNewConfig(BuildAsmDataUpdate(AsmDataPath1), removedConfigs: null);
        configurationState.AsmDataConfigs.Should().BeEmpty("ASM is disabled, so nothing has been applied yet");

        configurationState.ReceivedNewConfig(BuildAsmFeaturesUpdate(enabled: true), removedConfigs: null);

        configurationState.IncomingUpdateState.ShouldInitAppsec.Should().BeTrue();
        configurationState.AsmDataConfigs.Should().ContainKey(AsmDataPath1, "the pending ASM_DATA config must survive into the enable transition");
    }

    // ASM can be toggled off and on via ASM_FEATURES. The deserialized destination dictionaries
    // (RulesetConfigs / AsmConfigs / AsmDataConfigs) are not cleared when ASM is disabled, so a
    // re-enable should restore the previously applied configs without requiring RCM to re-send them.
    [Fact]
    public void DisableThenReEnableCycle_PreservesAppliedConfigs()
    {
        var configurationState = CreateTogglableConfigurationState();

        // Poll 1: enable ASM and apply a ruleset.
        configurationState.ReceivedNewConfig(MergeProducts(BuildAsmFeaturesUpdate(enabled: true), BuildAsmDdUpdate(AsmDdPath1)), removedConfigs: null);
        configurationState.IncomingUpdateState.ShouldInitAppsec.Should().BeTrue();
        configurationState.RulesetConfigs.Should().ContainSingle(r => r.Key == AsmDdPath1);
        CompletePoll(configurationState);

        // Poll 2: disable ASM via ASM_FEATURES.
        configurationState.ReceivedNewConfig(BuildAsmFeaturesUpdate(enabled: false), removedConfigs: null);
        configurationState.IncomingUpdateState.ShouldDisableAppsec.Should().BeTrue();
        configurationState.RulesetConfigs.Should().ContainSingle(r => r.Key == AsmDdPath1, "deserialized state must survive a disable");
        CompletePoll(configurationState);

        // Poll 3: re-enable ASM. RCM does not re-send the ruleset (unchanged on the backend).
        configurationState.ReceivedNewConfig(BuildAsmFeaturesUpdate(enabled: true), removedConfigs: null);
        configurationState.IncomingUpdateState.ShouldInitAppsec.Should().BeTrue();
        configurationState.RulesetConfigs.Should().ContainSingle(r => r.Key == AsmDdPath1, "the previously applied ruleset must still be active after the disable/re-enable cycle");
    }

    // BuildWafUpdatePayload(updating: true) is the path used when ASM is already running and a new RCM
    // delta arrives. It must only push the *changed* configs to the WAF — not the full applied state —
    // otherwise every poll re-ships every config across the FFI boundary.
    [Fact]
    public void BuildWafUpdatePayload_UpdatingMode_OnlyIncludesCurrentPollDelta()
    {
        var configurationState = CreateTogglableConfigurationState();

        // Poll 1: enable ASM with one ASM_DATA config. This is the init poll (updating=false path).
        configurationState.ReceivedNewConfig(MergeProducts(BuildAsmFeaturesUpdate(enabled: true), BuildAsmDataUpdate(AsmDataPath1)), removedConfigs: null);
        configurationState.IncomingUpdateState.ShouldInitAppsec.Should().BeTrue();
        configurationState.AsmDataConfigs.Should().ContainKey(AsmDataPath1);
        DrainAndCompletePoll(configurationState);

        // Poll 2: a NEW ASM_DATA config arrives; AsmDataPath1 is unchanged so RCM does not resend it.
        configurationState.ReceivedNewConfig(BuildAsmDataUpdate(AsmDataPath2), removedConfigs: null);
        configurationState.IncomingUpdateState.ShouldUpdateAppsec.Should().BeTrue();
        configurationState.AsmDataConfigs.Should().HaveCount(2, "the deserialized state retains both configs");

        var wafFiles = configurationState.BuildWafUpdatePayload(updating: true);

        wafFiles.Updates.Should().NotBeNull();
        wafFiles.Updates!.Should().ContainKey(AsmDataPath2, "only the newly-changed config should be shipped to the WAF");
        wafFiles.Updates.Should().NotContainKey(AsmDataPath1, "an unchanged config from a previous poll must not be re-pushed");
        wafFiles.Removes.Should().BeNullOrEmpty();
    }

    [Fact]
    public void BuildWafUpdatePayload_UpdatingMode_IncludesRemovesFromCurrentPoll()
    {
        var configurationState = CreateTogglableConfigurationState();

        // Poll 1: enable + add a config.
        configurationState.ReceivedNewConfig(MergeProducts(BuildAsmFeaturesUpdate(enabled: true), BuildAsmDataUpdate(AsmDataPath1)), removedConfigs: null);
        DrainAndCompletePoll(configurationState);

        // Poll 2: remove the config.
        configurationState.ReceivedNewConfig(new(), BuildRemoves(RcmProducts.AsmData, AsmDataPath1));
        configurationState.IncomingUpdateState.ShouldUpdateAppsec.Should().BeTrue();
        configurationState.AsmDataConfigs.Should().NotContainKey(AsmDataPath1, "the remove was applied to deserialized state");

        var wafFiles = configurationState.BuildWafUpdatePayload(updating: true);

        wafFiles.Removes.Should().NotBeNullOrEmpty();
        wafFiles.Removes!.Should().Contain(AsmDataPath1, "the WAF must be told about the remove on this poll");
    }

    // BuildWafUpdatePayload is a destructive read: it drains the WAF-relevant pending slots on its way out
    // so that subsequent update polls only see the next poll's delta. Calling it twice in the same cycle
    // returns an empty delta the second time.
    [Fact]
    public void BuildWafUpdatePayload_DrainsPendingAfterRead()
    {
        var configurationState = CreateTogglableConfigurationState();

        configurationState.ReceivedNewConfig(MergeProducts(BuildAsmFeaturesUpdate(enabled: true), BuildAsmDataUpdate(AsmDataPath1)), removedConfigs: null);
        DrainAndCompletePoll(configurationState);

        configurationState.ReceivedNewConfig(BuildAsmDataUpdate(AsmDataPath2), removedConfigs: null);
        configurationState.IncomingUpdateState.ShouldUpdateAppsec.Should().BeTrue();

        // First call returns the delta.
        var firstCall = configurationState.BuildWafUpdatePayload(updating: true);
        firstCall.Updates.Should().NotBeNull();
        firstCall.Updates!.Should().ContainKey(AsmDataPath2);

        // Second call: pending was drained, so IsNewUpdate returns false for everything in the dest dicts.
        var secondCall = configurationState.BuildWafUpdatePayload(updating: true);
        secondCall.Updates.Should().BeNull("the destructive read drained pending; the second call sees nothing new");
        secondCall.Removes.Should().BeNullOrEmpty();
    }

    // Verifies the drain-on-cycle-end behavior holds across consecutive update polls — configs applied
    // in earlier polls must not bleed into later WAF update payloads.
    [Fact]
    public void ConsecutiveUpdatePolls_DoNotLeakBetweenPolls()
    {
        var configurationState = CreateTogglableConfigurationState();

        // Init poll: enable + one config. Drain via BuildWafUpdatePayload (init path).
        configurationState.ReceivedNewConfig(MergeProducts(BuildAsmFeaturesUpdate(enabled: true), BuildAsmDataUpdate(AsmDataPath1)), removedConfigs: null);
        DrainAndCompletePoll(configurationState);

        // Update poll N: a new ASM_DATA config.
        configurationState.ReceivedNewConfig(BuildAsmDataUpdate(AsmDataPath2), removedConfigs: null);
        var payloadN = configurationState.BuildWafUpdatePayload(updating: true);
        payloadN.Updates!.Should().ContainKey(AsmDataPath2);
        payloadN.Updates.Should().NotContainKey(AsmDataPath1, "configs applied during init must not appear in subsequent update payloads");
        CompletePoll(configurationState);

        // Update poll N+1: a config under a different product. Earlier polls' configs must still not leak.
        configurationState.ReceivedNewConfig(BuildAsmUpdate(AsmPath1), removedConfigs: null);
        var payloadN1 = configurationState.BuildWafUpdatePayload(updating: true);
        payloadN1.Updates!.Should().ContainKey(AsmPath1);
        payloadN1.Updates.Should().NotContainKey(AsmDataPath1);
        payloadN1.Updates.Should().NotContainKey(AsmDataPath2, "configs applied in earlier update polls must not bleed into later update payloads");
    }

    // After a disable/re-enable cycle, a subsequent update poll must not re-push the previously-applied
    // configs to the WAF — they're already in its state. Only the current poll's delta should ship.
    [Fact]
    public void DisableThenReEnable_DoesNotRePushAppliedConfigsToWaf()
    {
        var configurationState = CreateTogglableConfigurationState();

        // Poll 1: enable + ruleset. Drain on the init WAF call.
        configurationState.ReceivedNewConfig(MergeProducts(BuildAsmFeaturesUpdate(enabled: true), BuildAsmDdUpdate(AsmDdPath1)), removedConfigs: null);
        DrainAndCompletePoll(configurationState);

        // Poll 2: disable. Production does NOT call BuildWafUpdatePayload on disable, so use plain CompletePoll.
        configurationState.ReceivedNewConfig(BuildAsmFeaturesUpdate(enabled: false), removedConfigs: null);
        CompletePoll(configurationState);

        // Poll 3: re-enable. Init path again — drains pending.
        configurationState.ReceivedNewConfig(BuildAsmFeaturesUpdate(enabled: true), removedConfigs: null);
        DrainAndCompletePoll(configurationState);

        // Poll 4: a NEW ASM_DD config arrives. The WAF update payload must contain ONLY the new config.
        configurationState.ReceivedNewConfig(BuildAsmDdUpdate(AsmDdPath2), removedConfigs: null);
        var payload = configurationState.BuildWafUpdatePayload(updating: true);
        payload.Updates.Should().NotBeNull();
        payload.Updates!.Should().ContainKey(AsmDdPath2);
        payload.Updates.Should().NotContainKey(AsmDdPath1, "configs applied before the disable cycle must not be re-pushed to the WAF on later update polls");
    }

    private static ConfigurationState CreateTogglableConfigurationState()
    {
        // Empty source: DD_APPSEC_ENABLED is unset, so SecuritySettings.CanBeToggled is true and AppsecEnabled is false.
        var source = new NameValueConfigurationSource(new NameValueCollection());
        var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);
        settings.CanBeToggled.Should().BeTrue();
        settings.AppsecEnabled.Should().BeFalse();
        return new ConfigurationState(settings, NullConfigurationTelemetry.Instance, wafIsNull: true);
    }

    private static Dictionary<string, List<RemoteConfiguration>> BuildAsmDdUpdate(string path)
    {
        // A minimal non-empty rules document. AsmDdProduct.ProcessUpdates only requires HasValues=true on the top-level JObject to take the standard parsing path.
        var ruleSetJson = new JObject { ["version"] = "2.2" };
        var bytes = Encoding.UTF8.GetBytes(ruleSetJson.ToString());
        var rcPath = RemoteConfigurationPath.FromPath(path);
        var rc = new RemoteConfiguration(rcPath, bytes, bytes.Length, new(), 1);
        return new Dictionary<string, List<RemoteConfiguration>> { { RcmProducts.AsmDd, new List<RemoteConfiguration> { rc } } };
    }

    private static Dictionary<string, List<RemoteConfiguration>> BuildAsmFeaturesUpdate(bool enabled)
    {
        var featuresJson = new JObject { ["asm"] = new JObject { ["enabled"] = enabled } };
        var bytes = Encoding.UTF8.GetBytes(featuresJson.ToString());
        var rcPath = RemoteConfigurationPath.FromPath(AsmFeaturesPath);
        var rc = new RemoteConfiguration(rcPath, bytes, bytes.Length, new(), 1);
        return new Dictionary<string, List<RemoteConfiguration>> { { RcmProducts.AsmFeatures, new List<RemoteConfiguration> { rc } } };
    }

    private static Dictionary<string, List<RemoteConfiguration>> BuildAsmUpdate(string path)
    {
        // AsmGenericProduct stores the raw JToken keyed by path — any payload that deserializes to a JToken works.
        var payload = new JObject { ["exclusions"] = new JArray() };
        var bytes = Encoding.UTF8.GetBytes(payload.ToString());
        var rcPath = RemoteConfigurationPath.FromPath(path);
        var rc = new RemoteConfiguration(rcPath, bytes, bytes.Length, new(), 1);
        return new Dictionary<string, List<RemoteConfiguration>> { { RcmProducts.Asm, new List<RemoteConfiguration> { rc } } };
    }

    private static Dictionary<string, List<RemoteConfiguration>> BuildAsmDataUpdate(string path)
    {
        var payload = new JObject { ["rules_data"] = new JArray() };
        var bytes = Encoding.UTF8.GetBytes(payload.ToString());
        var rcPath = RemoteConfigurationPath.FromPath(path);
        var rc = new RemoteConfiguration(rcPath, bytes, bytes.Length, new(), 1);
        return new Dictionary<string, List<RemoteConfiguration>> { { RcmProducts.AsmData, new List<RemoteConfiguration> { rc } } };
    }

    private static Dictionary<string, List<RemoteConfigurationPath>> BuildRemoves(string product, params string[] paths)
    {
        var list = new List<RemoteConfigurationPath>(paths.Length);
        foreach (var p in paths)
        {
            list.Add(RemoteConfigurationPath.FromPath(p));
        }

        return new Dictionary<string, List<RemoteConfigurationPath>> { { product, list } };
    }

    // Combines per-product update dicts into a single payload (the way RCM delivers a mixed delta).
    private static Dictionary<string, List<RemoteConfiguration>> MergeProducts(params Dictionary<string, List<RemoteConfiguration>>[] dicts)
    {
        var merged = new Dictionary<string, List<RemoteConfiguration>>();
        foreach (var dict in dicts)
        {
            foreach (var kvp in dict)
            {
                merged[kvp.Key] = kvp.Value;
            }
        }

        return merged;
    }

    // Mirrors what Security.UpdateFromRcm does at the end of an RCM cycle when no WAF update happens
    // (disable, or empty delta): commit the AppsecEnabled state and reset the per-poll flags.
    private static void CompletePoll(ConfigurationState state)
    {
        if (state.IncomingUpdateState.ShouldInitAppsec) { state.AppsecEnabled = true; }
        if (state.IncomingUpdateState.ShouldDisableAppsec) { state.AppsecEnabled = false; }
        state.IncomingUpdateState.Reset();
    }

    // Mirrors a cycle where production would call _waf.Create/Update — which invokes BuildWafUpdatePayload
    // and drains the WAF-relevant pending slots. Use after an init/update poll when you want the next poll
    // to start with empty pending (the way production behaves between WAF-updating polls).
    private static void DrainAndCompletePoll(ConfigurationState state)
    {
        var updating = state.IncomingUpdateState.ShouldUpdateAppsec;
        _ = state.BuildWafUpdatePayload(updating);
        CompletePoll(state);
    }
}
