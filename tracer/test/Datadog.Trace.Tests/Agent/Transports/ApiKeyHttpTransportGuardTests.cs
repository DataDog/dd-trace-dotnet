// <copyright file="ApiKeyHttpTransportGuardTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
#if NETCOREAPP3_1_OR_GREATER
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
#endif
using Datadog.Trace.Agent.Transports;
#if NETCOREAPP3_1_OR_GREATER
using Datadog.Trace.Ci;
using Datadog.Trace.Ci.Configuration;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
#endif
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Agent.Transports;

public class ApiKeyHttpTransportGuardTests
{
    [Theory]
    [InlineData("https://example.com")]
    [InlineData("http://localhost")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://[::1]")]
    public void AllowsSecureOrLoopbackEndpointWithApiKey(string endpoint)
    {
        var action = () => ApiKeyHttpTransportGuard.EnsureSafeEndpoint(new Uri(endpoint));

        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("ftp://example.com")]
    public void RejectsUnsafeEndpointWithApiKey(string endpoint)
    {
        var action = () => ApiKeyHttpTransportGuard.EnsureSafeEndpoint(new Uri(endpoint));

        action.Should()
              .Throw<ApiKeyHttpTransportException>()
              .WithMessage("*DD-API-KEY*");
    }

    [Theory]
    [InlineData("https://example.com", true, false)]
    [InlineData("http://localhost", false, true)]
    public void RejectsUnsafeTransport(string endpoint, bool isProxyDisabled, bool redirectsDisabled)
    {
        var action = () => ApiKeyHttpTransportGuard.EnsureSafe(
            new Uri(endpoint),
            isProxyDisabled,
            redirectsDisabled);

        action.Should().Throw<ApiKeyHttpTransportException>();
    }

#if NETCOREAPP3_1_OR_GREATER
    [Fact]
    public async Task HttpClientRequestRejectsUnsafeDefaultApiKeyHeader()
    {
        var factory = new HttpClientRequestFactory(
            new Uri("http://example.com"),
            [new KeyValuePair<string, string>(ApiKeyHttpTransportGuard.ApiKeyHeaderName, "test-key")]);
        var request = factory.Create(factory.GetEndpoint("/intake"));

        Func<Task> action = async () => { await request.GetAsync(); };

        await action.Should().ThrowAsync<ApiKeyHttpTransportException>();
    }

    [Fact]
    public void HttpClientRequestFactoryConfiguresOwnedProtectedHandler()
    {
        var factory = new HttpClientRequestFactory(
            new Uri("https://example.com"),
            [new KeyValuePair<string, string>(ApiKeyHttpTransportGuard.ApiKeyHeaderName, "test-key")],
            automaticDecompression: DecompressionMethods.GZip | DecompressionMethods.Deflate);

        var handler = GetHandler(factory);
        handler.AllowAutoRedirect.Should().BeFalse();
        handler.AutomaticDecompression.Should().Be(DecompressionMethods.GZip | DecompressionMethods.Deflate);
    }

    [Fact]
    public void HttpClientRequestFactoryDisablesProxyForPlaintextLoopback()
    {
        var factory = new HttpClientRequestFactory(
            new Uri("http://localhost"),
            [new KeyValuePair<string, string>(ApiKeyHttpTransportGuard.ApiKeyHeaderName, "test-key")]);
        factory.SetProxy(new WebProxy("http://example.com"), credential: null);

        var handler = GetHandler(factory);

        handler.UseProxy.Should().BeFalse();
    }

    [Fact]
    public void HttpClientRequestFactoryRejectsProtectedApiKeyWithCallerOwnedHandler()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };

        var action = () => new HttpClientRequestFactory(
            new Uri("https://example.com"),
            [new KeyValuePair<string, string>(ApiKeyHttpTransportGuard.ApiKeyHeaderName, "test-key")],
            handler);

        action.Should().Throw<ApiKeyHttpTransportException>();
    }

    [Fact]
    public void HttpClientRequestRejectsApiKeyAddedToKeylessFactoryRequest()
    {
        var factory = new HttpClientRequestFactory(
            new Uri("https://example.com"),
            []);
        var request = factory.Create(factory.GetEndpoint("/intake"));

        var action = () => request.AddHeader(ApiKeyHttpTransportGuard.ApiKeyHeaderName.ToLowerInvariant(), "test-key");

        action.Should().Throw<ApiKeyHttpTransportException>();
    }

    [Theory]
    [InlineData(true, "unix:///tmp/apm.socket")]
    [InlineData(false, "http://localhost:8126")]
    public void TestOptimizationFactoryUsesProtectedHttpTransportOnlyInAgentlessMode(bool agentless, string agentUri)
    {
        const string apiKey = "test-key";
        var values = new NameValueCollection();
        values.Add(ConfigurationKeys.AgentUri, agentUri);
        var source = new NameValueConfigurationSource(values);
        var settings = new TestOptimizationSettings(source, NullConfigurationTelemetry.Instance);
        settings.SetAgentlessConfiguration(agentless, apiKey, "https://example.com");
        var management = new TestOptimizationTracerManagement(settings);
        var tracerSettings = settings.InitializeTracerSettings(source);

        var factory = management.GetRequestFactory(tracerSettings).Should().BeOfType<HttpClientRequestFactory>().Subject;

        GetClient(factory).DefaultRequestHeaders.Contains(ApiKeyHttpTransportGuard.ApiKeyHeaderName).Should().Be(agentless);
    }

    private static HttpClientHandler GetHandler(HttpClientRequestFactory factory)
    {
        var field = typeof(HttpClientRequestFactory).GetField("_handler", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return field!.GetValue(factory).Should().BeOfType<HttpClientHandler>().Subject;
    }

    private static HttpClient GetClient(HttpClientRequestFactory factory)
    {
        var field = typeof(HttpClientRequestFactory).GetField("_client", BindingFlags.Instance | BindingFlags.NonPublic);
        field.Should().NotBeNull();
        return field!.GetValue(factory).Should().BeOfType<HttpClient>().Subject;
    }
#endif
}
