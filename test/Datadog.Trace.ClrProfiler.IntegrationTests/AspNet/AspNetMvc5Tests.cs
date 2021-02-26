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
    [Collection(nameof(IisFixture))]
    public class AspNetMvc5TestsCallsite : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallsite(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, enableInlining: false)
        {
        }
    }

    [Collection(nameof(IisFixture))]
    public class AspNetMvc5TestsCallTargetNoInlining : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTargetNoInlining(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: false)
        {
        }
    }

    [Collection(nameof(IisFixture))]
    public class AspNetMvc5TestsCallTarget : AspNetMvc5Tests
    {
        public AspNetMvc5TestsCallTarget(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: true)
        {
        }
    }

    public abstract class AspNetMvc5Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetMvc5Tests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool enableInlining)
            : base("AspNetMvc5", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget, enableInlining);

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        public static TheoryData<string, string, HttpStatusCode, bool, string, string, SerializableDictionary> Data => new()
        {
            { "/DataDog/DogHouse", "GET /datadog/doghouse", HttpStatusCode.OK, false, null, null, DatadogAreaTags() },
            { "/DataDog/DogHouse/Woof", "GET /datadog/doghouse/woof", HttpStatusCode.OK, false, null, null, DatadogAreaWoofTags() },
            { "/", "GET /", HttpStatusCode.OK, false, null, null, HomeIndexTags() },
            { "/Home", "GET /home", HttpStatusCode.OK, false, null, null, HomeIndexTags() },
            { "/Home/Index", "GET /home/index", HttpStatusCode.OK, false, null, null, HomeIndexTags() },
            { "/Home/Get", "GET /home/get", HttpStatusCode.InternalServerError, true, "System.ArgumentException", MissingParameterError(), HomeGetTags() },
            { "/Home/Get/3", "GET /home/get/?", HttpStatusCode.OK, false, null, null, HomeGetTags() },
            { "/delay/0", "GET /delay/{seconds}", HttpStatusCode.OK, false, null, null, DelayTags() },
            { "/delay-async/0", "GET /delay-async/{seconds}", HttpStatusCode.OK, false, null, null, DelayAsyncTags() },
            { "/delay-optional", "GET /delay-optional/{seconds}", HttpStatusCode.OK, false, null, null, DelayOptionalTags() },
            { "/delay-optional/1", "GET /delay-optional/{seconds}", HttpStatusCode.OK, false, null, null, DelayOptionalTags() },
            { "/badrequest", "GET /badrequest", HttpStatusCode.InternalServerError, true, "System.Exception", "Oops, it broke.", BadRequestTags() },
            { "/statuscode/201", "GET /statuscode/{value}", HttpStatusCode.Created, false, null, null, StatusCodeTags() },
            { "/statuscode/503", "GET /statuscode/{value}", HttpStatusCode.ServiceUnavailable, true, null, "The HTTP response has status code 503.", StatusCodeTags() },
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
                "aspnet-mvc.request",
                expectedResourceName,
                "1.0.0",
                expectedTags);
        }

        private static SerializableDictionary DatadogAreaTags() => new()
        {
            { Tags.AspNetRoute, "Datadog/{controller}/{action}/{id}" },
            { Tags.AspNetController, "doghouse" },
            { Tags.AspNetAction, "index" },
            { Tags.AspNetArea, "datadog" }
        };

        private static SerializableDictionary DatadogAreaWoofTags() => new()
        {
            { Tags.AspNetRoute, "Datadog/{controller}/{action}/{id}" },
            { Tags.AspNetController, "doghouse" },
            { Tags.AspNetAction, "woof" },
            { Tags.AspNetArea, "datadog" }
        };

        private static string DefaultRoute() => "{controller}/{action}/{id}";

        private static SerializableDictionary HomeIndexTags() => new()
        {
            { Tags.AspNetRoute, DefaultRoute() },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "index" }
        };

        private static SerializableDictionary HomeGetTags() => new()
        {
            { Tags.AspNetRoute, DefaultRoute() },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "get" }
        };

        private static SerializableDictionary DelayTags() => new()
        {
            { Tags.AspNetRoute, "delay/{seconds}" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "delay" }
        };

        private static SerializableDictionary DelayOptionalTags() => new()
        {
            { Tags.AspNetRoute, "delay-optional/{seconds}" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "optional" }
        };

        private static SerializableDictionary BadRequestTags() => new()
        {
            { Tags.AspNetRoute, "badrequest" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "badrequest" }
        };

        private static SerializableDictionary DelayAsyncTags() => new()
        {
            { Tags.AspNetRoute, "delay-async/{seconds}" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "delayasync" }
        };

        private static SerializableDictionary StatusCodeTags() => new()
        {
            { Tags.AspNetRoute, "statuscode/{value}" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "statuscode" }
        };

        private static string MissingParameterError() => @"The parameters dictionary contains a null entry for parameter 'id' of non-nullable type 'System.Int32' for method 'System.Web.Mvc.ActionResult Get(Int32)' in 'Samples.AspNetMvc5.Controllers.HomeController'. An optional parameter must be a reference type, a nullable type, or be declared as an optional parameter.
Parameter name: parameters";
    }
}
#endif
