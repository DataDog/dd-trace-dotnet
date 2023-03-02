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
            var expectedState = EnableSecurity == false ? ApplyStates.UNACKNOWLEDGED : ApplyStates.ACKNOWLEDGED;

            var url = "/Health/?[$slice]=value";
            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings();

            var spans1 = await SendRequestsAsync(agent, url);
            var acknowledgedId = nameof(TestSecurityToggling) + Guid.NewGuid();

            var request1 = await agent.SetupRcmAndWait(Output, new[] { ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = false } }, acknowledgedId) }, "ASM_FEATURES", "first", new[] { acknowledgedId });

            RcmBase.CheckAckState(request1, "ASM_FEATURES", expectedState, null, "First RCM call");
            request1.Client.State.BackendClientState.Should().Be("first");

            RcmBase.CheckAckState(request1, "ASM_FEATURES", expectedState, null, "First RCM call");

            var spans2 = await SendRequestsAsync(agent, url);
            var acknowledgedId2 = nameof(TestSecurityToggling) + Guid.NewGuid();

            var request2 = await agent.SetupRcmAndWait(Output, new[] { ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = true } }, acknowledgedId2) }, "ASM_FEATURES", "second", new[] { acknowledgedId2 });

            RcmBase.CheckAckState(request2, "ASM_FEATURES", expectedState, null, "Second RCM call");

            var request3 = await agent.WaitRcmRequestAndReturnLast(appliedServiceNames: new[] { acknowledgedId2 });
            request3.Client.State.BackendClientState.Should().Be("second");
            var spans3 = await SendRequestsAsync(agent, url);

            var spans = new List<MockSpan>();
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            spans.AddRange(spans3);

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
            var acknowledgedId = nameof(TestRemoteConfigError) + Guid.NewGuid();

            var request = await agent.SetupRcmAndWait(Output, new[] { ((object)"haha, you weren't expect this!", acknowledgedId) }, "ASM_FEATURES", appliedServiceNames: new[] { acknowledgedId });

            RcmBase.CheckAckState(request, "ASM_FEATURES", ApplyStates.ERROR, "Error converting value \"haha, you weren't expect this!\" to type 'Datadog.Trace.AppSec.AsmFeatures'. Path '', line 1, position 32.", "First RCM call");

            await VerifySpans(spans1.ToImmutableList(), settings);
        }
    }
}
#endif
