// <copyright file="FfeProductTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System.Collections.Generic;
using System.Text;
using Datadog.Trace.FeatureFlags.Rcm;
using Datadog.Trace.FeatureFlags.Rcm.Model;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

public class FfeProductTests
{
    [Fact]
    public void UpdateFromRcm_WhenTheSameFileIsModifiedRepeatedly_HoldsOneVersionOfIt()
    {
        var delivered = new List<KeyValuePair<string, ServerConfiguration>>();
        var product = new FfeProduct(configurations => delivered = configurations);
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
        // Default Newtonsoft date parsing turns these into dates, converted to the host's time zone, before the
        // model sees them; the agentless parser keeps them as strings.
        var delivered = new List<KeyValuePair<string, ServerConfiguration>>();
        var product = new FfeProduct(configurations => delivered = configurations);
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

    private static Dictionary<string, List<RemoteConfiguration>> ConfigUpdate(RemoteConfigurationPath path, int version, string? json = null)
    {
        json ??= JsonConvert.SerializeObject(new ServerConfiguration());
        return new Dictionary<string, List<RemoteConfiguration>>
        {
            [RcmProducts.FfeFlags] = [new RemoteConfiguration(path, Encoding.UTF8.GetBytes(json), json.Length, new Dictionary<string, string> { { "sha256", version.ToString() } }, version)]
        };
    }
}
