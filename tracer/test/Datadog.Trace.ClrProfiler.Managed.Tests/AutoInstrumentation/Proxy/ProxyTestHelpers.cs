// <copyright file="ProxyTestHelpers.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable

using System;
using System.Collections.Specialized;
using System.Globalization;
using Datadog.Trace.Agent;
using Datadog.Trace.ClrProfiler.AutoInstrumentation.Proxy;
using Datadog.Trace.Configuration;
using Datadog.Trace.Headers;
using Datadog.Trace.Sampling;
using Datadog.Trace.TestHelpers.TestTracer;
using Moq;

namespace Datadog.Trace.ClrProfiler.Managed.Tests.AutoInstrumentation.Proxy;

internal static class ProxyTestHelpers
{
    internal static ScopedTracer GetMockTracer(NameValueCollection? collection = null)
    {
        collection ??= new NameValueCollection { { ConfigurationKeys.FeatureFlags.InferredProxySpansEnabled, "true" } };
        IConfigurationSource source = new NameValueConfigurationSource(collection);
        var settings = new TracerSettings(source);
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();
        return TracerHelper.Create(settings, writerMock.Object, samplerMock.Object);
    }

    internal static NameValueHeadersCollection CreateValidHeaders(string? start = null)
    {
        start ??= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        var headers = new NameValueHeadersCollection([]);
        headers.Set(InferredProxyHeaders.Name, "aws-apigateway");
        headers.Set(InferredProxyHeaders.StartTime, start);
        headers.Set(InferredProxyHeaders.Domain, "test.api.com");
        headers.Set(InferredProxyHeaders.HttpMethod, "GET");
        headers.Set(InferredProxyHeaders.Path, "/api/test");
        headers.Set(InferredProxyHeaders.Stage, "prod");
        return headers;
    }

    internal static NameValueHeadersCollection CreateValidAzureHeaders(string? start = null)
    {
        start ??= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        var headers = new NameValueHeadersCollection([]);
        headers.Set(InferredProxyHeaders.Name, "azure-apim");
        headers.Set(InferredProxyHeaders.StartTime, start);
        headers.Set(InferredProxyHeaders.HttpMethod, "POST");
        headers.Set(InferredProxyHeaders.Path, "/api/v1/users");
        headers.Set(InferredProxyHeaders.Region, "canada central");
        return headers;
    }
}
