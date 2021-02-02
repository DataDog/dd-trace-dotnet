#if NET461

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [Collection("IisTests")]
    public class AspNetMvc5Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetMvc5Tests(IisFixture iisFixture, ITestOutputHelper output)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        public static TheoryData<string, string, HttpStatusCode, bool, string, string, Dictionary<string, string>> Data =>
            new TheoryData<string, string, HttpStatusCode, bool, string, string, Dictionary<string, string>>
            {
                { "/DataDog/DogHouse/Woof", "GET /datadog/doghouse/woof", HttpStatusCode.OK, false, null, null, DatadogAreaTags() },
                { "/Home/Index", "GET /home/index", HttpStatusCode.OK, false, null, null, HomeIndexTags() },
                { "/delay/0", "GET /delay/{seconds}", HttpStatusCode.OK, false, null, null, DelayTags() },
                { "/delay-async/0", "GET /delay-async/{seconds}", HttpStatusCode.OK, false, null, null, DelayAsyncTags() },
                { "/badrequest", "GET /badrequest", HttpStatusCode.InternalServerError, true, null, null, BadRequestTags() },
                { "/statuscode/201", "GET /statuscode/{value}", HttpStatusCode.Created, false, null, null, StatusCodeTags() },
                { "/statuscode/503", "GET /statuscode/{value}", HttpStatusCode.ServiceUnavailable, true, null, "The HTTP response has status code 503.", StatusCodeTags() }
            };

        [Theory]
        [Trait("Category", "EndToEnd")]
        [Trait("Integration", nameof(Integrations.AspNetMvcIntegration))]
        [MemberData(nameof(Data))]
        public async Task SubmitsTraces(
            string path,
            string expectedResourceName,
            HttpStatusCode expectedStatusCode,
            bool isError,
            string expectedErrorType,
            string expectedErrorMessage,
            Dictionary<string, string> expectedTags)
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
                "aspnet-mvc.request",
                expectedResourceName,
                "1.0.0",
                expectedTags);
        }

        private static Dictionary<string, string> DatadogAreaTags() =>
            new Dictionary<string, string>
            {
                { Tags.AspNetRoute, "Datadog/{controller}/{action}/{id}" },
                { Tags.AspNetController, "doghouse" },
                { Tags.AspNetAction, "woof" },
                { Tags.AspNetArea, "datadog" }
            };

        private static string DefaultRoute() => "{controller}/{action}/{id}";

        private static Dictionary<string, string> HomeIndexTags() =>
            new Dictionary<string, string>
            {
                { Tags.AspNetRoute, DefaultRoute() },
                { Tags.AspNetController, "home" },
                { Tags.AspNetAction, "index" }
            };

        private static Dictionary<string, string> DelayTags() =>
            new Dictionary<string, string>
            {
                { Tags.AspNetRoute, "delay/{seconds}" },
                { Tags.AspNetController, "home" },
                { Tags.AspNetAction, "delay" }
            };

        private static Dictionary<string, string> BadRequestTags() =>
            new Dictionary<string, string>
            {
                { Tags.AspNetRoute, DefaultRoute() },
                { Tags.AspNetController, "home" },
                { Tags.AspNetAction, "badrequest" }
            };

        private static Dictionary<string, string> DelayAsyncTags() =>
            new Dictionary<string, string>
            {
                { Tags.AspNetRoute, "delay-async/{seconds}" },
                { Tags.AspNetController, "home" },
                { Tags.AspNetAction, "delayasync" }
            };

        private static Dictionary<string, string> StatusCodeTags() =>
            new Dictionary<string, string>
            {
                { Tags.AspNetRoute, DefaultRoute() },
                { Tags.AspNetController, "home" },
                { Tags.AspNetAction, "statuscode" }
            };
    }
}

#endif
