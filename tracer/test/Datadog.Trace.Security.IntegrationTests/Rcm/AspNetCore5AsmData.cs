// <copyright file="AspNetCore5AsmData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    public class AspNetCore5AsmData : RcmBase
    {
        public AspNetCore5AsmData(ITestOutputHelper outputHelper)
            : base(outputHelper, testName: nameof(AspNetCore5AsmData))
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        }

        [SkippableTheory]
        [InlineData("blocking-ips", true, HttpStatusCode.Forbidden, "/")]
        [InlineData("blocking-ips", false, HttpStatusCode.OK, "/")]
        [Trait("RunOnWindows", "True")]
        public async Task TestBlockedRequestIp(string test, bool enableSecurity, HttpStatusCode expectedStatusCode, string url = DefaultAttackUrl)
        {
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{SampleProcessName}*");
            var agent = await RunOnSelfHosted(enableSecurity, "ruleset-withblockips.json");
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            // we want to see the ip here
            var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
            var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] { test, enableSecurity, (int)expectedStatusCode, sanitisedUrl });
            var spanBeforeAsmData = await SendRequestsAsync(agent, url);

            var product = new AsmDataProduct();
            agent.SetupRcm(
                Output,
                new[]
                {
                    (
                        (object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp } } } } }, "asm_data"),
                    (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } }, "asm_data_servicea"),
                },
                product.Name);

            var request1 = await agent.WaitRcmRequestAndReturnLast();
            if (enableSecurity)
            {
                await logEntryWatcher.WaitForLogEntry($"1 {RulesUpdatedMessage()}", logEntryWatcherTimeout);
            }
            else
            {
                await Task.Delay(1500);
            }

            var spanAfterAsmData = await SendRequestsAsync(agent, url);
            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            await VerifySpans(spans.ToImmutableList(), settings, true);
        }

        [SkippableTheory]
        [InlineData("blocking-ips-oneclick", true, HttpStatusCode.Forbidden, "/")]
        [Trait("RunOnWindows", "True")]
        public async Task TestBlockedRequestIpWithOneClickActivation(string test, bool enableSecurity, HttpStatusCode expectedStatusCode, string url = DefaultAttackUrl)
        {
            var agent = await RunOnSelfHosted(enableSecurity, "ruleset-withblockips.json");
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            // we want to see the ip here
            var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
            var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] { test, enableSecurity, (int)expectedStatusCode, sanitisedUrl });
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{SampleProcessName}*");
            var spanBeforeAsmData = await SendRequestsAsync(agent, url);

            var product = new AsmDataProduct();
            agent.SetupRcm(
                Output,
                new[]
                {
                    (
                        (object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp } } } } }, "asm_data"),
                    (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } }, "asm_data_servicea"),
                },
                product.Name);

            var request1 = await agent.WaitRcmRequestAndReturnLast();
            var rulesUpdatedMessage = RulesUpdatedMessage();
            await logEntryWatcher.WaitForLogEntry($"1 {rulesUpdatedMessage}", logEntryWatcherTimeout);

            var spanAfterAsmData = await SendRequestsAsync(agent, url);
            spanAfterAsmData.First().GetTag(Tags.AppSecEvent).Should().NotBeNull();
            agent.SetupRcm(Output, new[] { ((object)new AsmFeatures { Asm = new Asm { Enabled = false } }, "1") }, "ASM_FEATURES");
            var requestAfterDeactivation = await agent.WaitRcmRequestAndReturnLast();
            await logEntryWatcher.WaitForLogEntry(AppSecDisabledMessage(), logEntryWatcherTimeout);

            var spanAfterAsmDeactivated = await SendRequestsAsync(agent, url);

            agent.SetupRcm(Output, new[] { ((object)new AsmFeatures { Asm = new Asm { Enabled = true } }, "1") }, "ASM_FEATURES");
            var requestAfterReactivation = await agent.WaitRcmRequestAndReturnLast();
            await logEntryWatcher.WaitForLogEntries(new[] { $"1 {rulesUpdatedMessage}", AppSecEnabledMessage() }, logEntryWatcherTimeout);

            var spanAfterAsmDataReactivated = await SendRequestsAsync(agent, url);

            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            spans.AddRange(spanAfterAsmDeactivated);
            spans.AddRange(spanAfterAsmDataReactivated);

            await VerifySpans(spans.ToImmutableList(), settings, true);
        }
    }
}
#endif
