// <copyright file="AspNetMvc5CodeOriginTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#nullable enable

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

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [UsesVerify]
    public abstract class AspNetMvc5CodeOriginTests : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;
        private readonly bool _classicMode;

        protected AspNetMvc5CodeOriginTests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            _classicMode = classicMode;
            _testName = $"{nameof(AspNetMvc5CodeOriginTests)}.{(classicMode ? "Classic" : "Integrated")}";

            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.Debugger.CodeOriginForSpansEnabled, "true");
        }

        public static TheoryData<string, int, string, string> Data => new()
        {
            { "/Home/Index", 200, "Index", "Samples.AspNetMvc5.Controllers.HomeController" },
            { "/delay-async/0", 200, "DelayAsync", "Samples.AspNetMvc5.Controllers.HomeController" },
        };

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
            spans.Single(s => s.Name == "aspnet-mvc.request").Tags.Should().NotContainKey("_dd.code_origin.type");

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            await Verifier.Verify(spans, settings)
                          .UseFileName($"{_testName}.__path={sanitisedPath}_statusCode={statusCode}");
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;
    }

    [Collection("IisTests")]
    public class AspNetMvc5CodeOriginTestsClassic : AspNetMvc5CodeOriginTests
    {
        public AspNetMvc5CodeOriginTestsClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5CodeOriginTestsIntegrated : AspNetMvc5CodeOriginTests
    {
        public AspNetMvc5CodeOriginTestsIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false)
        {
        }
    }
}

#endif
