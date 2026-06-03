// <copyright file="AspNetWebApi2CodeOriginTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using FluentAssertions;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public abstract class AspNetWebApi2CodeOriginTests : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;
        private readonly bool _classicMode;

        protected AspNetWebApi2CodeOriginTests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            _classicMode = classicMode;
            _testName = $"{nameof(AspNetWebApi2CodeOriginTests)}.{(classicMode ? "Classic" : "Integrated")}";

            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "true");
        }

        public static TheoryData<string, int, string, string> Data => new()
        {
            { "/api/environment", 200, "Environment", "Samples.AspNetMvc5.Controllers.ApiController" },
            { "/api/delay-async/0", 200, "DelayAsync", "Samples.AspNetMvc5.Controllers.ApiController" },
        };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet.request" => span.IsAspNet(metadataSchemaVersion),
                "aspnet-webapi.request" => span.IsAspNetWebApi2(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(Data))]
        public async Task AddsCodeOriginToAspNetRequestSpan(string path, int statusCode, string methodName, string typeName)
        {
            var spans = await GetWebServerSpans(
                path: _iisFixture.VirtualApplicationPath + path,
                agent: _iisFixture.Agent,
                httpPort: _iisFixture.HttpPort,
                expectedHttpStatusCode: (HttpStatusCode)statusCode,
                expectedSpanCount: 2);

            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: "sample", isExternalSpan: false);

            var rootSpan = spans.Single(s => s.Name == "aspnet.request");
            rootSpan.Tags.Should().ContainKey("_dd.code_origin.type").WhoseValue.Should().Be("entry");
            rootSpan.Tags.Should().ContainKey("_dd.code_origin.frames.0.method").WhoseValue.Should().Be(methodName);
            rootSpan.Tags.Should().ContainKey("_dd.code_origin.frames.0.type").WhoseValue.Should().Be(typeName);
            spans.Single(s => s.Name == "aspnet-webapi.request").Tags.Should().NotContainKey("_dd.code_origin.type");

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            await Verifier.Verify(spans, settings)
                          .UseFileName($"{_testName}.__path={sanitisedPath}_statusCode={statusCode}");
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;
    }

    [Collection("IisTests")]
    public class AspNetWebApi2CodeOriginTestsClassic : AspNetWebApi2CodeOriginTests
    {
        public AspNetWebApi2CodeOriginTestsClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2CodeOriginTestsIntegrated : AspNetWebApi2CodeOriginTests
    {
        public AspNetWebApi2CodeOriginTestsIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false)
        {
        }
    }
}

#endif
