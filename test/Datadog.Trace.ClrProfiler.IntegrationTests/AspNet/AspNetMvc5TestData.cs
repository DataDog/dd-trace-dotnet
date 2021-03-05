using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests
{
    public class AspNetMvc5TestData
    {
        public static TheoryData<string, string, int, bool, string, string, SerializableDictionary> Data => new()
        {
            { "/DataDog/DogHouse", "GET /datadog/doghouse", 200, false, null, null, DatadogAreaTags },
            { "/DataDog/DogHouse/Woof", "GET /datadog/doghouse/woof", 200, false, null, null, DatadogAreaWoofTags },
            { "/", "GET /", 200, false, null, null, HomeIndexTags },
            { "/Home", "GET /home", 200, false, null, null, HomeIndexTags },
            { "/Home/Index", "GET /home/index", 200, false, null, null, HomeIndexTags },
            { "/Home/Get", "GET /home/get", 500, true, "System.ArgumentException", MissingParameterError, HomeGetTags },
            { "/Home/Get/3", "GET /home/get/?", 200, false, null, null, HomeGetTags },
            { "/delay/0", "GET /delay/{seconds}", 200, false, null, null, DelayTags },
            { "/delay-async/0", "GET /delay-async/{seconds}", 200, false, null, null, DelayAsyncTags },
            { "/delay-optional", "GET /delay-optional/{seconds}", 200, false, null, null, DelayOptionalTags },
            { "/delay-optional/1", "GET /delay-optional/{seconds}", 200, false, null, null, DelayOptionalTags },
            { "/badrequest", "GET /badrequest", 500, true, "System.Exception", "Oops, it broke.", BadRequestTags },
            { "/statuscode/201", "GET /statuscode/{value}", 201, false, null, null, StatusCodeTags },
            { "/statuscode/503", "GET /statuscode/{value}", 503, true, null, "The HTTP response has status code 503.", StatusCodeTags },
        };

        private static SerializableDictionary DatadogAreaTags => new()
        {
            { Tags.AspNetRoute, "Datadog/{controller}/{action}/{id}" },
            { Tags.AspNetController, "doghouse" },
            { Tags.AspNetAction, "index" },
            { Tags.AspNetArea, "datadog" }
        };

        private static SerializableDictionary DatadogAreaWoofTags => new()
        {
            { Tags.AspNetRoute, "Datadog/{controller}/{action}/{id}" },
            { Tags.AspNetController, "doghouse" },
            { Tags.AspNetAction, "woof" },
            { Tags.AspNetArea, "datadog" }
        };

        private static string DefaultRoute => "{controller}/{action}/{id}";

        private static SerializableDictionary HomeIndexTags => new()
        {
            { Tags.AspNetRoute, DefaultRoute },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "index" }
        };

        private static SerializableDictionary HomeGetTags => new()
        {
            { Tags.AspNetRoute, DefaultRoute },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "get" }
        };

        private static SerializableDictionary DelayTags => new()
        {
            { Tags.AspNetRoute, "delay/{seconds}" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "delay" }
        };

        private static SerializableDictionary DelayOptionalTags => new()
        {
            { Tags.AspNetRoute, "delay-optional/{seconds}" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "optional" }
        };

        private static SerializableDictionary BadRequestTags => new()
        {
            { Tags.AspNetRoute, "badrequest" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "badrequest" }
        };

        private static SerializableDictionary DelayAsyncTags => new()
        {
            { Tags.AspNetRoute, "delay-async/{seconds}" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "delayasync" }
        };

        private static SerializableDictionary StatusCodeTags => new()
        {
            { Tags.AspNetRoute, "statuscode/{value}" },
            { Tags.AspNetController, "home" },
            { Tags.AspNetAction, "statuscode" }
        };

        private static string MissingParameterError => @"The parameters dictionary contains a null entry for parameter 'id' of non-nullable type 'System.Int32' for method 'System.Web.Mvc.ActionResult Get(Int32)' in 'Samples.AspNetMvc5.Controllers.HomeController'. An optional parameter must be a reference type, a nullable type, or be declared as an optional parameter.
Parameter name: parameters";
    }
}
