// <copyright file="AspNetMvc5Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Linq;
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

    [Collection("IisTests")]
    public class AspNetMvc5TestsVirtualAppIntegratedWithFeatureFlag : AspNetMvc5Tests
    {
        public AspNetMvc5TestsVirtualAppIntegratedWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, virtualApp: true, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsModuleOnlyClassic : AspNetMvc5ModuleOnlyTests
    {
        public AspNetMvc5TestsModuleOnlyClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, virtualApp: false, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsModuleOnlyIntegrated : AspNetMvc5ModuleOnlyTests
    {
        public AspNetMvc5TestsModuleOnlyIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, virtualApp: false, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsModuleOnlyVirtualAppIntegrated : AspNetMvc5ModuleOnlyTests
    {
        public AspNetMvc5TestsModuleOnlyVirtualAppIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, virtualApp: true, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsModuleOnlyVirtualAppIntegratedWithFeatureFlag : AspNetMvc5ModuleOnlyTests
    {
        public AspNetMvc5TestsModuleOnlyVirtualAppIntegratedWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, virtualApp: true, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [UsesVerify]
    public abstract class AspNetMvc5Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        protected AspNetMvc5Tests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableRouteTemplateResourceNames, bool enableRouteTemplateExpansion = false, bool virtualApp = false)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());
            SetEnvironmentVariable(ConfigurationKeys.ExpandRouteTemplatesEnabled, enableRouteTemplateExpansion.ToString());

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            if (virtualApp)
            {
                _iisFixture.VirtualApplicationPath = "/my-app";
            }

            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = nameof(AspNetMvc5Tests)
                      + (virtualApp ? ".VirtualApp" : string.Empty)
                      + (classicMode ? ".Classic" : ".Integrated")
                      + (enableRouteTemplateExpansion     ? ".WithExpansion" :
                         enableRouteTemplateResourceNames ? ".WithFF" : ".NoFF");
        }

        public static TheoryData<string, int> Data => new()
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

            // Append virtual directory to the actual request
            var spans = await GetWebServerSpans(_iisFixture.VirtualApplicationPath + path, _iisFixture.Agent, _iisFixture.HttpPort, statusCode);

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

    [UsesVerify]
    public abstract class AspNetMvc5ModuleOnlyTests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        protected AspNetMvc5ModuleOnlyTests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableRouteTemplateResourceNames, bool virtualApp = false)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());

            // Disable the MVC part, so we can't back propagate any details to the tracing module
            SetEnvironmentVariable(ConfigurationKeys.DisabledIntegrations, nameof(Configuration.IntegrationId.AspNetMvc));

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            if (virtualApp)
            {
                _iisFixture.VirtualApplicationPath = "/my-app";
            }

            _iisFixture.TryStartIis(this, classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
            _testName = nameof(AspNetMvc5Tests)
                      + "ModuleOnly"
                      + (virtualApp ? ".VirtualApp" : string.Empty)
                      + (classicMode ? ".Classic" : ".Integrated");
        }

        public static TheoryData<string, int> Data() => new()
        {
            { "/Home/Index", 200 },
            { "/Home/Get", 500 },
            { "/badrequest", 500 },
            { "/statuscode/503", 503 },
            { "/BadRequestWithStatusCode/401", 500 }, // the exception thrown here overrides the status code set
            { "/BadRequestWithStatusCode/401?TransferRequest=true", 401 },
            { "/BadRequestWithStatusCode/503?TransferRequest=true", 503 },
        };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(string path, HttpStatusCode statusCode)
        {
            // TransferRequest cannot be called in the classic mode, so we expect a 500 when this happens
            if (_testName.Contains(".Classic") && path.ToLowerInvariant().Contains("transferrequest"))
            {
                statusCode = (HttpStatusCode)500;
            }

            // Append virtual directory if there is one
            var spans = await GetWebServerSpans(_iisFixture.VirtualApplicationPath + path, _iisFixture.Agent, _iisFixture.HttpPort, statusCode, expectedSpanCount: 1);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .DisableRequireUniquePrefix()
                          .UseTypeName(_testName);
        }
    }
}
#endif
