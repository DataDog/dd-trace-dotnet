// <copyright file="AspNetCore5AsmData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.RcmModels.AsmData;
using Datadog.Trace.Configuration;
using Datadog.Trace.Logging;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.Tagging;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    [Collection("AspNetCore Security Tests")]
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
        public async Task TestBlockedRequestIp(string test, bool enableSecurity, HttpStatusCode expectedStatusCode, string url)
        {
            using var fixture = RunOnSelfHosted(enableSecurity);
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{fixture.SampleProcessName}*", LogDirectory);
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            // we want to see the ip here
            var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
            var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] { test, enableSecurity, (int)expectedStatusCode, sanitisedUrl });
            var spanBeforeAsmData = await fixture.SendRequestsAsync(url);

            var product = new AsmDataProduct();
            fixture.Agent.SetupRcm(
                Output,
                new[]
                {
                    (
                        (object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp } } } } }, "asm_data"),
                    (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } }, "asm_data_servicea"),
                },
                product.Name);

            var request1 = await fixture.Agent.WaitRcmRequestAndReturnLast();
            if (enableSecurity)
            {
                await logEntryWatcher.WaitForLogEntry($"1 {RulesUpdatedMessage(fixture)}", LogEntryWatcherTimeout);
            }
            else
            {
                await Task.Delay(1500);
            }

            var spanAfterAsmData = await fixture.SendRequestsAsync(url);
            var spans = new List<MockSpan>();
            spans.AddRange(spanBeforeAsmData);
            spans.AddRange(spanAfterAsmData);
            await VerifySpans(spans.ToImmutableList(), settings, true);
        }

        [SkippableTheory]
        [InlineData("blocking-ips-oneclick", true, HttpStatusCode.Forbidden, "/")]
        [Trait("RunOnWindows", "True")]
        public async Task TestBlockedRequestIpWithOneClickActivation(string test, bool enableSecurity, HttpStatusCode expectedStatusCode, string url)
        {
            using var fixture = RunOnSelfHosted(enableSecurity);
            var sanitisedUrl = VerifyHelper.SanitisePathsForVerify(url);
            // we want to see the ip here
            var scrubbers = VerifyHelper.SpanScrubbers.Where(s => s.RegexPattern.ToString() != @"http.client_ip: (.)*(?=,)");
            var settings = VerifyHelper.GetSpanVerifierSettings(scrubbers: scrubbers, parameters: new object[] { test, enableSecurity, (int)expectedStatusCode, sanitisedUrl });
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{fixture.SampleProcessName}*", LogDirectory);
            var spanBeforeAsmData = await fixture.SendRequestsAsync(url);

            var product = new AsmDataProduct();
            fixture.Agent.SetupRcm(
                Output,
                new[]
                {
                    (
                        (object)new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 5545453532, Value = MainIp } } } } }, "asm_data"),
                    (new Payload { RulesData = new[] { new RuleData { Id = "blocked_ips", Type = "ip_with_expiration", Data = new[] { new Data { Expiration = 1545453532, Value = MainIp } } } } }, "asm_data_servicea"),
                },
                product.Name);

            var request1 = await fixture.Agent.WaitRcmRequestAndReturnLast();
            var rulesUpdatedMessage = RulesUpdatedMessage(fixture);
            await logEntryWatcher.WaitForLogEntry($"1 {rulesUpdatedMessage}", LogEntryWatcherTimeout);

            var spanAfterAsmData = await fixture.SendRequestsAsync(url);
            spanAfterAsmData.First().GetTag(Tags.AppSecEvent).Should().NotBeNull();
            fixture.Agent.SetupRcm(Output, new[] { ((object)new AsmFeatures { Asm = new Asm { Enabled = false } }, "1") }, "ASM_FEATURES");
            var requestAfterDeactivation = await fixture.Agent.WaitRcmRequestAndReturnLast();
            await logEntryWatcher.WaitForLogEntry(AppSecDisabledMessage(fixture), LogEntryWatcherTimeout);

            var spanAfterAsmDeactivated = await fixture.SendRequestsAsync(url);

            fixture.Agent.SetupRcm(Output, new[] { ((object)new AsmFeatures { Asm = new Asm { Enabled = true } }, "1") }, "ASM_FEATURES");
            var requestAfterReactivation = await fixture.Agent.WaitRcmRequestAndReturnLast();
            await logEntryWatcher.WaitForLogEntries(new[] { $"1 {rulesUpdatedMessage}", AppSecEnabledMessage(fixture) }, LogEntryWatcherTimeout);

            var spanAfterAsmDataReactivated = await fixture.SendRequestsAsync(url);

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
