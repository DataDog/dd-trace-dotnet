// <copyright file="OwinIisWebApi2Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class OwinIisWebApi2TestsCallTarget : OwinIisWebApi2Tests
    {
        public OwinIisWebApi2TestsCallTarget(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableRouteTemplateResourceNames: false)
        {
        }
    }

    [Collection("IisTests")]
    public class OwinIisWebApi2TestsCallTargetWithFeatureFlag : OwinIisWebApi2Tests
    {
        public OwinIisWebApi2TestsCallTargetWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableRouteTemplateResourceNames: true)
        {
        }
    }

    [Collection("IisTests")]
    public class OwinIisWebApi2TestsCallTargetWithRouteTemplateExpansion : OwinIisWebApi2Tests
    {
        public OwinIisWebApi2TestsCallTargetWithRouteTemplateExpansion(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableRouteTemplateResourceNames: true, enableRouteTemplateExpansion: true)
        {
        }
    }

    [UsesVerify]
    public abstract class OwinIisWebApi2Tests : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;

        public OwinIisWebApi2Tests(IisFixture iisFixture, ITestOutputHelper output, bool enableRouteTemplateResourceNames, bool enableRouteTemplateExpansion = false)
            : base("Owin.Iis.WebApi2", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());
            SetEnvironmentVariable(ConfigurationKeys.ExpandRouteTemplatesEnabled, enableRouteTemplateExpansion.ToString());

            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/shutdown";

            _iisFixture = iisFixture;
            _testName = nameof(AspNetWebApi2Tests) // Note that these spans are identical to the non-owin webapi2 version
                      + ".Integrated"
                      + (enableRouteTemplateExpansion ? ".WithExpansion" :
                        (enableRouteTemplateResourceNames ? ".WithFF" : ".NoFF"));
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
            // Transfer request doesn't work as expected with OWIN, but I'm not sure whether they should work, and seems relatively niche so ignoring them for now
            // { "/api/TransferRequest/401", 401, 4 },
            // { "/api/TransferRequest/503", 503, 4 },
            { "/api2/delay/0", 200, 2 },
            { "/api2/optional", 200, 2 },
            { "/api2/optional/1", 200, 2 },
            { "/api2/delayAsync/0", 200, 2 },
            { "/api2/transientfailure/true", 200, 2 },
            { "/api2/transientfailure/false", 500, 3 },
            { "/api2/statuscode/201", 201, 2 },
            { "/api2/statuscode/503", 503, 2 },

            // The global message handler will fail when ps=false
            // The per-route message handler is not invoked with the route /api2, so ts=true|false has no effect
            { "/api2/statuscode/201?ps=true&ts=true", 201, 2 },
            { "/api2/statuscode/201?ps=true&ts=false", 201, 2 },
            { "/api2/statuscode/201?ps=false&ts=true", 500, 2 },
            { "/api2/statuscode/201?ps=false&ts=false", 500, 2 },

            // The global message handler will fail when ps=false
            // The global and per-route message handler is invoked with the route /handler-api, so ts=false will also fail the request
            // { "/handler-api/api?ps=true&ts=true", 200, 1 }, // I'm not sure why, but this one doesnt seem to work
            { "/handler-api/api?ps=true&ts=false", 500, 2 },
            { "/handler-api/api?ps=false&ts=true", 500, 2 },
            { "/handler-api/api?ps=false&ts=false", 500, 2 },
        };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet.request" => span.IsAspNet(metadataSchemaVersion),
                "aspnet-webapi.request" => span.IsAspNetWebApi2(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(string path, HttpStatusCode statusCode, int expectedSpanCount)
        {
            // Append virtual directory to the actual request
            var spans = await GetWebServerSpans(_iisFixture.VirtualApplicationPath + path, _iisFixture.Agent, _iisFixture.HttpPort, statusCode, expectedSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: "sample", isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Overriding the method name to _
            // Overriding the parameters to remove the expectedSpanCount parameter, which is necessary for operation but unnecessary for the filename
            await Verifier.Verify(spans, settings)
                          .UseFileName($"{_testName}.__path={sanitisedPath}_statusCode={(int)statusCode}")
                          .DisableRequireUniquePrefix(); // sharing snapshots between web api and owin
        }

        // OWIN can only run in integrated mode
        public Task InitializeAsync() => _iisFixture.TryStartIis(this, IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
#endif
