// <copyright file="AASTagsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.Agent;
using Datadog.Trace.Configuration;
using Datadog.Trace.PlatformHelpers;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.IntegrationTests.Tagging;

[AzureAppServicesRestorer]
public class AASTagsTests
{
    private readonly Tracer _tracer;
    private readonly MockApi _testApi;

    public AASTagsTests()
    {
        _testApi = new MockApi();

        var settings = new TracerSettings();
        var agentWriter = new AgentWriter(_testApi, statsAggregator: null, statsd: null, spanSampler: null);
        _tracer = new Tracer(settings, agentWriter, sampler: null, scopeManager: null, statsd: null);
    }

    [Fact]
    public async Task AasTagsShouldBeSerialized()
    {
        var vars = GetMockVariables();
        AzureAppServices.Metadata = new AzureAppServices(vars);

        using (_tracer.StartActiveInternal("root"))
        {
        }

        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();
        var deserializedSpan = traceChunks.Single().Single();
        AssertLocalRootSPan(deserializedSpan);
    }

    [Fact]
    public async Task NullAasTagsShouldNotCauseIssues()
    {
        var vars = GetNullMockVariables();
        AzureAppServices.Metadata = new AzureAppServices(vars);

        using (_tracer.StartActiveInternal("root"))
        {
        }

        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();
        var deserializedSpan = traceChunks.Single().Single();
        AssertNoTags(deserializedSpan);
    }

    [Fact]
    public async Task AasTagsShouldBeSerializedOnLocalRootSpans()
    {
        // We are going to create 2 chunks
        // Both chunks should contain all AAS tags on local root span

        // The first chunk will contain multiple spans
        // Each non local root spans should contain only aas.site.name and aas.site.type tags

        var vars = GetMockVariables();
        AzureAppServices.Metadata = new AzureAppServices(vars);

        ISpan span1;
        ISpan span11;
        ISpan span12;
        ISpan span121;

        using (var scope1 = _tracer.StartActive("1"))
        {
            span1 = scope1.Span;

            var traceContext = ((Scope)scope1).Span.Context.TraceContext;

            using (var scope11 = _tracer.StartActive("1.1"))
            {
                span11 = scope11.Span;
            }

            using (var scope12 = _tracer.StartActive("1.2"))
            {
                span12 = scope12.Span;

                using (var scope121 = _tracer.StartActive("1.2.1"))
                {
                    span121 = scope121.Span;
                }
            }

            // send the finished spans as one trace chunk
            traceContext.WriteClosedSpans();
        }

        // send the remaining spans as another trace chunk
        await _tracer.FlushAsync();
        var traceChunks = _testApi.Wait();

        // expected chunks:
        // [ 1.1, 1.2, 1.2.1 ]
        // [ 1 ]

        traceChunks.Should().HaveCount(2);    // 2 trace chunks
        traceChunks[0].Should().HaveCount(3); // 3 spans
        traceChunks[1].Should().HaveCount(1); // 1 span

        // chunk 0, both orphan spans should have all aas tags
        traceChunks[0]
           .Where(s => s.SpanId == span11.SpanId || s.SpanId == span12.SpanId)
           .Should()
           .HaveCount(2)
           .And.OnlyContain(s => s.ParentId == span1.SpanId)
            // I don't know why Assume doesn't work in that case but using SatisfyRespectively does the trick
           .And.SatisfyRespectively(s => AssertLocalRootSPan(s), s => AssertLocalRootSPan(s));

        // chunk 0, other spans should only have site name and site type
        traceChunks[0]
           .Where(s => s.SpanId == span121.SpanId)
           .Should()
           .HaveCount(1)
           .And.OnlyContain(s => s.ParentId == span12.SpanId)
           .And.Satisfy(s => AssertNonRootSPan(s));

        // chunk 1, root span should have the sampling priority
        traceChunks[1]
           .Should()
           .HaveCount(1)
           .And.OnlyContain(s => s.ParentId == null || s.ParentId == 0)
           .And.OnlyContain(s => s.SpanId == span1.SpanId)
           .And.Satisfy(s => AssertLocalRootSPan(s));
    }

    private bool AssertLocalRootSPan(MockSpan span)
    {
        span.GetTag(Tags.AzureAppServicesSiteName).Should().Be("SiteName");
        span.GetTag(Tags.AzureAppServicesSiteKind).Should().Be("app");
        span.GetTag(Tags.AzureAppServicesSiteType).Should().Be("app");
        span.GetTag(Tags.AzureAppServicesResourceGroup).Should().Be("SiteResourceGroup");
        span.GetTag(Tags.AzureAppServicesSubscriptionId).Should().Be("SubscriptionId");
        span.GetTag(Tags.AzureAppServicesResourceId).Should().Be("/subscriptions/subscriptionid/resourcegroups/siteresourcegroup/providers/microsoft.web/sites/sitename");
        span.GetTag(Tags.AzureAppServicesInstanceId).Should().Be("InstanceId");
        span.GetTag(Tags.AzureAppServicesInstanceName).Should().Be("InstanceName");
        span.GetTag(Tags.AzureAppServicesOperatingSystem).Should().Be("windows");
        span.GetTag(Tags.AzureAppServicesRuntime).Should().Be(FrameworkDescription.Instance.Name);
        span.GetTag(Tags.AzureAppServicesExtensionVersion).Should().Be("unknown");

        return true;
    }

    private bool AssertNonRootSPan(MockSpan span)
    {
        span.GetTag(Tags.AzureAppServicesSiteName).Should().Be("SiteName");
        span.GetTag(Tags.AzureAppServicesSiteType).Should().Be("app");
        span.GetTag(Tags.AzureAppServicesSiteKind).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesResourceGroup).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesSubscriptionId).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesResourceId).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesInstanceId).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesInstanceName).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesOperatingSystem).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesRuntime).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesExtensionVersion).Should().BeNull();

        return true;
    }

    private bool AssertNoTags(MockSpan span)
    {
        span.GetTag(Tags.AzureAppServicesSiteName).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesSiteType).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesSiteKind).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesResourceGroup).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesSubscriptionId).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesResourceId).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesInstanceId).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesInstanceName).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesOperatingSystem).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesRuntime).Should().BeNull();
        span.GetTag(Tags.AzureAppServicesExtensionVersion).Should().BeNull();

        return true;
    }

    private IDictionary GetNullMockVariables()
    {
        var vars = Environment.GetEnvironmentVariables();
        vars.Add(AzureAppServices.AzureAppServicesContextKey, "1");
        return vars;
    }

    private IDictionary GetMockVariables()
    {
        var vars = Environment.GetEnvironmentVariables();

        if (vars.Contains(AzureAppServices.InstanceNameKey))
        {
            // This is the COMPUTERNAME key which we'll remove for consistent testing
            vars.Remove(AzureAppServices.InstanceNameKey);
        }

        if (vars.Contains(ConfigurationKeys.DebugEnabled))
        {
            vars.Remove(ConfigurationKeys.DebugEnabled);
        }

        if (!vars.Contains(ConfigurationKeys.ApiKey))
        {
            // This is a needed configuration for the AAS extension
            vars.Add(ConfigurationKeys.ApiKey, "1");
        }

        vars.Add(AzureAppServices.AzureAppServicesContextKey, "1");
        vars.Add(AzureAppServices.WebsiteOwnerNameKey, $"SubscriptionId+ResourceGroup-EastUSwebspace");
        vars.Add(AzureAppServices.ResourceGroupKey, "SiteResourceGroup");
        vars.Add(AzureAppServices.SiteNameKey, "SiteName");
        vars.Add(AzureAppServices.OperatingSystemKey, "windows");
        vars.Add(AzureAppServices.InstanceIdKey, "InstanceId");
        vars.Add(AzureAppServices.InstanceNameKey, "InstanceName");
        return vars;
    }
}
