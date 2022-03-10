// <copyright file="AspNetMvc5Tests.cs" company="Datadog">
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
    public class AspNetMvc5TestsCallTargetClassic : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsCallTargetIntegrated : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsCallTargetClassicWithFeatureFlag : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetClassicWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsCallTargetIntegratedWithFeatureFlag : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetIntegratedWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsCallTargetIntegratedWithRouteTemplateExpansion : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetIntegratedWithRouteTemplateExpansion(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableRouteTemplateResourceNames: true, enableRouteTemplateExpansion: true)
        {
        }
    }

    [UsesVerify]
    public abstract class AspNetMvc5Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        public AspNetMvc5Tests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableRouteTemplateResourceNames, bool enableRouteTemplateExpansion = false)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());
            SetEnvironmentVariable(ConfigurationKeys.ExpandRouteTemplatesEnabled, enableRouteTemplateExpansion.ToString());

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = nameof(AspNetMvc5Tests)
                      + (classicMode ? ".Classic" : ".Integrated")
                      + (enableRouteTemplateExpansion ? ".WithExpansion" :
                        (enableRouteTemplateResourceNames ?  ".WithFF" : ".NoFF"));
        }

        public static TheoryData<string, int> Data() => new()
        {
            { "/DataDog", 200 }, // Contains child actions
            { "/DataDog/DogHouse", 200 }, // Contains child actions
            { "/DataDog/DogHouse/Woof", 200 }, // Contains child actions
            { "/", 200 },
            { "/Home", 200 },
            { "/Home/Index", 200 },
            { "/Home/Get", 500 },
            { "/Home/Get/3", 200 },
            { "/delay/0", 200 },
            { "/delay-async/0", 200 },
            { "/delay-optional", 200 },
            { "/delay-optional/1", 200 },
            { "/badrequest", 500 },
            { "/statuscode/201", 201 },
            { "/statuscode/503", 503 },
            { "/badrequest?TransferRequest=true", 500 },
            { "/BadRequestWithStatusCode/401?TransferRequest=true", 401 },
            { "/BadRequestWithStatusCode/503?TransferRequest=true", 503 },
            { "/graphql/GetAllFoo", 200 }, // Slug in route template
        };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(string path, HttpStatusCode statusCode)
        {
            // TransferRequest cannot be called in the classic mode, so we expect a 500 when this happens
            var toLowerPath = path.ToLower();
            if (_testName.Contains(".Classic") && toLowerPath.Contains("badrequest") && toLowerPath.Contains("transferrequest"))
            {
                statusCode = (HttpStatusCode)500;
            }

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
