// <copyright file="AspNetMvc5Tests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.ExtensionMethods;
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

        protected override string ExpectedServiceName => "sample/my-app";
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

    [Collection("IisTests")]
    public class AspNetMvc5Tests128BitTraceIds : AspNetMvc5Tests
    {
        public AspNetMvc5Tests128BitTraceIds(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, classicMode: false, enableRouteTemplateResourceNames: true, enable128BitTraceIds: true)
        {
        }
    }

    public abstract class AspNetMvc5TestsInferredProxySpans : AspNetMvc5Tests
    {
        protected AspNetMvc5TestsInferredProxySpans(IisFixture iisFixture, ITestOutputHelper output, bool enableInferredProxySpans)
            : base(
                iisFixture,
                output,
                classicMode: false,
                enableRouteTemplateResourceNames: true,
                enableInferredProxySpans: enableInferredProxySpans,
                testName: enableInferredProxySpans ? $"{nameof(AspNetMvc5Tests)}.InferredProxySpans_Enabled" : $"{nameof(AspNetMvc5Tests)}.InferredProxySpans_Disabled")
        {
        }

        /// <summary>
        /// Override <see cref="CreateHttpRequestMessage"/> to add proxy headers to the request.
        /// </summary>
        protected override HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, string path, DateTimeOffset testStart)
        {
            var request = base.CreateHttpRequestMessage(method, path, testStart);
            var headers = request.Headers;

            headers.Add("x-dd-proxy", "aws-apigateway");
            headers.Add("x-dd-proxy-request-time-ms", testStart.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
            headers.Add("x-dd-proxy-domain-name", "test.api.com");
            headers.Add("x-dd-proxy-httpmethod", "GET");
            headers.Add("x-dd-proxy-path", "/api/test/1");
            headers.Add("x-dd-proxy-stage", "prod");

            return request;
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsInferredProxySpansEnabled : AspNetMvc5TestsInferredProxySpans
    {
        public AspNetMvc5TestsInferredProxySpansEnabled(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableInferredProxySpans: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsInferredProxySpansDisabled : AspNetMvc5TestsInferredProxySpans
    {
        public AspNetMvc5TestsInferredProxySpansDisabled(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableInferredProxySpans: false)
        {
        }
    }

    [UsesVerify]
    public abstract class AspNetMvc5TestsWithBaggage : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;
        private readonly bool _classicMode;
        private readonly bool _enableInferredProxySpans;

        protected AspNetMvc5TestsWithBaggage(IisFixture iisFixture, ITestOutputHelper output)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, true.ToString());
            SetEnvironmentVariable(ConfigurationKeys.ExpandRouteTemplatesEnabled, false.ToString());
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, false.ToString());
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.InferredProxySpansEnabled, false.ToString());

            _classicMode = false;
            _enableInferredProxySpans = false;
            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";

            _testName = nameof(AspNetMvc5Tests)
                      + ".Integrated"  // _classicMode = false
                      + ".WithFF"      // enableRouteTemplateResourceNames = true, enableRouteTemplateExpansion = false
                      + ".WithBaggage";
        }

        public static TheoryData<string, int> Data => new()
        {
            { "/", 200 },
            { "/Home/Index", 200 },
            { "/badrequest", 500 },
        };

        protected virtual string ExpectedServiceName => "sample";

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet.request" => span.IsAspNet(metadataSchemaVersion, excludeTags: new HashSet<string> { "baggage.user.id" }),
                "aspnet-mvc.request" => span.IsAspNetMvc(metadataSchemaVersion, excludeTags: new HashSet<string> { "baggage.user.id" }),
                _ => Result.DefaultSuccess,
            };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(Data))]
        public async Task BaggageInSpanTags(string path, int statusCode)
        {
            // TransferRequest cannot be called in the classic mode, so we expect a 500 when this happens
            var toLowerPath = path.ToLower();
            if (_testName.Contains(".Classic") && toLowerPath.Contains("badrequest") && toLowerPath.Contains("transferrequest"))
            {
                statusCode = 500;
            }

            var expectedSpanCount = _enableInferredProxySpans ? 3 : 2;

            var spans = await GetWebServerSpans(
                path: _iisFixture.VirtualApplicationPath + path, // Append virtual directory to the actual request
                agent: _iisFixture.Agent,
                httpPort: _iisFixture.HttpPort,
                expectedHttpStatusCode: (HttpStatusCode)statusCode,
                expectedSpanCount: expectedSpanCount,
                filterServerSpans: !_enableInferredProxySpans);

            var serverSpans = spans.Where(s => s.Tags.GetValueOrDefault(Tags.SpanKind) == SpanKinds.Server);
            ValidateIntegrationSpans(serverSpans, metadataSchemaVersion: "v0", expectedServiceName: ExpectedServiceName, isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            await Verifier.Verify(spans, settings)
                          .UseMethodName("_withBaggage")
                          .UseTypeName(_testName);
        }

        public async Task InitializeAsync()
        {
            await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        }

        public Task DisposeAsync() => Task.CompletedTask;

        /// <summary>
        /// Override <see cref="CreateHttpRequestMessage"/> to add baggage headers to the request.
        /// </summary>
        protected override HttpRequestMessage CreateHttpRequestMessage(HttpMethod method, string path, DateTimeOffset testStart)
        {
            var request = base.CreateHttpRequestMessage(method, path, testStart);
            request.Headers.Add("baggage", "user.id=doggo");
            return request;
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc5TestsWithBaggageEnabled : AspNetMvc5TestsWithBaggage
    {
        public AspNetMvc5TestsWithBaggageEnabled(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output)
        {
        }
    }

    [UsesVerify]
    public abstract class AspNetMvc5Tests : TracingIntegrationTest, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;
        private readonly bool _classicMode;
        private readonly bool _enableInferredProxySpans;

        protected AspNetMvc5Tests(
            IisFixture iisFixture,
            ITestOutputHelper output,
            bool classicMode,
            bool enableRouteTemplateResourceNames,
            string testName = null,
            bool enableRouteTemplateExpansion = false,
            bool virtualApp = false,
            bool enable128BitTraceIds = false,
            bool enableInferredProxySpans = false)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());
            SetEnvironmentVariable(ConfigurationKeys.ExpandRouteTemplatesEnabled, enableRouteTemplateExpansion.ToString());
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.TraceId128BitGenerationEnabled, enable128BitTraceIds.ToString());
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.InferredProxySpansEnabled, enableInferredProxySpans.ToString());

            _classicMode = classicMode;
            _enableInferredProxySpans = enableInferredProxySpans;
            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            if (virtualApp)
            {
                _iisFixture.VirtualApplicationPath = "/my-app";
            }

            _testName = testName ??
                        nameof(AspNetMvc5Tests)
                      + (virtualApp ? ".VirtualApp" : string.Empty)
                      + (classicMode ? ".Classic" : ".Integrated")
                      + (enableRouteTemplateExpansion     ? ".WithExpansion" :
                         enableRouteTemplateResourceNames ? ".WithFF" : ".NoFF")
                      + (enable128BitTraceIds ? ".128bit" : string.Empty);
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

        protected virtual string ExpectedServiceName => "sample";

        public override Result ValidateIntegrationSpan(MockSpan span, string metadataSchemaVersion) =>
            span.Name switch
            {
                "aspnet.request" => span.IsAspNet(metadataSchemaVersion),
                "aspnet-mvc.request" => span.IsAspNetMvc(metadataSchemaVersion),
                _ => Result.DefaultSuccess,
            };

        [SkippableTheory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(string path, int statusCode)
        {
            // TransferRequest cannot be called in the classic mode, so we expect a 500 when this happens
            var toLowerPath = path.ToLower();
            if (_testName.Contains(".Classic") && toLowerPath.Contains("badrequest") && toLowerPath.Contains("transferrequest"))
            {
                statusCode = 500;
            }

            var expectedSpanCount = _enableInferredProxySpans ? 3 : 2;

            var spans = await GetWebServerSpans(
                path: _iisFixture.VirtualApplicationPath + path, // Append virtual directory to the actual request
                agent: _iisFixture.Agent,
                httpPort: _iisFixture.HttpPort,
                expectedHttpStatusCode: (HttpStatusCode)statusCode,
                expectedSpanCount: expectedSpanCount,
                filterServerSpans: !_enableInferredProxySpans);

            // ValidateIntegrationSpans() expects only server spans, but we want all spans in the snapshot (e.g. inferred proxy spans)
            var serverSpans = spans.Where(s => s.Tags.GetValueOrDefault(Tags.SpanKind) == SpanKinds.Server);
            // Exclude inferred proxy spans (aws.apigateway, azure.apim) - they have Service from x-dd-proxy-domain-name (e.g. test.api.com)
            var spansToValidate = _enableInferredProxySpans
                ? serverSpans.Where(s => s.Name != "aws.apigateway" && s.Name != "azure.apim")
                : serverSpans;
            ValidateIntegrationSpans(spansToValidate, metadataSchemaVersion: "v0", expectedServiceName: ExpectedServiceName, isExternalSpan: false);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);
            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .UseTypeName(_testName);
        }

        public async Task InitializeAsync()
        {
            await _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);
        }

        public Task DisposeAsync() => Task.CompletedTask;
    }

    [UsesVerify]
    public abstract class AspNetMvc5ModuleOnlyTests : TestHelper, IClassFixture<IisFixture>, IAsyncLifetime
    {
        private readonly IisFixture _iisFixture;
        private readonly string _testName;
        private readonly bool _classicMode;

        protected AspNetMvc5ModuleOnlyTests(IisFixture iisFixture, ITestOutputHelper output, bool classicMode, bool enableRouteTemplateResourceNames, bool virtualApp = false)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, enableRouteTemplateResourceNames.ToString());

            // Disable the MVC part, so we can't back propagate any details to the tracing module
            SetEnvironmentVariable(ConfigurationKeys.DisabledIntegrations, nameof(Configuration.IntegrationId.AspNetMvc));

            _classicMode = classicMode;
            _iisFixture = iisFixture;
            _iisFixture.ShutdownPath = "/home/shutdown";
            if (virtualApp)
            {
                _iisFixture.VirtualApplicationPath = "/my-app";
            }

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
        public async Task SubmitsTraces(string path, int statusCode)
        {
            // TransferRequest cannot be called in the classic mode, so we expect a 500 when this happens
            if (_testName.Contains(".Classic") && path.ToLowerInvariant().Contains("transferrequest"))
            {
                statusCode = 500;
            }

            // Append virtual directory if there is one
            var spans = await GetWebServerSpans(_iisFixture.VirtualApplicationPath + path, _iisFixture.Agent, _iisFixture.HttpPort, (HttpStatusCode)statusCode, expectedSpanCount: 1);

            var sanitisedPath = VerifyHelper.SanitisePathsForVerify(path);

            var settings = VerifyHelper.GetSpanVerifierSettings(sanitisedPath, (int)statusCode);

            // Overriding the type name here as we have multiple test classes in the file
            // Ensures that we get nice file nesting in Solution Explorer
            await Verifier.Verify(spans, settings)
                          .UseMethodName("_")
                          .DisableRequireUniquePrefix()
                          .UseTypeName(_testName);
        }

        public Task InitializeAsync() => _iisFixture.TryStartIis(this, _classicMode ? IisAppType.AspNetClassic : IisAppType.AspNetIntegrated);

        public Task DisposeAsync() => Task.CompletedTask;
    }
}
#endif
