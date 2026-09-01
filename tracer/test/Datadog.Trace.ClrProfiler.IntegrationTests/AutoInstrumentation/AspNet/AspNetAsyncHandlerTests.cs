// <copyright file="AspNetAsyncHandlerTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.AspNet
{
#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

    [Collection("IisTests")]
    public class AspNetAsyncHandlerTestsCallTarget : AspNetAsyncHandlerTests
    {
        public AspNetAsyncHandlerTestsCallTarget(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output)
        {
        }
    }

    public abstract class AspNetAsyncHandlerTests : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;

        public AspNetAsyncHandlerTests(IisFixture iisFixture, ITestOutputHelper output)
            : base("AspNetAsyncHandler", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/shutdown";
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet.request" => span.IsAspNet(metadataSchemaVersion),
                "aspnet-mvc.request" => span.IsAspNetMvc(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        [SkippableFact]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        public async Task SubmitsTraces()
        {
            var spans = await GetWebServerSpans("/test", _iisFixture.Agent, _iisFixture.HttpPort, expectedHttpStatusCode: HttpStatusCode.OK, expectedSpanCount: 2, filterServerSpans: false);

            spans.Should().HaveCount(2);

            var customSpan = spans.First(s => s.Name == "HttpHandler");

            customSpan.ParentId.Should().NotBeNull("traces should be correlated");
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;
    }
#endif
}
