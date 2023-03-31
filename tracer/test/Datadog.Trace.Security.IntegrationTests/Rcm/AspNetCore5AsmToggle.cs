// <copyright file="AspNetCore5AsmToggle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Datadog.Trace.AppSec;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.Rcm
{
    public class AspNetCore5AsmToggleSecurityDefault : AspNetCore5AsmToggle
    {
        public AspNetCore5AsmToggleSecurityDefault(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: null, testName: nameof(AspNetCore5AsmToggleSecurityDefault))
        {
        }
    }

    public class AspNetCore5AsmToggleSecurityDisabled : AspNetCore5AsmToggle
    {
        public AspNetCore5AsmToggleSecurityDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: false, testName: nameof(AspNetCore5AsmToggleSecurityDisabled))
        {
        }
    }

    public class AspNetCore5AsmToggleSecurityEnabled : AspNetCore5AsmToggle
    {
        public AspNetCore5AsmToggleSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5AsmToggleSecurityEnabled))
        {
        }
    }

    public abstract class AspNetCore5AsmToggle : RcmBase
    {
        public AspNetCore5AsmToggle(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool? enableSecurity, string testName)
            : base(fixture, outputHelper, enableSecurity: enableSecurity, testName: testName)
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityToggling()
        {
            // we acknowledge either way, data is changed in memory
            var expectedState = ApplyStates.ACKNOWLEDGED;

            var url = "/Health/?[$slice]=value";
            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings();

            var span0nominalState = await SendRequestsAsync(agent, url);

            var request1 = await agent.SetupRcmAndWait(Output, new[] { ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = false } }, "ASM_FEATURES", nameof(TestSecurityToggling)) }, timeoutInMilliseconds: EnableSecurity is false ? 5000 : RemoteConfigTestHelper.WaitForAcknowledgmentTimeout);

            if (EnableSecurity == false)
            {
                // we dont subscribe to any product if security is set locally to true or false
                request1.Should().BeNull();
                return;
            }

            if (EnableSecurity == true)
            {
                // we subscribe to other products but dont apply anything in this scenario
                request1.Should().NotBeNull();
                request1.CachedTargetFiles.Should().HaveCount(0);
                return;
            }

            request1.Should().NotBeNull();
            request1.CachedTargetFiles.Should().HaveCount(1);

            CheckAckState(request1, "ASM_FEATURES", 1, expectedState, null, "First RCM call");

            var span1ShouldStillBeDisabled = await SendRequestsAsync(agent, url);

            var request2 = await agent.SetupRcmAndWait(Output, new[] { ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = true } }, "ASM_FEATURES", nameof(TestSecurityToggling)) });
            request2.Should().NotBeNull();
            request2.CachedTargetFiles.Should().HaveCount(1);

            CheckAckState(request2, "ASM_FEATURES", 1, expectedState, null, "First RCM call");
            var spans2ShouldBeEnabled = await SendRequestsAsync(agent, url);

            var request3 = await agent.SetupRcmAndWait(Output, new List<(object Config, string ProductId, string Id)>());
            request3.Should().NotBeNull();
            request3.CachedTargetFiles.Should().BeEmpty();
            var span3ConfigurationRemovedShouldBeDisabled = await SendRequestsAsync(agent, url);

            var spans = new List<MockSpan>();
            spans.AddRange(span0nominalState);
            spans.AddRange(span1ShouldStillBeDisabled);
            spans.AddRange(spans2ShouldBeEnabled);
            spans.AddRange(span3ConfigurationRemovedShouldBeDisabled);

            await VerifySpans(spans.ToImmutableList(), settings);
        }
    }

    public class AspNetCore5AsmToggleConfigError : RcmBase
    {
        public AspNetCore5AsmToggleConfigError(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5AsmToggleConfigError))
        {
            SetEnvironmentVariable(ConfigurationKeys.DebugEnabled, "0");
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestRemoteConfigError()
        {
            var url = "/Health/?[$slice]=value";
            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings();

            var spans1 = await SendRequestsAsync(agent, url);
            var request = await agent.SetupRcmAndWait(Output, new[] { ((object)"haha, you weren't expect this!", "ASM_FEATURES", nameof(TestRemoteConfigError)) });

            RcmBase.CheckAckState(request, "ASM_FEATURES", 1, ApplyStates.ERROR, "Error converting value \"haha, you weren't expect this!\" to type 'Datadog.Trace.AppSec.AsmFeatures'. Path '', line 1, position 32.", "First RCM call");

            await VerifySpans(spans1.ToImmutableList(), settings);
        }
    }
}
#endif
