// <copyright file="OwinWebApi2Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Collections.Generic;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class OwinWebApi2TestsCallTarget : OwinWebApi2Tests
    {
        public OwinWebApi2TestsCallTarget(OwinFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class OwinWebApi2TestsCallTargetWithFeatureFlag : OwinWebApi2Tests
    {
        public OwinWebApi2TestsCallTargetWithFeatureFlag(OwinFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class OwinWebApi2TestsCallTargetWithRouteTemplateExpansion : OwinWebApi2Tests
    {
        public OwinWebApi2TestsCallTargetWithRouteTemplateExpansion(OwinFixture fixture, ITestOutputHelper output)
            : base(fixture, output, enableRouteTemplateResourceNames: true, enableRouteTemplateExpansion: true)
        {
        }
    }

    [UsesVerify]
    public abstract class OwinWebApi2Tests : TracingIntegrationTest, IClassFixture<OwinFixture>
    {
        private readonly OwinFixture _fixture;
        private readonly string _testName;
        private readonly ITestOutputHelper _output;

        public OwinWebApi2Tests(OwinFixture fixture, ITestOutputHelper output, bool enableRouteTemplateResourceNames, bool enableRouteTemplateExpansion = false)
            : base("Owin.WebApi2", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());
            SetEnvironmentVariable(ConfigurationKeys.ExpandRouteTemplatesEnabled, enableRouteTemplateExpansion.ToString());

            _fixture = fixture;
            _output = output;
            _testName = nameof(OwinWebApi2Tests)
                      + (enableRouteTemplateExpansion ? ".WithExpansion" :
                        (enableRouteTemplateResourceNames ? ".WithFF" : ".NoFF"));
        }

        public static TheoryData<string, int, int> Data() => new()
        {
            { "/api/environment", 200, 1 },
            { "/api/absolute-route", 200, 1 },
            { "/api/delay/0", 200, 1 },
            { "/api/delay-optional", 200, 1 },
            { "/api/delay-optional/1", 200, 1 },
            { "/api/delay-async/0", 200, 1 },
            { "/api/transient-failure/true", 200, 1 },
            { "/api/transient-failure/false", 500, 2 },
            { "/api/statuscode/201", 201, 1 },
            { "/api/statuscode/503", 503, 1 },
            { "/api/constraints", 200, 1 },
            { "/api/constraints/201", 201, 1 },
            { "/api2/delay/0", 200, 1 },
            { "/api2/optional", 200, 1 },
            { "/api2/optional/1", 200, 1 },
            { "/api2/delayAsync/0", 200, 1 },
            { "/api2/transientfailure/true", 200, 1 },
            { "/api2/transientfailure/false", 500, 2 },
            { "/api2/statuscode/201", 201, 1 },
            { "/api2/statuscode/503", 503, 1 },

            // The global message handler will fail when ps=false
            // The per-route message handler is not invoked with the route /api2, so ts=true|false has no effect
            { "/api2/statuscode/201?ps=true&ts=true", 201, 1 },
            { "/api2/statuscode/201?ps=true&ts=false", 201, 1 },
            { "/api2/statuscode/201?ps=false&ts=true", 500, 1 },
            { "/api2/statuscode/201?ps=false&ts=false", 500, 1 },

            // The global message handler will fail when ps=false
            // The global and per-route message handler is invoked with the route /handler-api, so ts=false will also fail the request
            { "/handler-api/api?ps=true&ts=true", 200, 0 },
            { "/handler-api/api?ps=true&ts=false", 500, 1 },
            { "/handler-api/api?ps=false&ts=true", 500, 1 },
            { "/handler-api/api?ps=false&ts=false", 500, 1 },
        };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet-webapi.request" => span.IsAspNetWebApi2(metadataSchemaVersion, excludeTags: new HashSet<string> { "baggage.user.id" }),
                _ => Result.DefaultSuccess,
            };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(string path, int statusCode, int expectedSpanCount)
        {
            await _fixture.TryStartApp(this, _output);

            var spans = await _fixture.WaitForSpans(Output, path, expectedSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: "Samples.Owin.WebApi2", isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Overriding the method name to _
            // Overriding the parameters to remove the expectedSpanCount parameter, which is necessary for operation but unnecessary for the filename
            await Verifier.Verify(spans, settings)
                          .UseFileName($"{_testName}.__path={sanitisedPath}_statusCode={statusCode}");
        }
    }
}
#endif
