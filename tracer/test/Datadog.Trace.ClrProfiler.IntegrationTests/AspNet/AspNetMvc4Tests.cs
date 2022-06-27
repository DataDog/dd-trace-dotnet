// <copyright file="AspNetMvc4Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name
using System.Linq;
using System.Net;
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

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetIntegratedWithRouteTemplateExpansion : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetIntegratedWithRouteTemplateExpansion(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableRouteTemplateResourceNames: true, enableRouteTemplateExpansion: true)
        {
        }
    }

    [UsesVerify]
    public abstract class AspNetMvc4Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        public AspNetMvc4Tests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableRouteTemplateResourceNames, bool enableRouteTemplateExpansion = false)
            : base("AspNetMvc4", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());
            SetEnvironmentVariable(ConfigurationKeys.ExpandRouteTemplatesEnabled, enableRouteTemplateExpansion.ToString());

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = nameof(AspNetMvc4Tests)
                      + (classicMode ? ".Classic" : ".Integrated")
                      + (enableRouteTemplateExpansion ? ".WithExpansion" :
                        (enableRouteTemplateResourceNames ?  ".WithFF" : ".NoFF"));
        }

        public static TheoryData<string, int> Data() => new()
        {
              { "/Admin", 200 }, // Contains child actions
              { "/Admin/Home", 200 }, // Contains child actions
              { "/Admin/Home/Index", 200 }, // Contains child actions
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
              { "/Home/badrequest?TransferRequest=true", 500 },
              { "/Home/BadRequestWithStatusCode?statuscode=401&TransferRequest=true", 401 },
              { "/Home/BadRequestWithStatusCode?statuscode=503&TransferRequest=true", 503 },
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

            var aspnetSpans = spans.Where(s => s.Name == "aspnet.request");
            foreach (var aspnetSpan in aspnetSpans)
            {
                var result = aspnetSpan.IsAspNet();
                Assert.True(result.Success, result.ToString());
            }

            var aspnetMvcSpans = spans.Where(s => s.Name == "aspnet-mvc.request");
            foreach (var aspnetMvcSpan in aspnetMvcSpans)
            {
                var result = aspnetMvcSpan.IsAspNetMvc();
                Assert.True(result.Success, result.ToString());
            }

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
