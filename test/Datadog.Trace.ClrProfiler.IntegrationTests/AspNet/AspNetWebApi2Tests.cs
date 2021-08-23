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
    public class AspNetWebApi2TestsCallsiteClassic : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallsiteClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallsiteIntegrated : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallsiteIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallsiteClassicWithFeatureFlag : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallsiteClassicWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallsiteIntegratedWithFeatureFlag : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallsiteIntegratedWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetClassic : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetIntegrated : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetClassicWithFeatureFlag : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetClassicWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetIntegratedWithFeatureFlag : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetIntegratedWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [UsesVerify]
    public abstract class AspNetWebApi2Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        public AspNetWebApi2Tests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool classicMode, bool enableRouteTemplateResourceNames)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget);
            if (enableRouteTemplateResourceNames)
            {
                SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");
            }

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = nameof(AspNetWebApi2Tests)
                      + (enableCallTarget ? ".CallSite" : ".CallTarget")
                      + (classicMode ? ".Classic" : ".Integrated")
                      + (enableRouteTemplateResourceNames ? ".NoFF" : ".WithFF");
        }

        public static TheoryData<string, int> Data() => new()
        {
            { "/api/environment", 200 },
            { "/api/absolute-route", 200 },
            { "/api/delay/0", 200 },
            { "/api/delay-optional", 200 },
            { "/api/delay-optional/1", 200 },
            { "/api/delay-async/0", 200 },
            { "/api/transient-failure/true", 200 },
            { "/api/transient-failure/false", 500 },
            { "/api/statuscode/201", 201 },
            { "/api/statuscode/503", 503 },
            { "/api2/delay/0", 200 },
            { "/api2/optional", 200 },
            { "/api2/optional/1", 200 },
            { "/api2/delayAsync/0", 200 },
            { "/api2/transientfailure/true", 200 },
            { "/api2/transientfailure/false", 500 },
            { "/api2/statuscode/201", 201 },
            { "/api2/statuscode/503", 503 },
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
