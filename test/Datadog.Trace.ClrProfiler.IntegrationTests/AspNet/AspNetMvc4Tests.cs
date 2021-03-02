#if NET461
#pragma warning disable SA1402 // File may only contain a single class
#pragma warning disable SA1649 // File name must match first type name

using System.Net;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    [CollectionDefinition("IisTests", DisableParallelization = true)]
    [Collection("IisTests")]
    public class AspNetMvc4TestsCallsite : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallsite(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: false, enableInlining: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTargetNoInlining : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTargetNoInlining(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: false)
        {
        }
    }

    [Collection("IisTests")]
    public class AspNetMvc4TestsCallTarget : AspNetMvc4Tests
    {
        public AspNetMvc4TestsCallTarget(IisFixture iisFixture, ITestOutputHelper output)
            : base(iisFixture, output, enableCallTarget: true, enableInlining: true)
        {
        }
    }

    public abstract class AspNetMvc4Tests : TestHelper, IClassFixture<IisFixture>
    {
        private readonly IisFixture _iisFixture;

        public AspNetMvc4Tests(IisFixture iisFixture, ITestOutputHelper output, bool enableCallTarget, bool enableInlining)
            : base("AspNetMvc4", @"test\test-applications\aspnet", output)
        {
            SetServiceVersion("1.0.0");
            SetCallTargetSettings(enableCallTarget, enableInlining);

            _iisFixture = iisFixture;
            _iisFixture.TryStartIis(this);
        }

        public static TheoryData<string, string, int, bool, string, string, SerializableDictionary> Data => new()
        {
            { "/Admin/Home/Index", "GET /admin/home/index", 200, false, null, null, AdminHomeIndexTags() },
            { "/", "GET /", 200, false, null, null, HomeIndexTags() },
            { "/Home", "GET /home", 200, false, null, null, HomeIndexTags() },
            { "/Home/Index", "GET /home/index", 200, false, null, null, HomeIndexTags() },
            { "/Home/BadRequest", "GET /home/badrequest", 500, true, "System.Exception", "Oops, it broke.", BadRequestTags() },
            { "/Home/identifier", "GET /home/identifier", 500, true, "System.ArgumentException", MissingParameterError(), IdentifierTags() },
            { "/Home/identifier/123", "GET /home/identifier/?", 200, false, null, null, IdentifierTags() },
            { "/Home/identifier/BadValue", "GET /home/identifier/badvalue", 500, true, "System.ArgumentException", MissingParameterError(), IdentifierTags() },
            { "/Home/OptionalIdentifier", "GET /home/optionalidentifier", 200, false, null, null, OptionalIdentifierTags() },
            { "/Home/OptionalIdentifier/123", "GET /home/optionalidentifier/?", 200, false, null, null, OptionalIdentifierTags() },
            { "/Home/OptionalIdentifier/BadValue", "GET /home/optionalidentifier/badvalue", 200, false, null, null, OptionalIdentifierTags() },
            { "/Home/StatusCode?value=201", "GET /home/statuscode", 201, false, null, null, StatusCodeTags() },
            { "/Home/StatusCode?value=503", "GET /home/statuscode", 503, true, null, "The HTTP response has status code 503.", StatusCodeTags() },
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
            SerializableDictionary tags)
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
                tags);
        }

        private static SerializableDictionary AdminHomeIndexTags() => new()
        {
            { Tags.AspNetRoute, "Admin/{controller}/{action}/{id}" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "index" },
            { Tags.AspNetArea, "admin" }
        };

        private static string DefaultRoute() => "{controller}/{action}/{id}";

        private static SerializableDictionary HomeIndexTags() => new()
        {
            { Tags.AspNetRoute, DefaultRoute() },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "index" }
        };

        private static SerializableDictionary BadRequestTags() => new()
        {
            { Tags.AspNetRoute, DefaultRoute() },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "badrequest" }
        };

        private static SerializableDictionary StatusCodeTags() => new()
        {
            { Tags.AspNetRoute, DefaultRoute() },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "statuscode" }
        };

        private static SerializableDictionary IdentifierTags() => new()
        {
            { Tags.AspNetRoute, DefaultRoute() },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "identifier" }
        };

        private static SerializableDictionary OptionalIdentifierTags() => new()
        {
            { Tags.AspNetRoute, DefaultRoute() },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "optionalidentifier" }
        };

        private static string MissingParameterError() => @"The parameters dictionary contains a null entry for parameter 'id' of non-nullable type 'System.Int32' for method 'System.Web.Mvc.ActionResult Identifier(Int32)' in 'Samples.AspNetMvc4.Controllers.HomeController'. An optional parameter must be a reference type, a nullable type, or be declared as an optional parameter.
Parameter name: parameters";
    }
}
#endif
