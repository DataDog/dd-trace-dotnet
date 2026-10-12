// <copyright file="NativeRemoteConfigClientTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using Datadog.Trace.LibDatadog.RemoteConfig;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using Datadog.Trace.Vendors.Newtonsoft.Json.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Tests.RemoteConfigurationManagement;

public class NativeRemoteConfigClientTests
{
    private readonly ITestOutputHelper _output;

    public NativeRemoteConfigClientTests(ITestOutputHelper output)
    {
        _output = output;
    }

    // The responses which contain the files of all their configs, i.e. which are complete responses for a new client
    public static TheoryData<string> CompleteResponses()
    {
        var data = new TheoryData<string>();
        foreach (var response in AllResponses())
        {
            var json = JObject.Parse(response);
            var files = json["target_files"]!.Select(f => (string)f["path"]).ToList();
            if (json["client_configs"]!.Values<string>().All(files.Contains))
            {
                data.Add(response);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CompleteResponses))]
    public void FetchesAndDecodesConfigs(string response)
    {
        using var agent = MockTracerAgent.Create(_output, TcpPortProvider.GetOpenPort());
        agent.CustomResponses[MockTracerResponseType.RemoteConfig] = new(response);
        var expected = JObject.Parse(response);
        var files = expected["target_files"]!.ToDictionary(f => (string)f["path"], f => Convert.FromBase64String((string)f["raw"]));

        using var client = NativeRemoteConfigClient.Create(
            "client-id",
            RcmClientTracer.Create("runtime-id", "1.2.3", "service", "env", "1.0", new ReadOnlyDictionary<string, string>(new System.Collections.Generic.Dictionary<string, string>()), ["entrypoint.name:test"]),
            new RemoteConfigEndpoint($"http://127.0.0.1:{agent.Port}/", TimeSpan.FromSeconds(10)));
        client.SetProducts(
            [RcmProducts.Asm, RcmProducts.AsmDd, RcmProducts.AsmData, RcmProducts.AsmFeatures, RcmProducts.LiveDebugging, "APM_TRACING", "NOT_A_PRODUCT"],
            RcmCapabilitiesIndices.AsmActivation);

        var changes = client.Fetch();

        changes.Removed.Should().BeEmpty();
        changes.Updated.Select(c => c.Path.Path).Should().BeEquivalentTo(expected["client_configs"]!.Values<string>());
        foreach (var config in changes.Updated)
        {
            var raw = files[config.Path.Path];
            config.Contents.Length.Should().Be(raw.Length);
            ReadAll(config.Contents).Should().Equal(raw);
            new StreamReader(config.Contents.OpenStream(), Encoding.UTF8).ReadToEnd().Should().Be(Encoding.UTF8.GetString(raw));
            using var reader = new JsonTextReader(new StreamReader(config.Contents.OpenStream()));
            JToken.ReadFrom(reader).Should().NotBeNull();
        }

        // Nothing changed
        client.Fetch().IsEmpty.Should().BeTrue();

        var request = JsonConvert.DeserializeObject<GetRcmRequest>(agent.RemoteConfigRequests.Last());
        request.Client.Id.Should().Be("client-id");
        request.Client.Products.Should().NotContain("NOT_A_PRODUCT").And.Contain(RcmProducts.AsmFeatures);
        request.Client.ClientTracer.Service.Should().Be("service");
        request.Client.ClientTracer.ProcessTags.Should().BeEquivalentTo("entrypoint.name:test");
        request.CachedTargetFiles.Select(f => f.Path).Should().BeEquivalentTo(files.Keys);
    }

    [Fact]
    public void AppliesFollowUpResponses()
    {
        // These responses follow each other: files the client has already are not sent again
        var responses = AllResponses().Where(r => r.Contains("LIVE_DEBUGGING-second") || r.Contains("LIVE_DEBUGGING-third")).ToList();
        responses.Should().HaveCount(3);

        using var agent = MockTracerAgent.Create(_output, TcpPortProvider.GetOpenPort());
        using var client = NativeRemoteConfigClient.Create(
            "client-id",
            RcmClientTracer.Create("runtime-id", "1.2.3", "service", "env", null, new ReadOnlyDictionary<string, string>(new System.Collections.Generic.Dictionary<string, string>()), null),
            new RemoteConfigEndpoint($"http://127.0.0.1:{agent.Port}/", TimeSpan.FromSeconds(10)));
        client.SetProducts([RcmProducts.LiveDebugging], 0);

        var previousConfigs = new System.Collections.Generic.HashSet<string>();
        foreach (var response in responses)
        {
            agent.CustomResponses[MockTracerResponseType.RemoteConfig] = new(response);
            var json = JObject.Parse(response);
            var configs = json["client_configs"]!.Values<string>().ToList();
            var files = json["target_files"]!.ToDictionary(f => (string)f["path"], f => Convert.FromBase64String((string)f["raw"]));

            var changes = client.Fetch();

            changes.Removed.Select(p => p.Path).Should().BeEquivalentTo(previousConfigs.Except(configs));
            changes.Updated.Select(c => c.Path.Path).Should().Contain(configs.Except(previousConfigs));
            foreach (var config in changes.Updated)
            {
                ReadAll(config.Contents).Should().Equal(files[config.Path.Path]);
            }

            previousConfigs = [..configs];
        }
    }

    [Fact]
    public void RebindsWithAndWithoutNewEndpoint()
    {
        var response = AllResponses().First();
        using var agent = MockTracerAgent.Create(_output, TcpPortProvider.GetOpenPort());
        agent.CustomResponses[MockTracerResponseType.RemoteConfig] = new(response);
        var endpoint = new RemoteConfigEndpoint($"http://127.0.0.1:{agent.Port}/", TimeSpan.FromSeconds(10));
        var tags = new ReadOnlyDictionary<string, string>(new System.Collections.Generic.Dictionary<string, string>());

        using var client = NativeRemoteConfigClient.Create("client-id", RcmClientTracer.Create("runtime-id", "1.2.3", "service", "env", null, tags, null), endpoint);
        client.SetProducts([RcmProducts.Asm, RcmProducts.AsmDd, RcmProducts.AsmFeatures], 0);
        client.Fetch().Updated.Should().NotBeEmpty();

        // The known files are neither downloaded again nor reported again
        client.Rebind(RcmClientTracer.Create("runtime-id", "1.2.3", "other-service", "env", null, tags, null), null);
        client.Fetch().IsEmpty.Should().BeTrue();
        JsonConvert.DeserializeObject<GetRcmRequest>(agent.RemoteConfigRequests.Last()).Client.ClientTracer.Service.Should().Be("other-service");

        client.Rebind(RcmClientTracer.Create("runtime-id", "1.2.3", "service", "env", null, tags, null), endpoint);
        client.Fetch().IsEmpty.Should().BeTrue();
        var request = JsonConvert.DeserializeObject<GetRcmRequest>(agent.RemoteConfigRequests.Last());
        request.Client.ClientTracer.Service.Should().Be("service");
        request.Client.State.TargetsVersion.Should().Be(0);
        request.CachedTargetFiles.Should().NotBeEmpty();
    }

    [Fact]
    public void ContentsOutliveTheClient()
    {
        var response = AllResponses().First();
        using var agent = MockTracerAgent.Create(_output, TcpPortProvider.GetOpenPort());
        agent.CustomResponses[MockTracerResponseType.RemoteConfig] = new(response);

        RemoteConfigurationChanges changes;
        using (var client = NativeRemoteConfigClient.Create(
                   "client-id",
                   RcmClientTracer.Create("runtime-id", "1.2.3", "service", "env", null, new ReadOnlyDictionary<string, string>(new System.Collections.Generic.Dictionary<string, string>()), null),
                   new RemoteConfigEndpoint($"http://127.0.0.1:{agent.Port}/", TimeSpan.FromSeconds(10))))
        {
            client.SetProducts([RcmProducts.Asm, RcmProducts.AsmDd, RcmProducts.AsmFeatures], 0);
            changes = client.Fetch();
        }

        changes.Updated.Should().NotBeEmpty();
        changes.Updated.Should().OnlyContain(c => ReadAll(c.Contents).Length > 0);
    }

    private static byte[] ReadAll(RemoteConfigurationContent contents)
    {
        using var stream = contents.OpenStream();
        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }

    private static System.Collections.Generic.List<string> AllResponses()
    {
        var responses = new System.Collections.Generic.List<string>();
        foreach (string response in RcmResponses.TargetsTargetFilesClientConfigs())
        {
            responses.Add(response);
        }

        return responses;
    }
}
