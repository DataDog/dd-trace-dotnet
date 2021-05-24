// <copyright file="AspNetWebApi2WithFeatureFlagTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Net;
using System.Threading.Tasks;
using Datadog.Trace.Configuration;
using Datadog.Trace.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetWebApi2WithFeatureFlagCallsiteClassic : AspNetWebApi2WithFeatureFlagTests
    {
        public AspNetWebApi2WithFeatureFlagCallsiteClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2WithFeatureFlagCallsiteIntegrated : AspNetWebApi2WithFeatureFlagTests
    {
        public AspNetWebApi2WithFeatureFlagCallsiteIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, classicMode: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2WithFeatureFlagCallTargetClassic : AspNetWebApi2WithFeatureFlagTests
    {
        public AspNetWebApi2WithFeatureFlagCallTargetClassic(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: true)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2WithFeatureFlagCallTargetIntegrated : AspNetWebApi2WithFeatureFlagTests
    {
        public AspNetWebApi2WithFeatureFlagCallTargetIntegrated(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, classicMode: false)
        {
        }
    }

    public abstract class AspNetWebApi2WithFeatureFlagTests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetWebApi2WithFeatureFlagTests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool classicMode)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget);
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this, classicMode);
        }

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("RunOnWindows", "True")]
        [Trait("LoadFromGAC", "True")]
        [MemberData(nameof(AspNetWebApi2TestData.WithFeatureFlag), MemberType = typeof(AspNetWebApi2TestData))]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName,
            HttpStatusCode expectedStatusCode,
            bool isError,
            string expectedErrorType,
            string expectedErrorMessage,
            SerializableDictionary expectedTags)
        {
            await AssertWebServerSpan(
                path,
                _iisFixture.Agent,
                _iisFixture.HttpPort,
                expectedStatusCode,
                isError,
                expectedAspNetErrorType: null,
                expectedAspNetErrorMessage: isError ? $"The HTTP response has status code {(int)expectedStatusCode}." : null,
                expectedErrorType: expectedErrorType,
                expectedErrorMessage: expectedErrorMessage,
                "web",
                "aspnet-webapi.request",
                expectedResourceName,
                expectedResourceName,
                "1.0.0",
                expectedTags);
        }

        private static SerializableDictionary EnvironmentTags() => new()
        {
            { Tags.AspNetRoute, "api/environment" },
        };

        private static SerializableDictionary AbsoluteRouteTags() => new()
        {
            { Tags.AspNetRoute, "api/absolute-route" },
        };

        private static SerializableDictionary TransientFailureTags() => new()
        {
            { Tags.AspNetRoute, "api/transient-failure/{value}" },
        };

        private static SerializableDictionary DelayHomeTags() => new()
        {
            { Tags.AspNetRoute, "delay/{seconds}" },
        };

        private static SerializableDictionary DelayTags() => new()
        {
            { Tags.AspNetRoute, "api/delay/{seconds}" },
        };

        private static SerializableDictionary DelayOptionalTags() => new()
        {
            { Tags.AspNetRoute, "api/delay-optional/{seconds}" },
        };

        private static SerializableDictionary DelayAsyncTags() => new()
        {
            { Tags.AspNetRoute, "api/delay-async/{seconds}" },
        };

        private static SerializableDictionary StatusCodeTags() => new()
        {
            { Tags.AspNetRoute, "api/statuscode/{value}" },
        };
    }
}
#endif
