// <copyright file="TraceTaggingResultTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#nullable enable
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.AppSec.WafEncoding;
using Datadog.Trace.Configuration;
using Datadog.Trace.Configuration.Telemetry;
using Datadog.Trace.Headers;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.Security.Unit.Tests.Utils;
using Datadog.Trace.TestHelpers.TestTracer;
using FluentAssertions;
using Moq;
#if !NETFRAMEWORK
using Microsoft.AspNetCore.Http;
#else
using System.Web;
#endif
using Xunit;
using AppSecSecurity = Datadog.Trace.AppSec.Security;

namespace Datadog.Trace.Security.Unit.Tests
{
    public class TraceTaggingResultTests : WafLibraryRequiredTest
    {
        private const string RuleFile = "trace-tagging-rules.json";

        [Fact]
        public void GivenTraceTaggingV1_AttributesPopulated_KeepFalse_NoEvent()
        {
            var result = RunWafForUserAgent("TraceTagging/v1");

            AssertTraceTaggingAttributes(result, expectedAgentPrefix: "TraceTagging/v1");
            result.HasKeep.Should().BeTrue();
            result.Keep.Should().BeFalse();
            result.Data.Should().BeNullOrEmpty();
        }

        [Fact]
        public void GivenTraceTaggingV2_AttributesPopulated_KeepTrue_NoEvent()
        {
            var result = RunWafForUserAgent("TraceTagging/v2");

            AssertTraceTaggingAttributes(result, expectedAgentPrefix: "TraceTagging/v2");
            result.HasKeep.Should().BeTrue();
            result.Keep.Should().BeTrue();
            result.Data.Should().BeNullOrEmpty();
        }

        [Fact]
        public void GivenTraceTaggingV3_AttributesPopulated_KeepTrue_Event()
        {
            var result = RunWafForUserAgent("TraceTagging/v3");

            AssertTraceTaggingAttributes(result, expectedAgentPrefix: "TraceTagging/v3");
            result.HasKeep.Should().BeTrue();
            result.Keep.Should().BeTrue();
            result.Data.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void GivenTraceTaggingV4_AttributesPopulated_KeepFalse_Event()
        {
            var result = RunWafForUserAgent("TraceTagging/v4");

            AssertTraceTaggingAttributes(result, expectedAgentPrefix: "TraceTagging/v4");
            result.HasKeep.Should().BeTrue();
            result.Keep.Should().BeFalse();
            result.Data.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void GivenNoMatch_NoAttributesAndNoKeep()
        {
            var result = RunWafForUserAgent("Mozilla/5.0");

            result.WafSpanAttributes.Should().BeNullOrEmpty();
            result.ExtractSchemaDerivatives.Should().BeNullOrEmpty();
            result.Keep.Should().BeFalse();
            result.ShouldReportSecurityResult.Should().BeFalse();
        }

        [Fact]
        public void SchemaAttributes_GoToExtractSchemaBucket_NotWafSpanAttributes()
        {
            var initResult = CreateWaf();
            using var waf = initResult.Waf!;
            using var context = waf.CreateContext()!;

            var result = context.Run(
                new Dictionary<string, object>
                {
                    { AddressesConstants.RequestUriRaw, "http://localhost/" },
                    { AddressesConstants.RequestMethod, "GET" },
                    {
                        AddressesConstants.RequestBody,
                        new Dictionary<string, object> { { "p1", "/.adsensepostnottherenonobook" }, }
                    },
                    { AddressesConstants.WafContextProcessor, new Dictionary<string, bool> { { "extract-schema", true } } },
                },
                TimeoutMicroSeconds);

            result.Should().NotBeNull();
            result!.ExtractSchemaDerivatives.Should().NotBeNullOrEmpty();
            foreach (var key in result.ExtractSchemaDerivatives!.Keys)
            {
                key.Should().StartWith(WafConstants.AppSecSchemaPrefix);
            }

            if (result.WafSpanAttributes is not null)
            {
                foreach (var key in result.WafSpanAttributes.Keys)
                {
                    key.Should().NotStartWith(WafConstants.AppSecSchemaPrefix);
                }
            }
        }

        [Fact]
        public void TypedAttributes_AreKeptInWafSpanAttributes()
        {
            var result = CreateResult(
                new Dictionary<string, object?>
                {
                    { "keep", false },
                    {
                        "attributes",
                        new Dictionary<string, object?>
                        {
                            { WafConstants.AppSecSchemaPrefix + "body", "schema" },
                            { Tags.AppSecFpEndpoint, "fingerprint" },
                            { "_dd.appsec.trace.agent", "TraceTagging/v1" },
                            { "_dd.appsec.trace.signed", -42L },
                            { "_dd.appsec.trace.unsigned", 42UL },
                            { "_dd.appsec.trace.double", 3.14D },
                            { "_dd.appsec.trace.bool", true },
                        }
                    },
                });

            result.HasKeep.Should().BeTrue();
            result.Keep.Should().BeFalse();
            result.ExtractSchemaDerivatives.Should().ContainKey(WafConstants.AppSecSchemaPrefix + "body");
            result.FingerprintDerivatives.Should().ContainKey(Tags.AppSecFpEndpoint);
            result.WafSpanAttributes.Should().NotBeNullOrEmpty();
            result.WafSpanAttributes!["_dd.appsec.trace.agent"].Should().Be("TraceTagging/v1");
            AssertNumericWafSpanAttribute(result.WafSpanAttributes["_dd.appsec.trace.signed"], -42L);
            AssertNumericWafSpanAttribute(result.WafSpanAttributes["_dd.appsec.trace.unsigned"], 42L);
            result.WafSpanAttributes["_dd.appsec.trace.double"].Should().Be(3.14D);
            result.WafSpanAttributes["_dd.appsec.trace.bool"].Should().Be(true);
        }

        [Fact]
        public async Task TypedWafSpanAttributes_AreReportedAsTagsAndMetrics()
        {
            var settings = TracerSettings.Create(new Dictionary<string, object>());
            await using var tracer = TracerHelper.Create(settings);
            var rootTestScope = (Scope)tracer.StartActive("test.trace");

            var result = new Mock<IResult>();
            result.SetupGet(x => x.WafSpanAttributes).Returns(
                new Dictionary<string, object?>
                {
                    { "_dd.appsec.trace.agent", "TraceTagging/v1" },
                    { "_dd.appsec.trace.signed", -42L },
                    { "_dd.appsec.trace.unsigned", 42UL },
                    { "_dd.appsec.trace.double", 3.14D },
                    { "_dd.appsec.trace.bool", true },
                    { "_dd.appsec.trace.numeric_string", "42" },
                });

            new SecurityReporter(rootTestScope.Span, new NoopHttpTransport(), isRoot: true).TryReport(result.Object, blocked: false);

            rootTestScope.Span.GetTag("_dd.appsec.trace.agent").Should().Be("TraceTagging/v1");
            rootTestScope.Span.GetTag("_dd.appsec.trace.numeric_string").Should().Be("42");
            rootTestScope.Span.GetMetric("_dd.appsec.trace.signed").Should().Be(-42D);
            rootTestScope.Span.GetMetric("_dd.appsec.trace.unsigned").Should().Be(42D);
            rootTestScope.Span.GetMetric("_dd.appsec.trace.double").Should().Be(3.14D);
            rootTestScope.Span.GetMetric("_dd.appsec.trace.bool").Should().Be(1D);
            rootTestScope.Span.GetMetric("_dd.appsec.trace.numeric_string").Should().BeNull();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        [InlineData(true, false)]
        public async Task SchemaAttributes_KeepTraceRegardlessOfExplicitKeep(bool hasKeep, bool keep)
        {
            var settings = TracerSettings.Create(new Dictionary<string, object>());
            await using var tracer = TracerHelper.Create(settings);
            using var securityOverride = OverrideSecurityInstance();
            var rootTestScope = (Scope)tracer.StartActive("test.trace");
            rootTestScope.Span.Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep);

            var schemaTag = WafConstants.AppSecSchemaPrefix + "req.body";
            var result = new Mock<IResult>();
            result.SetupGet(x => x.ExtractSchemaDerivatives).Returns(
                new Dictionary<string, object?>
                {
                    { schemaTag, new Dictionary<string, object?> { { "type", "object" } } },
                });
            result.SetupGet(x => x.HasKeep).Returns(hasKeep);
            result.SetupGet(x => x.Keep).Returns(keep);

            new SecurityReporter(rootTestScope.Span, new NoopHttpTransport(), isRoot: true).TryReport(result.Object, blocked: false);

            rootTestScope.Span.GetTag(schemaTag).Should().NotBeNullOrEmpty();
            rootTestScope.Span.Context.TraceContext.SamplingPriority.Should().Be(SamplingPriorityValues.UserKeep);
        }

        [Fact]
        public async Task WafSpanAttributes_WithKeepFalse_DoNotForceUserKeep()
        {
            var settings = TracerSettings.Create(new Dictionary<string, object>());
            await using var tracer = TracerHelper.Create(settings);
            using var securityOverride = OverrideSecurityInstance();
            var rootTestScope = (Scope)tracer.StartActive("test.trace");
            rootTestScope.Span.Context.TraceContext.SetSamplingPriority(SamplingPriorityValues.AutoKeep);

            var result = new Mock<IResult>();
            result.SetupGet(x => x.WafSpanAttributes).Returns(
                new Dictionary<string, object?>
                {
                    { "_dd.appsec.trace.agent", "TraceTagging/v1" },
                    { "_dd.appsec.trace.integer", 42L },
                });
            result.SetupGet(x => x.HasKeep).Returns(true);
            result.SetupGet(x => x.Keep).Returns(false);

            new SecurityReporter(rootTestScope.Span, new NoopHttpTransport(), isRoot: true).TryReport(result.Object, blocked: false);

            rootTestScope.Span.GetTag("_dd.appsec.trace.agent").Should().Be("TraceTagging/v1");
            rootTestScope.Span.GetMetric("_dd.appsec.trace.integer").Should().Be(42D);
            rootTestScope.Span.Context.TraceContext.SamplingPriority.Should().Be(SamplingPriorityValues.AutoKeep);
        }

        private static void AssertTraceTaggingAttributes(IResult result, string expectedAgentPrefix)
        {
            result.WafSpanAttributes.Should().NotBeNullOrEmpty("trace-tagging rule should populate span attributes");
            result.WafSpanAttributes!.Should().ContainKey("_dd.appsec.trace.agent");
            result.WafSpanAttributes!.Should().ContainKey("_dd.appsec.trace.integer");

            var agentValue = result.WafSpanAttributes!["_dd.appsec.trace.agent"];
            agentValue.Should().BeOfType<string>();
            ((string)agentValue!).Should().StartWith(expectedAgentPrefix);
        }

        private static void AssertNumericWafSpanAttribute(object? value, long expected)
        {
            switch (value)
            {
                case long l:
                    l.Should().Be(expected);
                    break;
                case ulong u:
                    u.Should().Be((ulong)expected);
                    break;
                case double d:
                    d.Should().Be(expected);
                    break;
                default:
                    throw new Xunit.Sdk.XunitException($"Expected numeric WAF span attribute, but found {value?.GetType().FullName ?? "null"}.");
            }
        }

        private static IResult CreateResult(Dictionary<string, object?> returnValues, WafReturnCode returnCode = WafReturnCode.Match)
        {
            var encoder = new Encoder();
            using var encodedResult = encoder.Encode(returnValues, applySafetyLimits: true);
            var nativeResult = encodedResult.ResultDdwafObject;
            ulong duration = 0;
            return new Result(ref nativeResult, returnCode, ref duration, 0);
        }

        private static SecurityInstanceOverride OverrideSecurityInstance()
        {
            var previous = AppSecSecurity.Instance;
            var source = CreateConfigurationSource((ConfigurationKeys.AppSec.Enabled, "0"));
            var settings = new SecuritySettings(source, NullConfigurationTelemetry.Instance);
            var current = new AppSecSecurity(settings, rcmSubscriptionManager: Mock.Of<IRcmSubscriptionManager>());
            typeof(AppSecSecurity)
               .GetField("_rateLimiter", BindingFlags.Instance | BindingFlags.NonPublic)!
               .SetValue(current, new AppSecRateLimiter(settings.TraceRateLimit));

            AppSecSecurity.Instance = current;
            return new SecurityInstanceOverride(previous, current);
        }

        private IResult RunWafForUserAgent(string userAgent)
        {
            var initResult = CreateWaf(ruleFile: RuleFile);
            using var waf = initResult.Waf!;
            using var context = waf.CreateContext()!;

            var args = new Dictionary<string, object>
            {
                { AddressesConstants.RequestUriRaw, "http://localhost/waf/" },
                { AddressesConstants.RequestMethod, "GET" },
                {
                    AddressesConstants.RequestHeaderNoCookies,
                    new Dictionary<string, string[]> { { "user-agent", new[] { userAgent } } }
                },
            };

            var result = context.Run(args, TimeoutMicroSeconds);
            result.Should().NotBeNull();
            result!.Timeout.Should().BeFalse();
            return result;
        }

        private sealed class NoopHttpTransport : HttpTransportBase
        {
            public NoopHttpTransport()
            {
                IsHttpContextDisposed = true;
            }

            public override HttpContext Context => throw new System.NotImplementedException();

            internal override bool IsBlocked => false;

            internal override int? StatusCode => null;

            internal override IDictionary<string, object>? RouteData => null;

            internal override bool ReportedExternalWafsRequestHeaders { get; set; }

            internal override IHeadersCollection? GetRequestHeaders() => null;

            internal override IHeadersCollection GetResponseHeaders() => throw new System.NotImplementedException();

            internal override void MarkBlocked()
            {
            }
        }

        private sealed class SecurityInstanceOverride : System.IDisposable
        {
            private readonly AppSecSecurity _previous;
            private readonly AppSecSecurity _current;

            public SecurityInstanceOverride(AppSecSecurity previous, AppSecSecurity current)
            {
                _previous = previous;
                _current = current;
            }

            public void Dispose()
            {
                AppSecSecurity.Instance = _previous;
                _current.Dispose();
            }
        }
    }
}
