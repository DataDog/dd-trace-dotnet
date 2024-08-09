// <copyright file="AspNetCore5AsmFeatureUserId.cs" company="Datadog">
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
    public class AspNetCore5AsmFeatureUserIdSecurityRemoteActivated(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : AspNetCore5AsmFeatureUserId(fixture, outputHelper, enableSecurity: null, testName: nameof(AspNetCore5AsmFeatureUserIdSecurityRemoteActivated));

    public class AspNetCore5AsmFeatureUserIdSecurityEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : AspNetCore5AsmFeatureUserId(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5AsmFeatureUserIdSecurityEnabled));

    public abstract class AspNetCore5AsmFeatureUserId : RcmBase
    {
        public AspNetCore5AsmFeatureUserId(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper, bool? enableSecurity, string testName)
            : base(fixture, outputHelper, enableSecurity, testName)
        {
            EnableRasp(false);
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestChangeUserIdCollection()
        {
            var url = "/Account/Index";
            var bodyString = "Input.UserName=TestUser&Input.Password=test";

            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings();

            var active = ((object)new AsmFeatures { Asm = new AsmFeature { Enabled = true } }, "ASM_FEATURES", nameof(TestChangeUserIdCollection) + "Activate");
            if (EnableSecurity is not true)
            {
                var request0 = await agent.SetupRcmAndWait(Output, new[] { active }, timeoutInMilliseconds: EnableSecurity is false ? 5000 : RemoteConfigTestHelper.WaitForAcknowledgmentTimeout);
                request0.Should().NotBeNull();
            }

            await SendRequestsAsync(agent, "/account/reset-memory-db");
            await SendRequestsAsync(agent, "/account/logout");

            var span0Ident = await SendRequestsAsync(agent, url, bodyString, 1, 1, string.Empty, contentType: "application/x-www-form-urlencoded");

            await SendRequestsAsync(agent, "/account/reset-memory-db");
            await SendRequestsAsync(agent, "/account/logout");

            var span = span0Ident.First();
            Output.WriteLine($"usr.id: {span.Tags["usr.id"]}");

            var anonMode = ((object)new AsmFeatures { AutoUserInstrum = new AutoUserInstrum { Mode = "anon" } }, "ASM_FEATURES", nameof(TestChangeUserIdCollection));
            var request1Files = EnableSecurity is true ? new[] { anonMode } : new[] { active, anonMode };
            var request1 = await agent.SetupRcmAndWait(Output, request1Files, timeoutInMilliseconds: EnableSecurity is false ? 5000 : RemoteConfigTestHelper.WaitForAcknowledgmentTimeout);
            request1.Should().NotBeNull();

            var span1Anon = await SendRequestsAsync(agent, url, bodyString, 1, 1, string.Empty, contentType: "application/x-www-form-urlencoded");

            await SendRequestsAsync(agent, "/account/reset-memory-db");
            await SendRequestsAsync(agent, "/account/logout");

            var disabledMode = ((object)new AsmFeatures { AutoUserInstrum = new AutoUserInstrum { Mode = "disabled" } }, "ASM_FEATURES", nameof(TestChangeUserIdCollection));
            var request2Files = EnableSecurity is true ? new[] { disabledMode } : new[] { active, disabledMode };
            var request2 = await agent.SetupRcmAndWait(Output, request2Files, timeoutInMilliseconds: EnableSecurity is false ? 5000 : RemoteConfigTestHelper.WaitForAcknowledgmentTimeout);
            request2.Should().NotBeNull();

            var spans2Disabled = await SendRequestsAsync(agent, url, bodyString, 1, 1, string.Empty, contentType: "application/x-www-form-urlencoded");

            var spans = new List<MockSpan>();
            spans.AddRange(span0Ident);
            spans.AddRange(span1Anon);
            spans.AddRange(spans2Disabled);

            await VerifySpans(spans.ToImmutableList(), settings);
        }
    }
}
#endif
