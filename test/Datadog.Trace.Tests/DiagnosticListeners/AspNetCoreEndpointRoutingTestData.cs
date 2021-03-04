using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.Tests.DiagnosticListeners
{
    public static class AspNetCoreEndpointRoutingTestData
    {
        private const string IndexEndpointName = "Datadog.Trace.Tests.DiagnosticListeners.HomeController.Index (Datadog.Trace.Tests)";
        private const string ErrorEndpointName = "Datadog.Trace.Tests.DiagnosticListeners.HomeController.Error (Datadog.Trace.Tests)";
        private const string MyTestEndpointName = "Datadog.Trace.Tests.DiagnosticListeners.MyTestController.Index (Datadog.Trace.Tests)";
        // private const string StatusCodeEndpointName = "Datadog.Trace.Tests.DiagnosticListeners.MyTestController.SetStatusCode (Datadog.Trace.Tests)";

        /// <summary>
        /// Gets data for Endpoint Routing tests with the feature flags disabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static TheoryData<string, int, bool, string, SerializableDictionary> WithoutFeatureFlag => new()
        {
            { "/", 200, false, "GET Home/Index", EmptyTags() },
            { "/Home", 200, false, "GET Home/Index", EmptyTags() },
            { "/Home/Index", 200, false, "GET Home/Index", EmptyTags() },
            { "/Home/Error", 500, true, "GET Home/Error", EmptyTags() },
            { "/MyTest", 200, false, "GET MyTest/Index", EmptyTags() },
            { "/MyTest/index", 200, false, "GET MyTest/Index", EmptyTags() },
            { "/statuscode", 200, false, "GET statuscode/{value=200}", EmptyTags() },
            { "/statuscode/100", 200, false, "GET statuscode/{value=200}", EmptyTags() },
            { "/statuscode/Oops", 200, false, "GET statuscode/{value=200}", EmptyTags() },
            { "/statuscode/200", 200, false, "GET statuscode/{value=200}", EmptyTags() },
            { "/healthz", 200, false, "GET /healthz", EmptyTags() },
            { "/echo", 200, false, "GET /echo", EmptyTags() },
            { "/echo/123", 200, false, "GET /echo/?", EmptyTags() },
            { "/echo/false", 404, false, "GET /echo/false", EmptyTags() },
            { "/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags() },
        };

        /// <summary>
        /// Gets data for Endpoint Routing tests with the feature flags enabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static TheoryData<string, int, bool, string, SerializableDictionary> WithFeatureFlag => new()
        {
            { "/", 200, false, "GET /home/index", ConventionalRouteTags(endpoint: IndexEndpointName) },
            { "/Home", 200, false, "GET /home/index", ConventionalRouteTags(endpoint: IndexEndpointName) },
            { "/Home/Index", 200, false, "GET /home/index", ConventionalRouteTags(endpoint: IndexEndpointName) },
            { "/Home/Error", 500, true, "GET /home/error", ConventionalRouteTags(action: "error", endpoint: ErrorEndpointName) },
            { "/MyTest", 200, false, "GET /mytest/index", ConventionalRouteTags(controller: "mytest", endpoint: MyTestEndpointName) },
            { "/MyTest/index", 200, false, "GET /mytest/index", ConventionalRouteTags(controller: "mytest", endpoint: MyTestEndpointName) },
            { "/statuscode", 200, false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/100", 200, false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/Oops", 200, false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/200", 200, false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/healthz", 200, false, "GET /healthz", HealthCheckTags() },
            { "/echo", 200, false, "GET /echo", EchoTags() },
            { "/echo/123", 200, false, "GET /echo/{value?}", EchoTags() },
            { "/echo/false", 404, false, "GET /echo/false", EmptyTags() },
            { "/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags() },
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
            string controller = "home",
            string endpoint = null) => new()
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
            // { Tags.AspNetEndpoint, StatusCodeEndpointName },
        };

        private static SerializableDictionary HealthCheckTags() => new()
        {
            { Tags.AspNetRoute, "/healthz" },
            // { Tags.AspNetEndpoint, "Custom Health Check" },
        };

        private static SerializableDictionary EchoTags() => new()
        {
            { Tags.AspNetRoute, "/echo/{value:int?}" },
            // { Tags.AspNetEndpoint, "/echo/{value:int?} HTTP: GET" },
        };
    }
}
