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

        public static TheoryData<string, string, HttpStatusCode, bool, string, string, Dictionary<string, string>> Data =>
            new TheoryData<string, string, HttpStatusCode, bool, string, string, Dictionary<string, string>>
            {
                { "/api/environment", "GET api/environment", HttpStatusCode.OK, false, null, null, EnvironmentTags() },
                { "/api/delay/0", "GET api/delay/{seconds}", HttpStatusCode.OK, false, null, null, DelayTags() },
                { "/api/delay-async/0", "GET api/delay-async/{seconds}", HttpStatusCode.OK, false, null, null, DelayAsyncTags() },
                { "/api/transient-failure/true", "GET api/transient-failure/{value}", HttpStatusCode.OK, false, null, null, TransientFailureTags() },
                { "/api/transient-failure/false", "GET api/transient-failure/{value}", HttpStatusCode.InternalServerError, true, null, null, TransientFailureTags() },
                { "/api/statuscode/201", "GET api/statuscode/{value}", HttpStatusCode.Created, false, null, null, StatusCodeTags() },
                { "/api/statuscode/503", "GET api/statuscode/{value}", HttpStatusCode.ServiceUnavailable, true, null, "The HTTP response has status code 503.", StatusCodeTags() },
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
            string expectedErrorMessage)
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
                "1.0.0");
        }

        private static Dictionary<string, string> EnvironmentTags() =>
            new Dictionary<string, string>
            {
                { Tags.AspNetRoute, "api/environment" },
                { Tags.AspNetController, "api" },
                { Tags.AspNetAction, "environment" },
            };

        private static Dictionary<string, string> TransientFailureTags() =>
            new Dictionary<string, string>
            {
                { Tags.AspNetRoute, "api/transient-failure/{value}" },
                { Tags.AspNetController, "api" },
                { Tags.AspNetAction, "transientfailure" }
            };

        private static Dictionary<string, string> DelayTags() =>
            new Dictionary<string, string>
            {
                { Tags.AspNetRoute, "api/delay/{seconds}" },
                { Tags.AspNetController, "api" },
                { Tags.AspNetAction, "delay" }
            };

        private static Dictionary<string, string> DelayAsyncTags() =>
            new Dictionary<string, string>
            {
                { Tags.AspNetRoute, "api/delay-async/{seconds}" },
                { Tags.AspNetController, "api" },
                { Tags.AspNetAction, "delayasync" }
            };

        private static Dictionary<string, string> StatusCodeTags() =>
            new Dictionary<string, string>
            {
                { Tags.AspNetRoute, "api/statuscode/{value}" },
                { Tags.AspNetController, "api" },
                { Tags.AspNetAction, "statuscode" }
            };
    }
}

#endif
