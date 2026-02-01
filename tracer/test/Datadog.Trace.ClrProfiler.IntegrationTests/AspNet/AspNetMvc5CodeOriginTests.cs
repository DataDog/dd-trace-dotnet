// <copyright file="AspNetMvc5CodeOriginTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
using Datadog.Trace.TestHelpers;
using VerifyTests;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;
#pragma warning disable SA1402

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
        public async Task AddsCodeOriginToAspNetRequestSpan()
        {
            const string path = "/Home/Index";
            const int statusCode = 200;

            var spans = await GetWebServerSpans(
                path: _iisFixture.VirtualApplicationPath + path,
                agent: _iisFixture.Agent,
                httpPort: _iisFixture.HttpPort,
                expectedHttpStatusCode: HttpStatusCode.OK,
                expectedSpanCount: 2);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            AddCodeOriginScrubbers(settings);

            await Verifier.Verify(spans, settings)
                          .UseFileName($"{_testName}.__path={sanitisedPath}_statusCode={statusCode}");
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;

        private static void AddCodeOriginScrubbers(VerifySettings settings)
        {
            // File paths are machine dependent, and line/column numbers are sensitive to unrelated edits.
            // We scrub the values but keep the tags present in the snapshot.
            settings.AddRegexScrubber(
                new Regex(@"_dd\.code_origin\.frames\.(\d+)\.file: .*", VerifyHelper.RegOptions),
                "_dd.code_origin.frames.$1.file: <scrubbed>");

            settings.AddRegexScrubber(
                new Regex(@"_dd\.code_origin\.frames\.(\d+)\.(line|column): \d+", VerifyHelper.RegOptions),
                "_dd.code_origin.frames.$1.$2: 0");
        }
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

