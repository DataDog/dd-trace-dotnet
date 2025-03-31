// <copyright file="AspNetCore5Endpoints.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_0_OR_GREATER
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit.Abstractions;

namespace Datadog.Trace.Security.IntegrationTests.ApiSecurity
{
    public class AspNetCore5EndpointsEnabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : AspNetCoreEndpoints(fixture, outputHelper, "AspNetCore5", true)
    {
        public override async Task TestEndpointsCollection()
        {
            await base.TestEndpointsCollection();

            // Tests for specific endpoints that should be collected
            Endpoints.Should().Contain(e => e.Path == "/iast/executecommand" && e.Method == "GET");
            Endpoints.Should().Contain(e => e.Path == "/map_endpoint/sub_level");
        }
    }

    public class AspNetCore5EndpointsEnabledLimited(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : AspNetCoreEndpoints(fixture, outputHelper, sampleName: "AspNetCore5", true, 5);

    public class AspNetCore5EndpointsDisabled(AspNetCoreTestFixture fixture, ITestOutputHelper outputHelper)
        : AspNetCoreEndpoints(fixture, outputHelper, sampleName: "AspNetCore5", false);
}
#endif
