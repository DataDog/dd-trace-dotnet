#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallsite : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallsite(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, enableInlining: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTargetNoInlining : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTargetNoInlining(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetWebApi2TestsCallTarget : AspNetWebApi2Tests
    {
        public AspNetWebApi2TestsCallTarget(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: true)
        {
        }
    }

    public abstract class AspNetWebApi2Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetWebApi2Tests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool enableInlining)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget, enableInlining);

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        public static TheoryData<string, string, int, bool, string, string, SerializableDictionary> Data => new()
        {
            { "/api/environment", "GET api/environment", 200, false, null, null, EnvironmentTags() },
            { "/api/absolute-route", "GET api/absolute-route", 200, false, null, null, AbsoluteRouteTags() },
            { "/api/delay/0", "GET api/delay/{seconds}", 200, false, null, null, DelayTags() },
            { "/api/delay-optional", "GET api/delay-optional/{seconds}", 200, false, null, null, DelayOptionalTags() },
            { "/api/delay-optional/1", "GET api/delay-optional/{seconds}", 200, false, null, null, DelayOptionalTags() },
            { "/api/delay-async/0", "GET api/delay-async/{seconds}", 200, false, null, null, DelayAsyncTags() },
            { "/api/transient-failure/true", "GET api/transient-failure/{value}", 200, false, null, null, TransientFailureTags() },
            { "/api/transient-failure/false", "GET api/transient-failure/{value}", 500, true, "System.ArgumentException", "Passed in value was not 'true': false", TransientFailureTags() },
            { "/api/statuscode/201", "GET api/statuscode/{value}", 201, false, null, null, StatusCodeTags() },
            { "/api/statuscode/503", "GET api/statuscode/{value}", 503, true, null, "The HTTP response has status code 503.", StatusCodeTags() },
        };

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetWebApi2Integration))]
        [MemberData(nameof(Data))]
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
