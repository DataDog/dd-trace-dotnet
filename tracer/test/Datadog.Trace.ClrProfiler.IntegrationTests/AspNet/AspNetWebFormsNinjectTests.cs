// <copyright file="AspNetWebFormsNinjectTests.cs" company="Datadog">
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
    /// <summary>
    /// Integration tests for ASP.NET WebForms with Ninject dependency injection.
    /// Reproduces APMS-18833: Ninject's module loading creates a temporary AppDomain
    /// named "NinjectModuleLoader". The tracer intentionally skips registering its
    /// startup hook in that AppDomain. These tests verify that the tracer still
    /// correctly instruments HTTP requests in the main AppDomain after Ninject
    /// finishes its initialization.
    /// </summary>
    [Collection("IisTests")]
    public class AspNetWebFormsNinjectTests : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;

        public AspNetWebFormsNinjectTests(IisFixture iisFixture, ITestOutputHelper output)
            : base("WebForms.Ninject", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/account/login?shutdown=1";
        }

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet.request" => span.IsAspNet(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [InlineData("/", "GET /")]
        [InlineData("/Default", "GET /default")]
        [InlineData("/Account/Login", "GET /account/login")]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName)
        {
            await AssertAspNetSpanOnly(
                path,
                _iisFixture.Agent,
                _iisFixture.HttpPort,
                HttpStatusCode.OK,
                isError: false,
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
