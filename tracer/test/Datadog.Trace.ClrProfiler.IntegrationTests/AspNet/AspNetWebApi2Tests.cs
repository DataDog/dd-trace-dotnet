// <copyright file="AspNetWebApi2Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using VerifyXunit;
using Xunit;
using Xunit.Abstractions;
using Expectations = System.Collections.Generic.Dictionary<string, (int StatusCode, int ExpectedSpanCount)>;

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

        [Collection("IisTests")]
        public class AspNetWebApi2TestsVirtualAppIntegratedWithFeatureFlag : AspNetWebApi2Tests
        {
            public AspNetWebApi2TestsVirtualAppIntegratedWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
                : base(iisFixture, output, virtualApp: true, classicMode: false, enableRouteTemplateResourceNames: true)
            {
            }

            protected override string ExpectedServiceName => "sample/my-app";
        }

        [Collection("IisTests")]
        public class AspNetWebApi2TestsModuleOnlyClassic : AspNetWebApi2ModuleOnlyTests
        {
            public AspNetWebApi2TestsModuleOnlyClassic(IisFixture iisFixture, ITestOutputHelper output)
                : base(iisFixture, output, virtualApp: false, classicMode: true, enableRouteTemplateResourceNames: false)
            {
            }
        }

        [Collection("IisTests")]
        public class AspNetWebApi2TestsModuleOnlyIntegrated : AspNetWebApi2ModuleOnlyTests
        {
            public AspNetWebApi2TestsModuleOnlyIntegrated(IisFixture iisFixture, ITestOutputHelper output)
                : base(iisFixture, output, virtualApp: false, classicMode: false, enableRouteTemplateResourceNames: false)
            {
            }
        }

        [Collection("IisTests")]
        public class AspNetWebApi2TestsModuleOnlyVirtualAppIntegrated : AspNetWebApi2ModuleOnlyTests
        {
            public AspNetWebApi2TestsModuleOnlyVirtualAppIntegrated(IisFixture iisFixture, ITestOutputHelper output)
                : base(iisFixture, output, virtualApp: true, classicMode: false, enableRouteTemplateResourceNames: false)
            {
            }
        }

        [Collection("IisTests")]
        public class AspNetWebApi2TestsModuleOnlyVirtualAppIntegratedWithFeatureFlag : AspNetWebApi2ModuleOnlyTests
        {
            public AspNetWebApi2TestsModuleOnlyVirtualAppIntegratedWithFeatureFlag(IisFixture iisFixture, ITestOutputHelper output)
                : base(iisFixture, output, virtualApp: true, classicMode: false, enableRouteTemplateResourceNames: true)
            {
            }
        }
    }

    [UsesVerify]
    public abstract class AspNetWebApi2Tests : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private static readonly Expectations ClassicExpectations = CreateExpectations(classicMode: true);
        private static readonly Expectations IntegratedExpectations = CreateExpectations(classicMode: false);

        private readonly IisFixture _iisFixture;
        private readonly string _testName;
        private readonly Expectations _expectations;
        private readonly bool _classicMode;

        public AspNetWebApi2Tests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableRouteTemplateResourceNames, bool enableRouteTemplateExpansion = false, bool virtualApp = false)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());
            SetEnvironmentVariable(ConfigurationKeys.ExpandRouteTemplatesEnabled, enableRouteTemplateExpansion.ToString());

            _expectations = classicMode ? ClassicExpectations : IntegratedExpectations;
            _classicMode = classicMode;
            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            if (virtualApp)
            {
                _iisFixture.VirtualApplicationPath = "/my-app";
            }

            _testName = nameof(AspNetWebApi2Tests)
                      + (virtualApp ? ".VirtualApp" : string.Empty)
                      + (classicMode ? ".Classic" : ".Integrated")
                      + (enableRouteTemplateExpansion ? ".WithExpansion" :
                        (enableRouteTemplateResourceNames ? ".WithFF" : ".NoFF"));
        }

        protected virtual string ExpectedServiceName => "sample";

        // classicMode doesn't change the paths we test, just the expectations, so can use either
        public static IEnumerable<object[]> Data() => IntegratedExpectations.Keys.Select(x => new object[] { x });

        public static Dictionary<string, (int StatusCode, int ExpectedSpanCount)> CreateExpectations(bool classicMode) => new()
        {
            { "/api/environment", (200, 2) },
            { "/api/absolute-route", (200, 2) },
            { "/api/delay/0", (200, 2) },
            { "/api/delay-optional", (200, 2) },
            { "/api/delay-optional/1", (200, 2) },
            { "/api/delay-async/0", (200, 2) },
            { "/api/transient-failure/true", (200, 2) },
            { "/api/transient-failure/false", (500, 3) },
            { "/api/statuscode/201", (201, 2) },
            { "/api/statuscode/503", (503, 2) },
            { "/api/constraints", (200, 2) },
            { "/api/constraints/201", (201, 2) },
            { "/api/TransferRequest/401", (401, classicMode ? 2 : 4) },
            { "/api/TransferRequest/503", (503, classicMode ? 2 : 4) },
            { "/api2/delay/0", (200, 2) },
            { "/api2/optional", (200, 2) },
            { "/api2/optional/1", (200, 2) },
            { "/api2/delayAsync/0", (200, 2) },
            { "/api2/transientfailure/true", (200, 2) },
            { "/api2/transientfailure/false", (500, 3) },
            { "/api2/statuscode/201", (201, 2) },
            { "/api2/statuscode/503", (503, 2) },

            // The global message handler will fail when ps=false
            // The per-route message handler is not invoked with the route /api2, so ts=true|false has no effect
            { "/api2/statuscode/201?ps=true&ts=true", (201, 2) },
            { "/api2/statuscode/201?ps=true&ts=false", (201, 2) },
            { "/api2/statuscode/201?ps=false&ts=true", (500, 2) },
            { "/api2/statuscode/201?ps=false&ts=false", (500, 2) },

            // The global message handler will fail when ps=false
            // The global and per-route message handler is invoked with the route /handler-api, so ts=false will also fail the request
            { "/handler-api/api?ps=true&ts=true", (200, 1) },
            { "/handler-api/api?ps=true&ts=false", (500, 2) },
            { "/handler-api/api?ps=false&ts=true", (500, 2) },
            { "/handler-api/api?ps=false&ts=false", (500, 2) },
        };

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet.request" => span.IsAspNet(metadataSchemaVersion),
                "aspnet-mvc.request" => span.IsAspNetMvc(metadataSchemaVersion),
                "aspnet-webapi.request" => span.IsAspNetWebApi2(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(string path)
        {
            var (statusCode, expectedSpanCount) = _expectations[path];

            // TransferRequest cannot be called in the classic mode, so we expect a 500 when this happens
            var toLowerPath = path.ToLower();
            if (_testName.Contains(".Classic") && toLowerPath.Contains("transferrequest"))
            {
                statusCode = 500;
            }

            // Append virtual directory to the actual request
            var spans = await GetWebServerSpans(_iisFixture.VirtualApplicationPath + path, _iisFixture.Agent, _iisFixture.HttpPort, (HttpStatusCode)statusCode, expectedSpanCount);
            ValidateIntegrationSpans(spans, metadataSchemaVersion: "v0", expectedServiceName: ExpectedServiceName, isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Overriding the method name to _
            // Overriding the parameters to remove the expectedSpanCount parameter, which is necessary for operation but unnecessary for the filename
            await Verifier.Verify(spans, settings)
                          .UseFileName($"{_testName}.__path={sanitisedPath}_statusCode={statusCode}")
                          .DisableRequireUniquePrefix(); // sharing snapshots between web api and owin
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;
    }

    [UsesVerify]
    public abstract class AspNetWebApi2ModuleOnlyTests : TestHelper, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;
        private readonly bool _classicMode;

        public AspNetWebApi2ModuleOnlyTests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableRouteTemplateResourceNames, bool virtualApp = false)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());

            // Disable the WebApi2 part, so we can't back propagate any details to the tracing module
            SetEnvironmentVariable(ConfigurationKeys.DisabledIntegrations, nameof(Configuration.IntegrationId.AspNetWebApi2));

            _classicMode = classicMode;
            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            if (virtualApp)
            {
                _iisFixture.VirtualApplicationPath = "/my-app";
            }

            _testName = nameof(AspNetWebApi2Tests)
                      + "ModuleOnly"
                      + (virtualApp ? ".VirtualApp" : string.Empty);
        }

        public static TheoryData<string, int, int> Data() => new()
        {
            { "/api/absolute-route", 200, 1 },
            { "/api/transient-failure/true", 200, 1 },
            { "/api/transient-failure/false", 500, 2 },
            { "/api/statuscode/503", 503, 1 },
            { "/api2/transientfailure/false", 500, 2 },
            { "/api2/statuscode/201", 201, 1 },
            { "/api2/statuscode/503", 503, 1 },

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
            // Append virtual directory to the actual request
            var spans = await GetWebServerSpans(_iisFixture.VirtualApplicationPath + path, _iisFixture.Agent, _iisFixture.HttpPort, statusCode, expectedSpanCount);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Overriding the method name to _
            // Overriding the parameters to remove the expectedSpanCount parameter, which is necessary for operation but unnecessary for the filename
            await Verifier.Verify(spans, settings)
                          .DisableRequireUniquePrefix()
                          .UseFileName($"{_testName}.__path={sanitisedPath}_statusCode={(int)statusCode}");
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
#endif
