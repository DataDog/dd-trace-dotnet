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
using Datadog.Trace.AppSec.Rcm.Models.AsmFeatures;
using Datadog.Trace.Configuration;
using Datadog.Trace.RemoteConfigurationManagement;
using Datadog.Trace.RemoteConfigurationManagement.Protocol;
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
            request1.Should().NotBeNull();

            void CheckRequest(GetRcmRequest associatedRcmRequest, int fileNumberIfSecurityCanBeToggled)
            {
                if (EnableSecurity != null)
                {
                    // AsmFeatures is requested when security is enabled, as it contains info about userId collection
                    if (EnableSecurity is false)
                    {
                        associatedRcmRequest.CachedTargetFiles.Should().BeEmpty();
                    }

                    // Other products may be included, but none of the ASM ones should be
                    var asmProducts = new[] { RcmProducts.Asm, RcmProducts.AsmData, RcmProducts.AsmDd };
                    if (EnableSecurity == false)
                    {
                        associatedRcmRequest.Client.Products.Should().NotContain(asmProducts);
                    }
                    else
                    {
                        associatedRcmRequest.Client.Products.Should().Contain(asmProducts);
                    }
                }
                else
                {
                    associatedRcmRequest.CachedTargetFiles.Should().HaveCount(fileNumberIfSecurityCanBeToggled);
                    if (fileNumberIfSecurityCanBeToggled > 0)
                    {
                        CheckAckState(associatedRcmRequest, "ASM_FEATURES", 1, expectedState, null, "First RCM call");
                    }
                }
            }

            CheckRequest(request1, 1);
            var span1ShouldStillBeDisabled = await SendRequestsAsync(agent, url);

            var request2 = await agent.SetupRcmAndWait(Output, new[] { ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = true } }, "ASM_FEATURES", nameof(TestSecurityToggling)) });
            CheckRequest(request2, 1);

            var spans2ShouldBeEnabled = await SendRequestsAsync(agent, url);

            var request3 = await agent.SetupRcmAndWait(Output, new List<(object Config, string ProductId, string Id)>());
            CheckRequest(request3, 0);

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
            : base(fixture, outputHelper, enableSecurity: null, testName: nameof(AspNetCore5AsmToggleConfigError))
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

            CheckAckState(request, "ASM_FEATURES", 1, ApplyStates.ERROR, "Error converting value \"haha, you weren't expect this!\" to type 'Datadog.Trace.AppSec.Rcm.Models.AsmFeatures.AsmFeatures'. Path '', line 1, position 32.", "First RCM call");

            await VerifySpans(spans1.ToImmutableList(), settings);
        }
    }
}
#endif
