// <copyright file="AspNetMvc4Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetClassic : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetIntegrated : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetClassicWithFeatureFlag : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetClassicWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetIntegratedWithFeatureFlag : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetIntegratedWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [UsesVerify]
    public abstract class AspNetMvc4Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        public AspNetMvc4Tests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableRouteTemplateResourceNames)
            : base("AspNetMvc4", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = nameof(AspNetMvc4Tests)
                      + (classicMode ? ".Classic" : ".Integrated")
                      + (enableRouteTemplateResourceNames ? ".NoFF" : ".WithFF");
        }

        public static TheoryData<string, int> Data() => new()
        {
            { "/Admin", 200 },
            { "/Admin/Home", 200 },
            { "/Admin/Home/Index", 200 },
            { "/", 200 },
            { "/Home", 200 },
            { "/Home/Index", 200 },
            { "/Home/BadRequest", 500 },
            { "/Home/identifier", 500 },
            { "/Home/identifier/123", 200 },
            { "/Home/identifier/BadValue", 500 },
            { "/Home/OptionalIdentifier", 200 },
            { "/Home/OptionalIdentifier/123", 200 },
            { "/Home/OptionalIdentifier/BadValue", 200 },
            { "/Home/StatusCode?value=201", 201 },
            { "/Home/StatusCode?value=503", 503 },
        };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(string path, HttpStatusCode statusCode)
        {
            var spans = await GetWebServerSpans(path, _iisFixture.Agent, _iisFixture.HttpPort, statusCode);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }
    }
}
#endif
