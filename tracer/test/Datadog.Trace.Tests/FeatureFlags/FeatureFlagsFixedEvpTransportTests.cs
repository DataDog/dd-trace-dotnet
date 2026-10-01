// <copyright file="FeatureFlagsFixedEvpTransportTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.FeatureFlags.Evp;
using Datadog.Trace.Telemetry;
using Datadog.Trace.TestHelpers;
using Datadog.Trace.Vendors.Newtonsoft.Json;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.FeatureFlags;

[Collection(nameof(WebRequestCollection))]
public class FeatureFlagsFixedEvpTransportTests
{
    [Theory]
    [InlineData("remote_config", 403)]
    [InlineData("remote_config", 500)]
    [InlineData("agentless", 403)]
    [InlineData("agentless", 500)]
    public async Task ProductionSenderKeepsFixedV2AfterHttpFailure(string source, int firstStatus)
    {
        using var agent = new HttpListener();
        var agentUrl = $"http://127.0.0.1:{TcpPortProvider.GetOpenPort()}/";
        agent.Prefixes.Add(agentUrl);
        agent.Start();
        var settings = new TracerSettings(new NameValueConfigurationSource(new NameValueCollection
        {
            { ConfigurationKeys.FeatureFlags.FeatureFlagsConfigurationSource, source },
            { ConfigurationKeys.AgentUri, agentUrl + "prefix/" },
            { ConfigurationKeys.ApiKey, "must-not-reach-agent" },
        }));
        using var transport = new FeatureFlagsEvpTransport(settings);

        foreach (var status in new[] { firstStatus, 200 })
        {
            var received = agent.GetContextAsync();
            var send = transport.SendAsync(new { Flag = "test" }, FeatureFlagsEvpTransport.ExposureIntakePath, new JsonSerializerSettings());
            (await Task.WhenAny(received, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(received);
            var request = await received;
            request.Request.Url!.AbsolutePath.Should().Be("/prefix/evp_proxy/v2/api/v2/exposures");
            request.Request.Headers[TelemetryConstants.ApiKeyHeader].Should().BeNull();
            await request.Request.InputStream.CopyToAsync(Stream.Null);
            request.Response.StatusCode = status;
            request.Response.Close();
            (await Task.WhenAny(send, Task.Delay(TimeSpan.FromSeconds(5)))).Should().BeSameAs(send);
            await send;
        }
    }
}
