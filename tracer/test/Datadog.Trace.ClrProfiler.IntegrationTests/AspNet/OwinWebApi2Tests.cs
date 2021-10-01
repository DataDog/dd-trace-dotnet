// <copyright file="OwinWebApi2Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.ClrProfiler.IntegrationTests.AspNet;
using Datadog.Trace.Configuration;
using NUnit.Framework;
using VerifyNUnit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class OwinWebApi2TestsCallsite : OwinWebApi2Tests
    {
        public OwinWebApi2TestsCallsite()
            : base(enableCallTarget: false, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class OwinWebApi2TestsCallTarget : OwinWebApi2Tests
    {
        public OwinWebApi2TestsCallTarget()
            : base(enableCallTarget: true, enableRouteTemplateResourceNames: false)
        {
        }
    }

    public class OwinWebApi2TestsCallsiteWithFeatureFlag : OwinWebApi2Tests
    {
        public OwinWebApi2TestsCallsiteWithFeatureFlag()
            : base(enableCallTarget: false, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public class OwinWebApi2TestsCallTargetWithFeatureFlag : OwinWebApi2Tests
    {
        public OwinWebApi2TestsCallTargetWithFeatureFlag()
            : base(enableCallTarget: true, enableRouteTemplateResourceNames: true)
        {
        }
    }

    public abstract class OwinWebApi2Tests : OwinTestsBase
    {
        private readonly string _testName;

        public OwinWebApi2Tests(bool enableCallTarget, bool enableRouteTemplateResourceNames)
            : base("Owin.WebApi2")
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget);
            if (enableRouteTemplateResourceNames)
            {
                SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");
            }

            _testName = nameof(OwinWebApi2Tests)
                      + (enableCallTarget ? ".CallSite" : ".CallTarget")
                      + (enableRouteTemplateResourceNames ? ".NoFF" : ".WithFF");
        }

        public static IEnumerable<TestCaseData> Data() => new TestCaseData[]
        {
            new("/api/environment", 200, 1),
            new("/api/absolute-route", 200, 1),
            new("/api/delay/0", 200, 1),
            new("/api/delay-optional", 200, 1),
            new("/api/delay-optional/1", 200, 1),
            new("/api/delay-async/0", 200, 1),
            new("/api/transient-failure/true", 200, 1),
            new("/api/transient-failure/false", 500, 2),
            new("/api/statuscode/201", 201, 1),
            new("/api/statuscode/503", 503, 1),
            new("/api2/delay/0", 200, 1),
            new("/api2/optional", 200, 1),
            new("/api2/optional/1", 200, 1),
            new("/api2/delayAsync/0", 200, 1),
            new("/api2/transientfailure/true", 200, 1),
            new("/api2/transientfailure/false", 500, 2),
            new("/api2/statuscode/201", 201, 1),
            new("/api2/statuscode/503", 503, 2),

            // The global message handler will fail when ps=false
            // The per-route message handler is not invoked with the route /api2, so ts=true|false has no effect
            new("/api2/statuscode/201?ps=true&ts=true", 201, 1),
            new("/api2/statuscode/201?ps=true&ts=false", 201, 1),
            new("/api2/statuscode/201?ps=false&ts=true", 500, 1),
            new("/api2/statuscode/201?ps=false&ts=false", 500, 1),

            // The global message handler will fail when ps=false
            // The global and per-route message handler is invoked with the route /handler-api, so ts=false will also fail the request
            new("/handler-api/api?ps=true&ts=true", 200, 0),
            new("/handler-api/api?ps=true&ts=false", 500, 1),
            new("/handler-api/api?ps=false&ts=true", 500, 1),
            new("/handler-api/api?ps=false&ts=false", 500, 1),
        };

        [Property("Category", "EndToEnd")]
        [Property("RunOnWindows", "True")]
        [TestCaseSource(nameof(Data))]
        public async Task SubmitsTraces(string path, HttpStatusCode statusCode, int expectedSpanCount)
        {
            var spans = await WaitForSpans(path, expectedSpanCount);

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
