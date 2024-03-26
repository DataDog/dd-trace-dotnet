// <copyright file="AspNetCore5AsmData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Rcm;
using Datadog.Trace.AppSec.Rcm.Models.AsmData;
using Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    public class AspNetCore5AsmDataSecurityDisabledBlockingRequestIp : AspNetCore5AsmDataBlockingRequestIp
    {
        public AspNetCore5AsmDataSecurityDisabledBlockingRequestIp(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: false, testName: "AspNetCore5AsmDataSecurityDisabled")
        {
        }
    }

    public class AspNetCore5AsmDataSecurityEnabledBlockingRequestIp : AspNetCore5AsmDataBlockingRequestIp
    {
        public AspNetCore5AsmDataSecurityEnabledBlockingRequestIp(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: "AspNetCore5AsmDataSecurityEnabled")
        {
        }
    }

    public class AspNetCore5AsmDataSecurityDisabledBlockingUser : AspNetCore5AsmDataBlockingUser
    {
        public AspNetCore5AsmDataSecurityDisabledBlockingUser(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: false, testName: "AspNetCore5AsmDataSecurityDisabled")
        {
        }
    }

    public class AspNetCore5AsmDataSecurityEnabledBlockingUser : AspNetCore5AsmDataBlockingUser
    {
        public AspNetCore5AsmDataSecurityEnabledBlockingUser(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: "AspNetCore5AsmDataSecurityEnabled")
        {
        }
    }

    public abstract class AspNetCore5AsmDataBlockingRequestIp : RcmBase
    {
        public AspNetCore5AsmDataBlockingRequestIp(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableSecurity, string testName)
            : base(fixture, outputHelper, enableSecurity, testName: testName)
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        }

        [SkippableTheory]
        [InlineData("blocking-ips", "/")]
        [Trait("RunOnWindows", "True")]
        public async Task RunTest(string test, string url)
        {
            await TryStartApp();
            var agent = Fixture.Agent;
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            // we want to see the ip here
            var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
            var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] { test, sanitisedUrl });
            var spanBeforeAsmData = await SendRequestsAsync(agent, url);

            await agent.SetupRcmAndWait(
                Output,
                new[]
                {
                    ((object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp } } } } },
                     RcmProducts.AsmData, nameof(AspNetCore5AsmDataBlockingRequestIp)),
                    (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } },
                     RcmProducts.AsmData, nameof(AspNetCore5AsmDataBlockingRequestIp) + "2"),
                });

            var spanAfterAsmData = await SendRequestsAsync(agent, url);
            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            await VerifySpans(spans.ToImmutableList(), settings);
        }
    }

    public class AspNetCore5AsmDataSecurityEnabledBlockingRequestIpOneClick : RcmBase
    {
        public AspNetCore5AsmDataSecurityEnabledBlockingRequestIpOneClick(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: null, testName: "AspNetCore5AsmDataSecurityEnabled")
        {
        }

        [SkippableTheory]
        [InlineData("blocking-ips-oneclick", "/")]
        [Trait("RunOnWindows", "True")]
        public async Task RunTest(string test, string url)
        {
            await TryStartApp();
            var agent = Fixture.Agent;
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            // we want to see the ip here
            var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
            var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] { test, sanitisedUrl });
            var spanBeforeAsmData = await SendRequestsAsync(agent, url);
            var asmFeaturesFileId = nameof(AspNetCore5AsmDataSecurityEnabledBlockingRequestIpOneClick);

            var request = await agent.SetupRcmAndWait(Output, new[] { ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = true } }, RcmProducts.AsmFeatures, asmFeaturesFileId) });
            request.Should().NotBeNull();
            request.CachedTargetFiles.Should().HaveCount(1);
            var spanAfterAsmActivated = await SendRequestsAsync(agent, url);

            var fileId = nameof(AspNetCore5AsmDataSecurityEnabledBlockingRequestIpOneClick) + Guid.NewGuid();
            var fileId2 = nameof(AspNetCore5AsmDataSecurityEnabledBlockingRequestIpOneClick) + Guid.NewGuid();

            request = await agent.SetupRcmAndWait(
                          Output,
                          new[]
                          {
                              ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = true } }, RcmProducts.AsmFeatures, asmFeaturesFileId),
                              (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp }, new Data { Expiration = null, Value = "123.1.1.1" } } } } }, RcmProducts.AsmData, fileId),
                              ((object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } }, RcmProducts.AsmData,
                               fileId2)
                          });
            request.Should().NotBeNull();
            request.CachedTargetFiles.Should().HaveCount(3);
            request.CachedTargetFiles.Any(c => c.Path.Contains(fileId)).Should().BeTrue();
            request.CachedTargetFiles.Any(c => c.Path.Contains(fileId2)).Should().BeTrue();
            var spanAfterAsmData = await SendRequestsAsync(agent, url);
            spanAfterAsmData.First().GetTag(Tags.AppSecEvent).Should().NotBeNull();

            request = await agent.SetupRcmAndWait(
                          Output,
                          new[]
                          {
                              ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = false } }, RcmProducts.AsmFeatures,
                                asmFeaturesFileId),
                              (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp }, new Data { Expiration = null, Value = "123.1.1.1" } } } } },
                               RcmProducts.AsmData, fileId),
                              ((object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } },
                               RcmProducts.AsmData, fileId2)
                          });
            request.Should().NotBeNull();
            request.CachedTargetFiles.Should().HaveCount(3);
            var spanAfterAsmDeactivated = await SendRequestsAsync(agent, url);

            request = await agent.SetupRcmAndWait(
                          Output,
                          new[]
                          {
                              ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = true } }, RcmProducts.AsmFeatures, asmFeaturesFileId), (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp }, new Data { Expiration = null, Value = "123.1.1.1" } } } } },
                                  RcmProducts.AsmData, fileId),
                              ((object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } },
                               RcmProducts.AsmData, fileId2)
                          });
            request.Should().NotBeNull();
            request.CachedTargetFiles.Should().HaveCount(3);
            var spanAfterAsmDataReactivated = await SendRequestsAsync(agent, url);

            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmActivated);
            spans.AddRange(spanAfterAsmData);
            spans.AddRange(spanAfterAsmDeactivated);
            spans.AddRange(spanAfterAsmDataReactivated);

            await VerifySpans(spans.ToImmutableList(), settings);
        }
    }

    public abstract class AspNetCore5AsmDataBlockingUser : RcmBase
    {
        public AspNetCore5AsmDataBlockingUser(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableSecurity, string testName)
            : base(fixture, outputHelper, enableSecurity, testName: testName)
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        }

        [SkippableTheory]
        [InlineData("blocking-user", "/user")]
        [Trait("RunOnWindows", "True")]
        public async Task RunTest(string test, string url)
        {
            await TryStartApp();
            var agent = Fixture.Agent;
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            var settings = VerifyHelper.GetSpanVerifierSettings(parameters: new object[] { test, sanitisedUrl });
            var spanBeforeAsmData = await SendRequestsAsync(agent, url);

            // make sure this is unique if it s going to be run parallel
            var fileId = nameof(AspNetCore5AsmDataBlockingUser) + Guid.NewGuid();

            await agent.SetupRcmAndWait(
                Output,
                new[] { ((object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_users", Type = "data_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = "user3" } } } } }, RcmProducts.AsmData, acknowledgedId: fileId) });
            var spanAfterAsmData = await SendRequestsAsync(agent, url);
            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            await VerifySpans(spans.ToImmutableList(), settings, true);
        }
    }
}
#endif
