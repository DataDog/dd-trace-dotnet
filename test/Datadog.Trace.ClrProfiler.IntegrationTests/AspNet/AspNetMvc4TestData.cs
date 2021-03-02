using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public static class AspNetMvc4TestData
    {
        public static TheoryData<string, string, int, bool, string, string, SerializableDictionary> Data => new()
        {
            { "/Admin", "GET /admin", 200, false, null, null, AdminHomeIndexTags },
            { "/Admin/Home", "GET /admin/home", 200, false, null, null, AdminHomeIndexTags },
            { "/Admin/Home/Index", "GET /admin/home/index", 200, false, null, null, AdminHomeIndexTags },
            { "/", "GET /", 200, false, null, null, HomeIndexTags },
            { "/Home", "GET /home", 200, false, null, null, HomeIndexTags },
            { "/Home/Index", "GET /home/index", 200, false, null, null, HomeIndexTags },
            { "/Home/BadRequest", "GET /home/badrequest", 500, true, "System.Exception", "Oops, it broke.", BadRequestTags },
            { "/Home/identifier", "GET /home/identifier", 500, true, "System.ArgumentException", MissingParameterError, IdentifierTags },
            { "/Home/identifier/123", "GET /home/identifier/?", 200, false, null, null, IdentifierTags },
            { "/Home/identifier/BadValue", "GET /home/identifier/badvalue", 500, true, "System.ArgumentException", MissingParameterError, IdentifierTags },
            { "/Home/OptionalIdentifier", "GET /home/optionalidentifier", 200, false, null, null, OptionalIdentifierTags },
            { "/Home/OptionalIdentifier/123", "GET /home/optionalidentifier/?", 200, false, null, null, OptionalIdentifierTags },
            { "/Home/OptionalIdentifier/BadValue", "GET /home/optionalidentifier/badvalue", 200, false, null, null, OptionalIdentifierTags },
            { "/Home/StatusCode?value=201", "GET /home/statuscode", 201, false, null, null, StatusCodeTags },
            { "/Home/StatusCode?value=503", "GET /home/statuscode", 503, true, null, "The HTTP response has status code 503.", StatusCodeTags },
        };

        private static SerializableDictionary AdminHomeIndexTags => new()
        {
            { Tags.AspNetRoute, "Admin/{controller}/{action}/{id}" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "index" },
            { Tags.AspNetArea, "admin" }
        };

        private static string DefaultRoute => "{controller}/{action}/{id}";

        private static SerializableDictionary HomeIndexTags => new()
        {
            { Tags.AspNetRoute, DefaultRoute },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "index" }
        };

        private static SerializableDictionary BadRequestTags => new()
        {
            { Tags.AspNetRoute, DefaultRoute },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "badrequest" }
        };

        private static SerializableDictionary StatusCodeTags => new()
        {
            { Tags.AspNetRoute, DefaultRoute },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "statuscode" }
        };

        private static SerializableDictionary IdentifierTags => new()
        {
            { Tags.AspNetRoute, DefaultRoute },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "identifier" }
        };

        private static SerializableDictionary OptionalIdentifierTags => new()
        {
            { Tags.AspNetRoute, DefaultRoute },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "optionalidentifier" }
        };

        private static string MissingParameterError => @"The parameters dictionary contains a null entry for parameter 'id' of non-nullable type 'System.Int32' for method 'System.Web.Mvc.ActionResult Identifier(Int32)' in 'Samples.AspNetMvc4.Controllers.HomeController'. An optional parameter must be a reference type, a nullable type, or be declared as an optional parameter.
Parameter name: parameters";
    }
}
