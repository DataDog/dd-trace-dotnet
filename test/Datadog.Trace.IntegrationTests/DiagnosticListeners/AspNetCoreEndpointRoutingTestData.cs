#if !NETFRAMEWORK
using Datadog.Trace.TestHelpers;
using Xunit;
using static Datadog.Trace.IntegrationTests.DiagnosticListeners.ErrorHandlingHelper;

namespace Datadog.Trace.IntegrationTests.DiagnosticListeners
{
    public static class AspNetCoreEndpointRoutingTestData
    {
        /// <summary>
        /// Gets data for Endpoint Routing tests with the feature flags disabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static TheoryData<string, int, bool, string, SerializableDictionary> WithoutFeatureFlag => new()
        {
            { "/", 200, false, "GET Home/Index", EmptyTags() },
            { "/Home", 200, false, "GET Home/Index", EmptyTags() },
            { "/Home/Index", 200, false, "GET Home/Index", EmptyTags() },
            { "/MyTest", 200, false, "GET MyTest/Index", EmptyTags() },
            { "/MyTest/index", 200, false, "GET MyTest/Index", EmptyTags() },
            { "/Api/index", 200, false, "GET api/Index", EmptyTags() },
            { "/Api/Value/3", 200, false, "GET api/Value/{value}", EmptyTags() },
            { "/Api/Value/100", 400, false, "GET api/Value/{value}", EmptyTags() },
            { "/statuscode", 200, false, "GET statuscode/{value=200}", EmptyTags() },
            { "/statuscode/401", 401, false, "GET statuscode/{value=200}", EmptyTags() },
            { "/statuscode/200", 200, false, "GET statuscode/{value=200}", EmptyTags() },
            { "/statuscode/201", 201, false, "GET statuscode/{value=200}", EmptyTags() },
            { "/healthz", 200, false, "GET /healthz", EmptyTags() },
            { "/echo", 200, false, "GET /echo", EmptyTags() },
            { "/echo/123", 200, false, "GET /echo/?", EmptyTags() },
            { "/echo/false", 404, false, "GET /echo/false", EmptyTags() },
            { "/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags() },
            { "/Home/Error", 500, true, "GET Home/Error", EmptyTags() },
            { "/Home/UncaughtError", 500, true, "GET Home/UncaughtError", EmptyTags() },
            { "/Home/BadHttpRequest", 400, true, "GET Home/BadHttpRequest", EmptyTags() },
            { $"{CustomHandlerPrefix}/Home/Error", 500, true, "GET Home/Error", EmptyTags() },
            { $"{CustomHandlerPrefix}/Home/UncaughtError", 500, true, "GET Home/UncaughtError", EmptyTags() },
            { $"{CustomHandlerPrefix}/Home/BadHttpRequest", 500, true, "GET Home/BadHttpRequest", EmptyTags() },
            { $"{CustomHandlerPrefix}/throws", 500, true, $"GET {CustomHandlerPrefix}/throws", EmptyTags() },
            { $"{ExceptionPagePrefix}/Home/Error", 500, true, "GET Home/Error", EmptyTags() },
            { $"{ExceptionPagePrefix}/Home/BadHttpRequest", 400, true, "GET Home/BadHttpRequest", EmptyTags() },
            { $"{ExceptionPagePrefix}/throws", 500, true, $"GET {ExceptionPagePrefix}/throws", EmptyTags() },
            // { $"{ReExecuteHandlerPrefix}/Home/Error", 500, true, "GET Home/Error", EmptyTags() },
            // { $"{ReExecuteHandlerPrefix}/Home/BadHttpRequest", 500, true, "GET Home/BadHttpRequest", EmptyTags() },
            // { $"{ReExecuteHandlerPrefix}/throws", 500, true, $"GET {ReExecuteHandlerPrefix}/throws", EmptyTags() },
        };

        /// <summary>
        /// Gets data for Endpoint Routing tests with the feature flags enabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static TheoryData<string, int, bool, string, SerializableDictionary> WithFeatureFlag => new()
        {
            { "/", 200, false, "GET /home/index", ConventionalRouteTags(endpoint: "HomeController.Index") },
            { "/Home", 200, false, "GET /home/index", ConventionalRouteTags(endpoint: "HomeController.Index") },
            { "/Home/Index", 200, false, "GET /home/index", ConventionalRouteTags(endpoint: "HomeController.Index") },
            { "/Api/index", 200, false, "GET /api/index", ApiIndexTags() },
            { "/Api/Value/3", 400, false, "GET /api/value/{value}", ApiValueTags() },
            { "/Api/Value/200", 200, false, "GET /api/value/{value}", ApiValueTags() },
            { "/Api/Value/201", 201, false, "GET /api/value/{value}", ApiValueTags() },
            { "/Api/Value/401", 401, false, "GET /api/value/{value}", ApiValueTags() },
            { "/MyTest", 200, false, "GET /mytest/index", ConventionalRouteTags(controller: "mytest", endpoint: "MyTestController.Index") },
            { "/MyTest/index", 200, false, "GET /mytest/index", ConventionalRouteTags(controller: "mytest", endpoint: "MyTestController.Index") },
            { "/statuscode", 200, false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/100", 200, false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/Oops", 200, false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/statuscode/200", 200, false, "GET /statuscode/{value}", StatusCodeTags() },
            { "/healthz", 200, false, "GET /healthz", HealthCheckTags() },
            { "/echo", 200, false, "GET /echo", EchoTags() },
            { "/echo/123", 200, false, "GET /echo/{value?}", EchoTags() },
            { "/echo/false", 404, false, "GET /echo/false", EmptyTags() },
            { "/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags() },
            { "/Home/Error", 500, true, "GET /home/error", ErrorRouteTags() },
            { "/Home/UncaughtError", 500, true, "GET /home/uncaughterror", UncaughtErrorTags() },
            { "/Home/BadHttpRequest", 400, true, "GET /home/badhttprequest", BadHttpRequestTags() },
            { $"{CustomHandlerPrefix}/Home/Error", 500, true, $"GET {CustomHandlerPrefix}/home/error", ErrorRouteTags() },
            { $"{CustomHandlerPrefix}/Home/UncaughtError", 500, true, $"GET {CustomHandlerPrefix}/home/uncaughterror", UncaughtErrorTags() },
            { $"{CustomHandlerPrefix}/Home/BadHttpRequest", 500, true, $"GET {CustomHandlerPrefix}/home/badhttprequest", BadHttpRequestTags() },
            { $"{CustomHandlerPrefix}/throws", 500, true, $"GET {CustomHandlerPrefix}/throws", ThrowsTags() },
            { $"{ExceptionPagePrefix}/Home/Error", 500, true, $"GET {ExceptionPagePrefix}/home/error", ErrorRouteTags() },
            { $"{ExceptionPagePrefix}/Home/BadHttpRequest", 400, true, $"GET {ExceptionPagePrefix}/home/badhttprequest", BadHttpRequestTags() },
            { $"{ExceptionPagePrefix}/throws", 500, true, $"GET {ExceptionPagePrefix}/throws", ThrowsTags() },
            { $"{ReExecuteHandlerPrefix}/Home/Error", 500, true, $"GET {ReExecuteHandlerPrefix}/home/error", ErrorRouteTags() },
            { $"{ReExecuteHandlerPrefix}/Home/BadHttpRequest", 500, true, $"GET {ReExecuteHandlerPrefix}/home/badhttprequest", BadHttpRequestTags() },
            { $"{ReExecuteHandlerPrefix}/throws", 500, true, $"GET {ReExecuteHandlerPrefix}/throws", ThrowsTags() },
        };

        private static SerializableDictionary EmptyTags() => new()
        {
            { Tags.AspNetRoute, null },
            { Tags.AspNetController, null },
            { Tags.AspNetAction, null },
            { Tags.AspNetArea, null },
            { Tags.AspNetPage, null },
            { Tags.AspNetEndpoint, null },
        };

        private static SerializableDictionary ConventionalRouteTags(
            string action = "index",
            string controller = "home",
            string endpoint = null) => new()
        {
            { Tags.AspNetRoute, "{controller=home}/{action=index}/{id?}" },
            { Tags.AspNetController, controller },
            { Tags.AspNetAction, action },
            { Tags.AspNetArea, null },
            { Tags.AspNetPage, null },
            { Tags.AspNetEndpoint, $"Datadog.Trace.IntegrationTests.DiagnosticListeners.{endpoint} (Datadog.Trace.IntegrationTests)" },
        };

        private static SerializableDictionary ErrorRouteTags()
            => ConventionalRouteTags(action: "error", endpoint: "HomeController.Error");

        private static SerializableDictionary BadHttpRequestTags()
            => ConventionalRouteTags(action: "badhttprequest", endpoint: "HomeController.BadHttpRequest");

        private static SerializableDictionary UncaughtErrorTags()
            => ConventionalRouteTags(action: "uncaughterror", endpoint: "HomeController.UncaughtError");

        private static SerializableDictionary StatusCodeTags() => new()
        {
            { Tags.AspNetRoute, "statuscode/{value=200}" },
            { Tags.AspNetController, "mytest" },
            { Tags.AspNetAction, "setstatuscode" },
            { Tags.AspNetArea, null },
            { Tags.AspNetPage, null },
            { Tags.AspNetEndpoint, "Datadog.Trace.IntegrationTests.DiagnosticListeners.MyTestController.SetStatusCode (Datadog.Trace.IntegrationTests)" },
        };

        private static SerializableDictionary HealthCheckTags() => new()
        {
            { Tags.AspNetRoute, "/healthz" },
            { Tags.AspNetController, null },
            { Tags.AspNetAction, null },
            { Tags.AspNetArea, null },
            { Tags.AspNetPage, null },
            { Tags.AspNetEndpoint, "Custom Health Check" },
        };

        private static SerializableDictionary EchoTags() => new()
        {
            { Tags.AspNetRoute, "/echo/{value:int?}" },
            { Tags.AspNetController, null },
            { Tags.AspNetAction, null },
            { Tags.AspNetArea, null },
            { Tags.AspNetPage, null },
            { Tags.AspNetEndpoint, "/echo/{value:int?} HTTP: GET" },
        };

        private static SerializableDictionary ThrowsTags() => new()
        {
            { Tags.AspNetRoute, "/throws" },
            { Tags.AspNetController, null },
            { Tags.AspNetAction, null },
            { Tags.AspNetArea, null },
            { Tags.AspNetPage, null },
            { Tags.AspNetEndpoint, "/throws HTTP: GET" },
        };

        private static SerializableDictionary ApiIndexTags() => new()
        {
            { Tags.AspNetRoute, "api/index" },
            { Tags.AspNetController, "api" },
            { Tags.AspNetAction, "index" },
            { Tags.AspNetArea, null },
            { Tags.AspNetPage, null },
            { Tags.AspNetEndpoint, "Datadog.Trace.IntegrationTests.DiagnosticListeners.ApiController.Index (Datadog.Trace.IntegrationTests)" },
        };

        private static SerializableDictionary ApiValueTags() => new()
        {
            { Tags.AspNetRoute, "api/value/{value}" },
            { Tags.AspNetController, "api" },
            { Tags.AspNetAction, "value" },
            { Tags.AspNetArea, null },
            { Tags.AspNetPage, null },
            { Tags.AspNetEndpoint, "Datadog.Trace.IntegrationTests.DiagnosticListeners.ApiController.Value (Datadog.Trace.IntegrationTests)" },
        };
    }
}
#endif
