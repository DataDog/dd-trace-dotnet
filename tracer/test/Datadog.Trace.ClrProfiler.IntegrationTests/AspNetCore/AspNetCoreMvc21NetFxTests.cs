// <copyright file="AspNetCoreMvc21NetFxTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNetCore
{
    public class AspNetCoreMvc21NetFxTests : AspNetCoreMvcTestBase
    {
        public AspNetCoreMvc21NetFxTests(AspNetCoreTestFixture fixture, ITestOutputHelper output)
            : base("AspNetCoreMvc21", fixture, output, enableCallTarget: true, enableRouteTemplateResourceNames: true)
        {
        }

        [Fact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        public async Task MeetsAllAspNetCoreMvcExpectations()
        {
            await Fixture.TryStartApp(this);

            var path = "/";
            var spans = await Fixture.WaitForSpans(path);

            // We don't expect any spans for netfx
            spans.Should().BeEmpty();
        }
    }
}
#endif
