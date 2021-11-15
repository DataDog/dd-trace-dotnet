// <copyright file="AspNetVersionConflictTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Linq;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNet
{
    [Collection("IisTests")]
    public class AspNetVersionConflictTests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetVersionConflictTests(IisFixture iisFixture, ITestOutputHelper output)
            : base("AspNet.VersionConflict", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated);
        }

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        public async Task SubmitsTraces()
        {
            var spans = await GetWebServerSpans("/home/sendrequest", _iisFixture.Agent, _iisFixture.HttpPort, System.Net.HttpStatusCode.OK, expectedSpanCount: 6, filterServerSpans: false);

            Output.WriteLine($"Received {spans.Count} spans");

            foreach (var span in spans)
            {
                Output.WriteLine($"{span.Name} - {span.TraceId} - {span.SpanId} - {span.ParentId} - {span.Resource}");
            }

            // Using Single to make sure there is no orphaned span
            var rootSpan = spans.Single(s => s.ParentId == null);

            rootSpan.Name.Should().Be("aspnet.request");

            var mvcSpan = spans.Single(s => s.ParentId == rootSpan.SpanId);

            mvcSpan.TraceId.Should().Be(rootSpan.TraceId);
            mvcSpan.Name.Should().Be("aspnet-mvc.request");

            var manualSpan = spans.Single(s => s.ParentId == mvcSpan.SpanId);

            manualSpan.TraceId.Should().Be(rootSpan.TraceId);
            manualSpan.Name.Should().Be("Manual");

            var httpSpan = spans.Single(s => s.ParentId == manualSpan.SpanId);

            httpSpan.TraceId.Should().Be(rootSpan.TraceId);
            httpSpan.Name.Should().Be("http.request");
        }
    }
}

#endif
