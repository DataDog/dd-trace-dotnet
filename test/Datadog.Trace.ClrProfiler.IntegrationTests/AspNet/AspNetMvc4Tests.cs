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
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [CollectionDefinition("IisTests", DisableParallelization = true)]
    [Collection("IisTests")]
    public class AspNetMvc4TestsCallsiteClassic : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallsiteClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallsiteIntegrated : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallsiteIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallsiteClassicWithFeatureFlag : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallsiteClassicWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallsiteIntegratedWithFeatureFlag : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallsiteIntegratedWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetClassic : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetIntegrated : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetClassicWithFeatureFlag : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetClassicWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetIntegratedWithFeatureFlag : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetIntegratedWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [UsesVerify]
    public abstract class AspNetMvc4Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        public AspNetMvc4Tests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool classicMode, bool enableRouteTemplateResourceNames)
            : base("AspNetMvc4", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget);
            if (enableRouteTemplateResourceNames)
            {
                SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");
            }

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this, classicMode);
            _testName = nameof(AspNetMvc4Tests)
                      + (enableCallTarget ? ".CallSite" : ".CallTarget")
                      + (classicMode ? ".Classic" : ".Integrated")
                      + (enableRouteTemplateResourceNames ? ".NoFF" : ".WithFF")
                      + (RuntimeInformation.ProcessArchitecture == Architecture.X64 ? ".X64" : ".X86"); // assume that arm is the same
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

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(string path, HttpStatusCode statusCode)
        {
            var spans = await GetWebServerSpans(path, _iisFixture.Agent, _iisFixture.HttpPort, statusCode);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseTypeName(_testName);
        }
    }
}
#endif
