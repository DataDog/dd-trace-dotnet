// <copyright file="AspNetCore5RcmClient.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER

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
    public class AspNetCore5RcmClient : RcmBase
    {
        public AspNetCore5RcmClient(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
            : base(fixture, outputHelper, enableSecurity: true, testName: nameof(AspNetCore5RcmClient))
        {
        }

        [SkippableFact]
        [Trait("RunOnWindows", "True")]
        public async Task TestExtraServiceField()
        {
            await TryStartApp();
            var agent = Fixture.Agent;
            var settings = VerifyHelper.GetSpanVerifierSettings();

            var request0 = await agent.SetupRcmAndWait(Output, Enumerable.Empty<(object Config, string ProductName, string Id)>(), timeoutInMilliseconds: RemoteConfigTestHelper.WaitForAcknowledgmentTimeout);
            request0.Should().NotBeNull();
            request0.Client.ClientTracer.ExtraServices.Should().BeNull();

            var spans1 = await SendRequestsAsync(agent, 2, "/createextraservice/?serviceName=extraVegetables");

            var request1 = await agent.SetupRcmAndWait(Output, Enumerable.Empty<(object Config, string ProductName, string Id)>(), timeoutInMilliseconds: RemoteConfigTestHelper.WaitForAcknowledgmentTimeout);
            request1.Should().NotBeNull();
            request1.Client.ClientTracer.ExtraServices.Should().NotBeNull();
            request1.Client.ClientTracer.ExtraServices.Should().HaveCount(1);
            request1.Client.ClientTracer.ExtraServices.Should().HaveElementAt(0, "extraVegetables");

            var spans2 = await SendRequestsAsync(agent, 2, "/createextraservice/?serviceName=ExTrAvEgEtAbLeS");

            var request2 = await agent.SetupRcmAndWait(Output, Enumerable.Empty<(object Config, string ProductName, string Id)>(), timeoutInMilliseconds: RemoteConfigTestHelper.WaitForAcknowledgmentTimeout);
            request2.Should().NotBeNull();
            request2.Client.ClientTracer.ExtraServices.Should().NotBeNull();
            request2.Client.ClientTracer.ExtraServices.Should().HaveCount(1);
            request2.Client.ClientTracer.ExtraServices.Should().HaveElementAt(0, "extraVegetables");

            var allSpans = new List<MockSpan>();
            allSpans.AddRange(spans1);
            allSpans.AddRange(spans2);
            await VerifySpans(allSpans.ToImmutableList(), settings, testName: $"{GetTestName()}_{nameof(TestExtraServiceField)}");
        }
    }
}
#endif
