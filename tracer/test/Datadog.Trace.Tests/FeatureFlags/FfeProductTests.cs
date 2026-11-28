// <copyright file="FfeProductTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Text;
using Datadog.Trace.FeatureFlags;
using Datadog.Trace.FeatureFlags.Rcm;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

using FeatureFlagsValueType = Datadog.Trace.FeatureFlags.ValueType;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FfeProductTests
{
    [Theory]
    [CombinatorialData]
    public void UpdateFromRcm_InstallsValidFilesAndReportsAnInvalidSibling(bool invalidFirst, bool malformed)
    {
        var delivered = new List<KeyValuePair<string, ServerConfiguration>>();
        var product = new FfeProduct(configurations =>
        {
            delivered = configurations;
            return true;
        });
        var validPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/valid/config");
        var invalidPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/invalid/config");
        var valid = ConfigUpdate(validPath, 1)[RcmProducts.FfeFlags][0];
        var invalid = ConfigUpdate(invalidPath, 1, malformed ? "{" : "null")[RcmProducts.FfeFlags][0];
        var updates = new Dictionary<string, List<RemoteConfiguration>>
        {
            [RcmProducts.FfeFlags] = invalidFirst ? [invalid, valid] : [valid, invalid]
        };

        var outcomes = product.UpdateFromRcm(updates, null);

        delivered.Should().ContainSingle().Which.Key.Should().Be(validPath.Path);
        outcomes.Should().ContainSingle(outcome => outcome.Filename == validPath.Path)
                .Which.ApplyState.Should().Be(ApplyStates.ACKNOWLEDGED);
        outcomes.Should().ContainSingle(outcome => outcome.Filename == invalidPath.Path)
                .Which.ApplyState.Should().Be(ApplyStates.ERROR);
    }

    [Fact]
    public void UpdateFromRcm_RetriesWithdrawalsWhenTheEvaluatorRejectsAnUpdate()
    {
        var delivered = new List<KeyValuePair<string, ServerConfiguration>>();
        var reject = false;
        var product = new FfeProduct(configurations =>
        {
            if (reject)
            {
                return false;
            }

            delivered = configurations;
            return true;
        });
        var installedPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/installed/config");
        var rejectedPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/rejected/config");
        var successorPath = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/successor/config");
        product.UpdateFromRcm(ConfigUpdate(installedPath, 1), null);

        reject = true;
        var removals = new Dictionary<string, List<RemoteConfigurationPath>> { [RcmProducts.FfeFlags] = [installedPath] };
        var rejected = product.UpdateFromRcm(ConfigUpdate(rejectedPath, 1), removals);
        rejected.Should().ContainSingle().Which.ApplyState.Should().Be(ApplyStates.ERROR);
        delivered.Should().ContainSingle().Which.Key.Should().Be(installedPath.Path);

        reject = false;
        product.UpdateFromRcm(ConfigUpdate(successorPath, 1), null);
        delivered.Should().ContainSingle().Which.Key.Should().Be(successorPath.Path);
    }

    [Fact]
    public void UpdateFromRcm_WhenTheSameFileIsModifiedRepeatedly_HoldsOneVersionOfIt()
    {
        var delivered = new List<KeyValuePair<string, ServerConfiguration>>();
        var product = new FfeProduct(configurations =>
        {
            delivered = configurations;
            return true;
        });
        var path = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/test-config/config");

        for (var version = 1; version <= 3; version++)
        {
            product.UpdateFromRcm(ConfigUpdate(path, version), null);
        }

        // Each update replaces the file's previous version. Appending instead grows the list by a
        // full copy of the flags file on every update, for the life of the process.
        delivered.Should().ContainSingle().Which.Key.Should().Be(path.Path);
    }

    [Theory]
    [InlineData("2026-11-02T00:00:00+05:00")]
    [InlineData("2026-11-02T00:00:00Z")]
    [InlineData("2026-11-02T00:00:00")]
    public void UpdateFromRcm_KeepsDateLikeStringsAsConfigured(string text)
    {
        // Default Newtonsoft date parsing turns these into dates, in the host's time zone for offsets. The
        // agentless parser keeps them as strings.
        var delivered = new List<KeyValuePair<string, ServerConfiguration>>();
        var product = new FfeProduct(configurations =>
        {
            delivered = configurations;
            return true;
        });
        var path = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/test-config/config");
        var json = """
            {"flags":{"date-flag":{"key":"date-flag","enabled":true,"variationType":"STRING",
              "variations":{"on":{"key":"on","value":"TEXT"}},
              "allocations":[{"key":"alloc","splits":[{"variationKey":"on","shards":[],
                "features":[{"key":"starts","value":"TEXT","destinations":["HOOK"]}]}]}]}}}
            """.Replace("TEXT", text);

        product.UpdateFromRcm(ConfigUpdate(path, 1, json), null);

        var flag = delivered.Should().ContainSingle().Subject.Value.Flags!["date-flag"];
        flag.Variations!["on"].Value.Should().Be(text);
        flag.Allocations![0].Splits![0].Features![0].Value.Should().Be(text);
    }

    [Theory]
    [InlineData("2020-11-02T00:00:00+05:00")]
    [InlineData("2020-11-02T00:00:00.123456Z")]
    public void UpdateFromRcm_KeepsDateLikeStringsInConditionsJsonValuesAndAllocationDates(string text)
    {
        var delivered = new List<KeyValuePair<string, ServerConfiguration>>();
        var product = new FfeProduct(configurations =>
        {
            delivered = configurations;
            return true;
        });
        var path = RemoteConfigurationPath.FromPath($"datadog/2/{RcmProducts.FfeFlags}/test-config/config");
        var json = """
            {"flags":{"json-flag":{"key":"json-flag","enabled":true,"variationType":"JSON",
              "variations":{"on":{"key":"on","value":{"launch":{"at":"TEXT"}}}},
              "allocations":[{"key":"alloc","startAt":"TEXT",
                "rules":[{"conditions":[{"attribute":"launch","operator":"ONE_OF","value":["TEXT"]}]}],
                "splits":[{"variationKey":"on","shards":[]}]}]}}}
            """.Replace("TEXT", text);

        product.UpdateFromRcm(ConfigUpdate(path, 1, json), null);

        var configuration = delivered.Should().ContainSingle().Subject.Value;
        configuration.Flags!["json-flag"].Allocations![0].StartAt.Should().Be(text);
        var result = new FeatureFlagsEvaluator(null, configuration)
            .Evaluate("json-flag", FeatureFlagsValueType.Json, null, new EvaluationContext("user", new Dictionary<string, object?> { ["launch"] = text }));

        result.Reason.Should().Be(EvaluationReason.TargetingMatch, "the ONE_OF value keeps its configured text");
        var launch = result.Value.Should().BeOfType<Dictionary<string, object?>>().Subject["launch"];
        launch.Should().BeOfType<Dictionary<string, object?>>().Which["at"].Should().Be(text, "a nested date-like string stays a string");
    }

    private static Dictionary<string, List<RemoteConfiguration>> ConfigUpdate(RemoteConfigurationPath path, int version, string? json = null)
    {
        json ??= JsonConvert.SerializeObject(new ServerConfiguration());
        return new Dictionary<string, List<RemoteConfiguration>>
        {
            [RcmProducts.FfeFlags] = [new RemoteConfiguration(path, Encoding.UTF8.GetBytes(json), json.Length, new Dictionary<string, string> { { "sha256", version.ToString() } }, version)]
        };
    }
}
