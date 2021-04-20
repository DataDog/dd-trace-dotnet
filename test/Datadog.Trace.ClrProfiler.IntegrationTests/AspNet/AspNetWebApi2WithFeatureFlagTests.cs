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
    public class AspNetWebApi2WithFeatureFlagCallsite : AspNetWebApi2WithFeatureFlagTests
    {
        public AspNetWebApi2WithFeatureFlagCallsite(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, enableInlining: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2WithFeatureFlagCallTargetNoInlining : AspNetWebApi2WithFeatureFlagTests
    {
        public AspNetWebApi2WithFeatureFlagCallTargetNoInlining(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2WithFeatureFlagCallTarget : AspNetWebApi2WithFeatureFlagTests
    {
        public AspNetWebApi2WithFeatureFlagCallTarget(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: true)
        {
        }
    }

    public abstract class AspNetWebApi2WithFeatureFlagTests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetWebApi2WithFeatureFlagTests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool enableInlining)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget, enableInlining);
            SetEnvironmentVariable(ConfigurationKeys.FeatureFlags.RouteTemplateResourceNamesEnabled, "true");

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
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
                expectedErrorType,
                expectedErrorMessage,
                "web",
                "aspnet-webapi.request",
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
