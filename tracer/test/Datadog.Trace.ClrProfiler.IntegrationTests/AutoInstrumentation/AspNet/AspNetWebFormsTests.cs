// <copyright file="AspNetWebFormsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetWebFormsTests : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;

        // NOTE: Would pass this in addition to the name/output to the new constructor if we removed the Samples.WebForms copied project in favor of the demo repo source project...
        // $"../dd-trace-demo/dotnet-coffeehouse/Datadog.Coffeehouse.WebForms",
        public AspNetWebFormsTests(IisFixture iisFixture, ITestOutputHelper output)
            : base("WebForms", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/account/login?shutdown=1";
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet.request" => span.IsAspNet(metadataSchemaVersion),
                "aspnet-mvc.request" => span.IsAspNetMvc(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [InlineData("/Account/Login", "GET /account/login", false)]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName,
            bool isError)
        {
            await AssertAspNetSpanOnly(
                path,
                _iisFixture.Agent,
                _iisFixture.HttpPort,
                HttpStatusCode.OK,
                isError,
                expectedErrorType: null,
                expectedErrorMessage: null,
                SpanTypes.Web,
                expectedResourceName,
                "1.0.0");
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;
    }
}

#endif
