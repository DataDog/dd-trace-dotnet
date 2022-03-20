// <copyright file="AspNetWebApi2Tests.cs" company="Datadog">
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
    public class AspNetWebApi2TestsCallTargetClassic : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetIntegrated : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetClassicWithFeatureFlag : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetClassicWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetIntegratedWithFeatureFlag : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetIntegratedWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetIntegratedWithRouteTemplateExpansion : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetIntegratedWithRouteTemplateExpansion(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableRouteTemplateResourceNames: true, enableRouteTemplateExpansion: true)
        {
        }
    }

    [UsesVerify]
    public abstract class AspNetWebApi2Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        public AspNetWebApi2Tests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableRouteTemplateResourceNames, bool enableRouteTemplateExpansion = false)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());
            SetEnvironmentVariable(ConfigurationKeys.ExpandRouteTemplatesEnabled, enableRouteTemplateExpansion.ToString());

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = nameof(AspNetWebApi2Tests)
                      + (classicMode ? ".Classic" : ".Integrated")
                      + (enableRouteTemplateExpansion ? ".WithExpansion" :
                        (enableRouteTemplateResourceNames ?  ".WithFF" : ".NoFF"));
        }

        public static TheoryData<string, int, int> Data() => new()
        {
            { "/api/environment", 200, 2 },
            { "/api/absolute-route", 200, 2 },
            { "/api/delay/0", 200, 2 },
            { "/api/delay-optional", 200, 2 },
            { "/api/delay-optional/1", 200, 2 },
            { "/api/delay-async/0", 200, 2 },
            { "/api/transient-failure/true", 200, 2 },
            { "/api/transient-failure/false", 500, 3 },
            { "/api/statuscode/201", 201, 2 },
            { "/api/statuscode/503", 503, 2 },
            { "/api/constraints", 200, 2 },
            { "/api/constraints/201", 201, 2 },
            { "/api/TransferRequest/401", 401, 4 },
            { "/api/TransferRequest/503", 503, 4 },
            { "/api2/delay/0", 200, 2 },
            { "/api2/optional", 200, 2 },
            { "/api2/optional/1", 200, 2 },
            { "/api2/delayAsync/0", 200, 2 },
            { "/api2/transientfailure/true", 200, 2 },
            { "/api2/transientfailure/false", 500, 3 },
            { "/api2/statuscode/201", 201, 2 },
            { "/api2/statuscode/503", 503, 3 },

            // The global message handler will fail when ps=false
            // The per-route message handler is not invoked with the route /api2, so ts=true|false has no effect
            { "/api2/statuscode/201?ps=true&ts=true", 201, 2 },
            { "/api2/statuscode/201?ps=true&ts=false", 201, 2 },
            { "/api2/statuscode/201?ps=false&ts=true", 500, 2 },
            { "/api2/statuscode/201?ps=false&ts=false", 500, 2 },

            // The global message handler will fail when ps=false
            // The global and per-route message handler is invoked with the route /handler-api, so ts=false will also fail the request
            { "/handler-api/api?ps=true&ts=true", 200, 1 },
            { "/handler-api/api?ps=true&ts=false", 500, 2 },
            { "/handler-api/api?ps=false&ts=true", 500, 2 },
            { "/handler-api/api?ps=false&ts=false", 500, 2 },
        };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(string path, HttpStatusCode statusCode, int expectedSpanCount)
        {
            // TransferRequest cannot be called in the classic mode, so we expect a 500 when this happens
            var toLowerPath = path.ToLower();
            if (_testName.Contains(".Classic") && toLowerPath.Contains("transferrequest"))
            {
                statusCode = (HttpStatusCode)500;
            }

            var spans = await GetWebServerSpans(path, _iisFixture.Agent, _iisFixture.HttpPort, statusCode, expectedSpanCount);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Overriding the method name to _
            // Overriding the parameters to remove the expectedSpanCount parameter, which is necessary for operation but unnecessary for the filename
            await Verifier.Verify(spans, settings)
                          .UseFileName($"{_testName}.__path={sanitisedPath}_statusCode={(int)statusCode}");
        }
    }
}
#endif
