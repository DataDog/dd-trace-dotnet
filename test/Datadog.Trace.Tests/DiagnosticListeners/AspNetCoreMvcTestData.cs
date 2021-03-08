using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.Tests.DiagnosticListeners
{
    public static class AspNetCoreMvcTestData
    {
        /// <summary>
        /// Gets data for MVC tests with the feature flags disabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static TheoryData<string, bool, string, SerializableDictionary> WithoutFeatureFlag => new()
        {
            { "/", false, "GET Home/Index", EmptyTags() },
            { "/Home", false, "GET Home/Index", EmptyTags() },
            { "/Home/Index", false, "GET Home/Index", EmptyTags() },
            { "/Home/Error", true, "GET Home/Error", EmptyTags() },
            { "/MyTest", false, "GET MyTest/Index", EmptyTags() },
            { "/MyTest/index", false, "GET MyTest/Index", EmptyTags() },
            { "/statuscode", false, "GET statuscode/{value=200}", EmptyTags() },
            { "/statuscode/100", false, "GET statuscode/{value=200}", EmptyTags() },
            { "/statuscode/Oops", false, "GET statuscode/{value=200}", EmptyTags() },
            { "/statuscode/200", false, "GET statuscode/{value=200}", EmptyTags() },
        };

        /// <summary>
        /// Gets data for MVC tests with the feature flags enabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static TheoryData<string, bool, string, SerializableDictionary> WithFeatureFlag => new()
        {
            { "/", false, "GET /home/index", ConventionalRouteTags() },
            { "/Home", false, "GET /home/index", ConventionalRouteTags() },
            { "/Home/Index", false, "GET /home/index", ConventionalRouteTags() },
            { "/Home/Error", true, "GET /home/error", ConventionalRouteTags(action: "error") },
            { "/MyTest", false, "GET /mytest/index", ConventionalRouteTags(controller: "mytest") },
            { "/MyTest/index", false, "GET /mytest/index", ConventionalRouteTags(controller: "mytest") },
            { "/statuscode", false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/100", false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/Oops", false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/200", false, "GET /statuscode/{value}", StatusCodeTags() },
        };

        private static SerializableDictionary EmptyTags() => new()
        {
            { Tags.AspNetRoute, null },
            { Tags.AspNetController, null },
            { Tags.AspNetAction, null },
            // { Tags.AspNetEndpoint, endpoint },
        };

        private static SerializableDictionary ConventionalRouteTags(
            string action = "index",
            string controller = "home") => new()
        {
            { Tags.AspNetRoute, "{controller=home}/{action=index}/{id?}" },
            { Tags.AspNetController, controller },
            { Tags.AspNetAction, action },
            // { Tags.AspNetEndpoint, endpoint },
        };

        private static SerializableDictionary StatusCodeTags() => new()
        {
            { Tags.AspNetRoute, "statuscode/{value=200}" },
            { Tags.AspNetController, "mytest" },
            { Tags.AspNetAction, "setstatuscode" },
            // { Tags.AspNetEndpoint, endpoint },
        };
    }
}
