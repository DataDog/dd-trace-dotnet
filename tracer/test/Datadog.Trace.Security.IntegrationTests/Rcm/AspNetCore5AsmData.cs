// <copyright file="AspNetCore5AsmData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    public class AspNetCore5AsmDataSecurityDisabled : AspNetCore5AsmData
    {
        public AspNetCore5AsmDataSecurityDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: false, testName: nameof(AspNetCore5AsmDataSecurityDisabled))
        {
        }
    }

    public class AspNetCore5AsmDataSecurityEnabled : AspNetCore5AsmData
    {
        public AspNetCore5AsmDataSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5AsmDataSecurityEnabled))
        {
        }

        [SkippableTheory]
        [InlineData("blocking-ips-oneclick", "/")]
        [Trait("RunOnWindows", "True")]
        public async Task TestBlockedRequestIpWithOneClickActivation(string test, string url)
        {
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            // we want to see the ip here
            var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
            var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] { test, sanitisedUrl });
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{Fixture.Process.ProcessName}*", LogDirectory);
            var spanBeforeAsmData = await SendRequestsAsync(Fixture.Agent, url);

            var product = new AsmDataProduct();
            Fixture.Agent.SetupRcm(
                Output,
                new[]
                {
                    (
                        (object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp } } } } }, "asm_data"),
                    (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } }, "asm_data_servicea"),
                },
                product.Name);

            var request1 = await Fixture.Agent.WaitRcmRequestAndReturnLast();
            var rulesUpdatedMessage = RulesUpdatedMessage();
            await logEntryWatcher.WaitForLogEntry($"1 {rulesUpdatedMessage}", LogEntryWatcherTimeout);

            var spanAfterAsmData = await SendRequestsAsync(Fixture.Agent, url);
            spanAfterAsmData.First().GetTag(Tags.AppSecEvent).Should().NotBeNull();
            Fixture.Agent.SetupRcm(Output, new[] { ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = false } }, "1") }, "ASM_FEATURES");
            var requestAfterDeactivation = await Fixture.Agent.WaitRcmRequestAndReturnLast();
            await logEntryWatcher.WaitForLogEntry(AppSecDisabledMessage(), LogEntryWatcherTimeout);

            var spanAfterAsmDeactivated = await SendRequestsAsync(Fixture.Agent, url);

            Fixture.Agent.SetupRcm(Output, new[] { ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = true } }, "1") }, "ASM_FEATURES");
            var requestAfterReactivation = await Fixture.Agent.WaitRcmRequestAndReturnLast();
            await logEntryWatcher.WaitForLogEntries(new[] { $"1 {rulesUpdatedMessage}", AppSecEnabledMessage() }, LogEntryWatcherTimeout);

            var spanAfterAsmDataReactivated = await SendRequestsAsync(Fixture.Agent, url);

            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            spans.AddRange(spanAfterAsmDeactivated);
            spans.AddRange(spanAfterAsmDataReactivated);

            await VerifySpans(spans.ToImmutableList(), settings);
        }
    }

    public abstract class AspNetCore5AsmData : RcmBase
    {
        public AspNetCore5AsmData(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool enableSecurity, string testName)
            : base(fixture, outputHelper, enableSecurity, testName: testName)
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        }

        [SkippableTheory]
        [InlineData("blocking-ips", "/")]
        [Trait("RunOnWindows", "True")]
        public async Task TestBlockedRequestIp(string test, string url)
        {
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{Fixture.Process.ProcessName}*", LogDirectory);
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            // we want to see the ip here
            var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
            var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] { test, sanitisedUrl });
            var spanBeforeAsmData = await SendRequestsAsync(Fixture.Agent, url);

            var product = new AsmDataProduct();
            Fixture.Agent.SetupRcm(
                Output,
                new[]
                {
                    (
                        (object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp } } } } }, "asm_data"),
                    (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } }, "asm_data_servicea"),
                },
                product.Name);

            var request1 = await Fixture.Agent.WaitRcmRequestAndReturnLast();
            if (EnableSecurity == true)
            {
                await logEntryWatcher.WaitForLogEntry($"1 {RulesUpdatedMessage()}", LogEntryWatcherTimeout);
            }
            else
            {
                await Task.Delay(1500);
            }

            var spanAfterAsmData = await SendRequestsAsync(Fixture.Agent, url);
            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            await VerifySpans(spans.ToImmutableList(), settings);
        }
    }
}
#endif
