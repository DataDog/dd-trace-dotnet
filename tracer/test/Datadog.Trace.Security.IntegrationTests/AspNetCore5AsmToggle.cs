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
        public AspNetCore5AsmToggle(ITestOutputHelper outputHelper)
            : base("AspNetCore5", outputHelper, "/shutdown", testName: nameof(AspNetCore5AsmToggle))
        {
            SetEnvironmentVariable(ConfigurationKeys.Rcm.PollInterval, "500");
        }

        [SkippableTheory]
        [InlineData(true, ApplyStates.ACKNOWLEDGED)]
        [InlineData(false, ApplyStates.UNACKNOWLEDGED)]
        [InlineData(null, ApplyStates.ACKNOWLEDGED)]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityToggling(bool? enableSecurity, uint expectedState)
        {
            var url = "/Health/?[$slice]=value";
            var agent = await RunOnSelfHosted(enableSecurity);
            var settings = VerifyHelper.GetSpanVerifierSettings(enableSecurity, expectedState);

            var spans1 = await SendRequestsAsync(agent, url);

            agent.SetupRcm(Output, new[] { ((object)new Features() { Asm = new Asm() { Enabled = false } }, "1") }, "FEATURES");

            var request1 = await agent.WaitRcmRequestAndReturnLast();
            CheckAckState(request1, expectedState, null, "First RCM call");
            // even the request show the applied state seems extra time is needed before it's active
            await Task.Delay(1500);

            var spans2 = await SendRequestsAsync(agent, url);

            agent.SetupRcm(Output, new[] { ((object)new Features() { Asm = new Asm() { Enabled = true } }, "2") }, "FEATURES");

            var request2 = await agent.WaitRcmRequestAndReturnLast();
            CheckAckState(request2, expectedState, null, "Second RCM call");
            // even the request show the applied state seems extra time is needed before it's active
            await Task.Delay(1500);

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

            agent.SetupRcm(Output, new[] { ((object)"haha, you weren't expect this!", "1") }, "FEATURES");

            var request = await agent.WaitRcmRequestAndReturnLast();
            CheckAckState(request, ApplyStates.ERROR, "Error converting value \"haha, you weren't expect this!\" to type 'Datadog.Trace.Configuration.Features'. Path '', line 1, position 32.", "First RCM call");

            await VerifySpans(spans1.ToImmutableList(), settings, true);
        }

        private void CheckAckState(GetRcmRequest request, uint expectedState, string expectedError, string message)
        {
            var state = request?.Client?.State?.ConfigStates?.FirstOrDefault(x => x.Product == "FEATURES");

            state.Should().NotBeNull();
            state.ApplyState.Should().Be(expectedState, message);
            state.ApplyError.Should().Be(expectedError, message);
        }
    }
}
#endif
