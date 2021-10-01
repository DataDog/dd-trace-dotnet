// <copyright file="AspNetMvc4Tests.cs" company="Datadog">
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
    public class AspNetMvc4TestsCallsiteClassic : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallsiteClassic()
            : base(enableCallTarget: false, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetMvc4TestsCallsiteIntegrated : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallsiteIntegrated()
            : base(enableCallTarget: false, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetMvc4TestsCallsiteClassicWithFeatureFlag : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallsiteClassicWithFeatureFlag()
            : base(enableCallTarget: false, classicMode: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public class AspNetMvc4TestsCallsiteIntegratedWithFeatureFlag : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallsiteIntegratedWithFeatureFlag()
            : base(enableCallTarget: false, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public class AspNetMvc4TestsCallTargetClassic : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetClassic()
            : base(enableCallTarget: true, classicMode: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetMvc4TestsCallTargetIntegrated : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetIntegrated()
            : base(enableCallTarget: true, classicMode: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class AspNetMvc4TestsCallTargetClassicWithFeatureFlag : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetClassicWithFeatureFlag()
            : base(enableCallTarget: true, classicMode: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public class AspNetMvc4TestsCallTargetIntegratedWithFeatureFlag : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetIntegratedWithFeatureFlag()
            : base(enableCallTarget: true, classicMode: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public abstract class AspNetMvc4Tests : IisTestsBase
    {
        private readonly string _testName;

        protected AspNetMvc4Tests(bool enableCallTarget, bool classicMode, bool enableRouteTemplateResourceNames)
            : base("AspNetMvc4", @"test\test-applications\aspnet", classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated, "/home/shutdown")
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget);
            if (enableRouteTemplateResourceNames)
            {
                SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");
            }

            _testName = nameof(AspNetMvc4Tests)
                      + (enableCallTarget ? ".CallSite" : ".CallTarget")
                      + (classicMode ? ".Classic" : ".Integrated")
                      + (enableRouteTemplateResourceNames ? ".NoFF" : ".WithFF");
        }

        public static IEnumerable<TestCaseData> Data() => new TestCaseData[]
        {
            new("/Admin", 200),
            new("/Admin/Home", 200),
            new("/Admin/Home/Index", 200),
            new("/", 200),
            new("/Home", 200),
            new("/Home/Index", 200),
            new("/Home/BadRequest", 500),
            new("/Home/identifier", 500),
            new("/Home/identifier/123", 200),
            new("/Home/identifier/BadValue", 500),
            new("/Home/OptionalIdentifier", 200),
            new("/Home/OptionalIdentifier/123", 200),
            new("/Home/OptionalIdentifier/BadValue", 200),
            new("/Home/StatusCode?value=201", 201),
            new("/Home/StatusCode?value=503", 503),
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
