// <copyright file="AspNetCore5AsmToggle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests
{
    public class AspNetCore5AsmToggle : AspNetBase, IDisposable
    {
        private const string LogFileNamePrefix = "dotnet-tracer-managed-";

        public AspNetCore5AsmToggle(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: nameof(AspNetCore5AsmToggle))
        {
            SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "500");
        }

        // TODO addjust third parameter as the following PRs are merged:
        // * https://github.com/DataDog/dd-trace-dotnet/pull/3120
        // * https://github.com/DataDog/dd-trace-dotnet/pull/3171
        // the verify file names will need adjusting too
        [SkippableTheory]
        [InlineData(true, ApplyStates.ACKNOWLEDGED, RcmCapabilitiesIndices.AsmActivation)] // RcmCapabilitiesIndices.AsmActivation | RcmCapabilitiesIndices.AsmIpBlocking | RcmCapabilitiesIndices.AsmDdRules)]
        [InlineData(false, ApplyStates.UNACKNOWLEDGED, 0)] // RcmCapabilitiesIndices.AsmIpBlocking | RcmCapabilitiesIndices.AsmDdRules)]
        [InlineData(null, ApplyStates.ACKNOWLEDGED, RcmCapabilitiesIndices.AsmActivation)] // RcmCapabilitiesIndices.AsmActivation | RcmCapabilitiesIndices.AsmIpBlocking | RcmCapabilitiesIndices.AsmDdRules)]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityToggling(bool? enableSecurity, uint expectedState, byte expectedCapabilities)
        {
            var timeoutLogEntry = TimeSpan.FromSeconds(20);
            var url = "/Health/?[$slice]=value";
            var agent = await RunOnSelfHosted(enableSecurity);
            var settings = VerifyHelper.GetSpanVerifierSettings(enableSecurity, expectedState, expectedCapabilities);

            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{SampleProcessName}*");

            var spans1 = await SendRequestsAsync(agent, url);

            agent.SetupRcm(Output, new[] { ((object)new AsmFeatures { Asm = new Asm { Enabled = false } }, "1") }, "ASM_FEATURES");

            var request1 = await agent.WaitRcmRequestAndReturnLast();
            if (enableSecurity == true)
            {
                await logEntryWatcher.WaitForLogEntry("AppSec is now Disabled, coming from remote config: true", timeoutLogEntry);
            }

            CheckAckState(request1, expectedState, null, "First RCM call");
            CheckCapabilities(request1, expectedCapabilities, "First RCM call");

            var spans2 = await SendRequestsAsync(agent, url);

            agent.SetupRcm(Output, new[] { ((object)new AsmFeatures { Asm = new Asm { Enabled = true } }, "2") }, "ASM_FEATURES");
            var request2 = await agent.WaitRcmRequestAndReturnLast();
            if (enableSecurity != false)
            {
                await logEntryWatcher.WaitForLogEntry("AppSec is now Enabled, coming from remote config: true", timeoutLogEntry);
            }

            CheckAckState(request2, expectedState, null, "Second RCM call");
            CheckCapabilities(request2, expectedCapabilities, "Second RCM call");

            var spans3 = await SendRequestsAsync(agent, url);

            var spans = new List<MockSpan>();
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            spans.AddRange(spans3);

            await VerifySpans(spans.ToImmutableList(), settings, true);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestRemoteConfigError()
        {
            var enableSecurity = true;
            var url = "/Health/?[$slice]=value";
            var agent = await RunOnSelfHosted(enableSecurity);
            var settings = VerifyHelper.GetSpanVerifierSettings();

            var spans1 = await SendRequestsAsync(agent, url);

            agent.SetupRcm(Output, new[] { ((object)"haha, you weren't expect this!", "1") }, "ASM_FEATURES");

            var request = await agent.WaitRcmRequestAndReturnLast();
            CheckAckState(request, ApplyStates.ERROR, "Error converting value \"haha, you weren't expect this!\" to type 'Datadog.Trace.AppSec.AsmFeatures'. Path '', line 1, position 32.", "First RCM call");

            await VerifySpans(spans1.ToImmutableList(), settings, true);
        }

        private void CheckAckState(GetRcmRequest request, uint expectedState, string expectedError, string message)
        {
            var state = request?.Client?.State?.ConfigStates?.SingleOrDefault(x => x.Product == "ASM_FEATURES");

            state.Should().NotBeNull();
            state.ApplyState.Should().Be(expectedState, message);
            state.ApplyError.Should().Be(expectedError, message);
        }

        private void CheckCapabilities(GetRcmRequest request, byte expectedState, string message)
        {
            var capabilities = BitConverter.ToInt32(request?.Client?.Capabilities);
            capabilities.Should().Be(expectedState, message);
        }
    }
}
#endif
