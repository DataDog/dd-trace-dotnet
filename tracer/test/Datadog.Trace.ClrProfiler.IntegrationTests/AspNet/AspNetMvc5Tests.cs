// <copyright file="AspNetMvc5Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using NUnit.Framework;
using VerifyNUnit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetMvc5TestsCallsiteClassic : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallsiteClassic()
            : base(enableCallTarget: false, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetMvc5TestsCallsiteIntegrated : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallsiteIntegrated()
            : base(enableCallTarget: false, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetMvc5TestsCallsiteClassicWithFeatureFlag : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallsiteClassicWithFeatureFlag()
            : base(enableCallTarget: false, classicMode: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public class AspNetMvc5TestsCallsiteIntegratedWithFeatureFlag : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallsiteIntegratedWithFeatureFlag()
            : base(enableCallTarget: false, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public class AspNetMvc5TestsCallTargetClassic : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetClassic()
            : base(enableCallTarget: true, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetMvc5TestsCallTargetIntegrated : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetIntegrated()
            : base(enableCallTarget: true, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetMvc5TestsCallTargetClassicWithFeatureFlag : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetClassicWithFeatureFlag()
            : base(enableCallTarget: true, classicMode: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public class AspNetMvc5TestsCallTargetIntegratedWithFeatureFlag : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetIntegratedWithFeatureFlag()
            : base(enableCallTarget: true, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public abstract class AspNetMvc5Tests : IisTestsBase
    {
        private readonly string _testName;

        public AspNetMvc5Tests(bool enableCallTarget, bool classicMode, bool enableRouteTemplateResourceNames)
            : base("AspNetMvc5", @"test\test-applications\aspnet", classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated, "/home/shutdown")
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget);

            if (enableRouteTemplateResourceNames)
            {
                SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");
            }

            _testName = nameof(AspNetMvc5Tests)
                      + (enableCallTarget ? ".CallSite" : ".CallTarget")
                      + (classicMode ? ".Classic" : ".Integrated")
                      + (enableRouteTemplateResourceNames ? ".NoFF" : ".WithFF");
        }

        public static IEnumerable<TestCaseData> Data() => new TestCaseData[]
        {
            new("/DataDog", 200),
            new("/DataDog/DogHouse", 200),
            new("/DataDog/DogHouse/Woof", 200),
            new("/", 200),
            new("/Home", 200),
            new("/Home/Index", 200),
            new("/Home/Get", 500),
            new("/Home/Get/3", 200),
            new("/delay/0", 200),
            new("/delay-async/0", 200),
            new("/delay-optional", 200),
            new("/delay-optional/1", 200),
            new("/badrequest", 500),
            new("/statuscode/201", 201),
            new("/statuscode/503", 503),
        };

        [Property("Category", "EndToEnd")]
        [Property("RunOnWindows", "True")]
        [Property("LoadFromGAC", "True")]
        [TestCaseSource(nameof(Data))]
        public async Task SubmitsTraces(string path, HttpStatusCode statusCode)
        {
            var spans = await GetWebServerSpans(path, Agent, HttpPort, statusCode);

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
