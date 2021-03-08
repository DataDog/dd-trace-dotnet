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
            { "/healthz", false, "GET /healthz", EmptyTags() },
            { "/echo", false, "GET /echo", EmptyTags() },
            { "/echo/123", false, "GET /echo/?", EmptyTags() },
            { "/echo/false", true, "GET /echo/false", EmptyTags() },
        };

        /// <summary>
        /// Gets data for Endpoint Routing tests with the feature flags enabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static TheoryData<string, bool, string, SerializableDictionary> WithFeatureFlag => new()
        {
            { "/", false, "GET /home/index", ConventionalRouteTags(endpoint: IndexEndpointName) },
            { "/Home", false, "GET /home/index", ConventionalRouteTags(endpoint: IndexEndpointName) },
            { "/Home/Index", false, "GET /home/index", ConventionalRouteTags(endpoint: IndexEndpointName) },
            { "/Home/Error", true, "GET /home/error", ConventionalRouteTags(action: "error", endpoint: ErrorEndpointName) },
            { "/MyTest", false, "GET /mytest/index", ConventionalRouteTags(controller: "mytest", endpoint: MyTestEndpointName) },
            { "/MyTest/index", false, "GET /mytest/index", ConventionalRouteTags(controller: "mytest", endpoint: MyTestEndpointName) },
            { "/statuscode", false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/100", false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/Oops", false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/200", false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/healthz", false, "GET /healthz", HealthCheckTags() },
            { "/echo", false, "GET /echo", EchoTags() },
            { "/echo/123", false, "GET /echo/{value?}", EchoTags() },
            { "/echo/false", true, "GET /echo/false", EmptyTags() },
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
