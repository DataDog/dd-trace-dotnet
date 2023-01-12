// <copyright file="AspNetCore5AsmToggle.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

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
            SetHttpPort(Fixture.HttpPort);
            var settings = VerifyHelper.GetSpanVerifierSettings();

            var spans1 = await SendRequestsAsync(Fixture.Agent, url);

            var request = await Fixture.Agent.SetupRcmAndWait(Output, new[] { ((object)"haha, you weren't expect this!", "1") }, "ASM_FEATURES");

            RcmBase.CheckAckState(request, "ASM_FEATURES", ApplyStates.ERROR, "Error converting value \"haha, you weren't expect this!\" to type 'Datadog.Trace.AppSec.AsmFeatures'. Path '', line 1, position 32.", "First RCM call");

            await VerifySpans(spans1.ToImmutableList(), settings, true);
        }
    }

    public class AspNetCore5AsmToggleSecurityNull : AspNetCore5AsmToggle
    {
        public AspNetCore5AsmToggleSecurityNull(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: null, testName: nameof(AspNetCore5AsmToggleSecurityNull))
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

        // TODO adjust third parameter as the following PRs are merged:
        // * https://github.com/DataDog/dd-trace-dotnet/pull/3120
        // * https://github.com/DataDog/dd-trace-dotnet/pull/3171
        // the verify file names will need adjusting too
        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestSecurityToggling()
        {
            uint expectedState = EnableSecurity == false ? ApplyStates.UNACKNOWLEDGED : ApplyStates.ACKNOWLEDGED;
            uint expectedCapabilities = EnableSecurity == false ?
                    (RcmCapabilitiesIndices.AsmIpBlockingUInt32 | RcmCapabilitiesIndices.AsmDdRulesUInt32)
                    : (RcmCapabilitiesIndices.AsmActivationUInt32 | RcmCapabilitiesIndices.AsmIpBlockingUInt32 | RcmCapabilitiesIndices.AsmDdRulesUInt32);

            var url = "/Health/?[$slice]=value";
            await TryStartApp();
            SetHttpPort(Fixture.HttpPort);
            var settings = VerifyHelper.GetSpanVerifierSettings();
            using var logEntryWatcher = new LogEntryWatcher($"{LogFileNamePrefix}{Fixture.Process.ProcessName}*", LogDirectory);

            var spans1 = await SendRequestsAsync(Fixture.Agent, url);

            var request1 = await Fixture.Agent.SetupRcmAndWait(Output, new[] { ((object)new AsmFeatures() { Asm = new AsmFeature() { Enabled = false } }, "1") }, "ASM_FEATURES", "first");

            RcmBase.CheckAckState(request1, "ASM_FEATURES", expectedState, null, "First RCM call");
            CheckCapabilities(request1, expectedCapabilities, "First RCM call");
            request1.Client.State.BackendClientState.Should().Be("first");
            if (EnableSecurity == true)
            {
                await logEntryWatcher.WaitForLogEntry(AppSecDisabledMessage(), LogEntryWatcherTimeout);
            }

            RcmBase.CheckAckState(request1, "ASM_FEATURES", expectedState, null, "First RCM call");
            CheckCapabilities(request1, expectedCapabilities, "First RCM call");

            var spans2 = await SendRequestsAsync(Fixture.Agent, url);

            var request2 = await Fixture.Agent.SetupRcmAndWait(Output, new[] { ((object)new AsmFeatures() { Asm = new AsmFeature() { Enabled = true } }, "2") }, "ASM_FEATURES", "second");

            RcmBase.CheckAckState(request2, "ASM_FEATURES", expectedState, null, "Second RCM call");
            CheckCapabilities(request2, expectedCapabilities, "Second RCM call");
            if (EnableSecurity != false)
            {
                await logEntryWatcher.WaitForLogEntry(AppSecEnabledMessage(), LogEntryWatcherTimeout);
            }

            var spans3 = await SendRequestsAsync(Fixture.Agent, url);

            var request3 = await Fixture.Agent.WaitRcmRequestAndReturnLast();
            request3.Client.State.BackendClientState.Should().Be("second");

            var spans = new List<MockSpan>();
            spans.AddRange(spans1);
            spans.AddRange(spans2);
            spans.AddRange(spans3);

            await VerifySpans(spans.ToImmutableList(), settings);
        }
    }
}
#endif
