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
        /// Gets data for MVC tests with the feature flags enabled
        /// (URL, StatusCode, isError, Resource, ParentSpanTags, Span Count, ChildSpan1ResourceName, Child1SpanTags, ChildSpan2ResourceName, Child2SpanTags)
        /// </summary>
        public static TheoryData<string, int, bool, string, SerializableDictionary, int, string, SerializableDictionary, string, SerializableDictionary> WithFeatureFlag => new()
        {
            { "/", 200, false, "GET /home/index", ConventionalParentTags(endpoint: "HomeController.Index"), 2, null, ConventionalChildTags(), null, null },
            { "/Home", 200, false, "GET /home/index", ConventionalParentTags(endpoint: "HomeController.Index"), 2, null, ConventionalChildTags(), null, null },
            { "/Home/Index", 200, false, "GET /home/index", ConventionalParentTags(endpoint: "HomeController.Index"), 2, null, ConventionalChildTags(), null, null },
            { "/Api/index", 200, false, "GET /api/index", ApiIndexParentTags(), 2, null, ApiIndexChildTags(), null, null },
            { "/Api/Value/3", 200, false, "GET /api/value/{value}", ApiValueParentTags(), 2, null, ApiValueChildTags(), null, null },
            { "/Api/Value/100", 400, false, "GET /api/value/{value}", ApiValueParentTags(), 2, null, ApiValueChildTags(), null, null },
            { "/MyTest", 200, false, "GET /mytest/index", ConventionalParentTags(controller: "mytest", endpoint: "MyTestController.Index"), 2, null, ConventionalChildTags(controller: "mytest"), null, null },
            { "/MyTest/index", 200, false, "GET /mytest/index", ConventionalParentTags(controller: "mytest", endpoint: "MyTestController.Index"), 2, null, ConventionalChildTags(controller: "mytest"), null, null },
            { "/statuscode", 200, false, "GET /statuscode/{value}", StatusCodeParentTags(), 2, null, StatusCodeChildTags(), null, null },
            { "/statuscode/401", 401, false, "GET /statuscode/{value}", StatusCodeParentTags(), 2, null, StatusCodeChildTags(), null, null },
            { "/statuscode/404", 404, false, "GET /statuscode/{value}", StatusCodeParentTags(), 2, null, StatusCodeChildTags(), null, null },
            { "/statuscode/200", 200, false, "GET /statuscode/{value}", StatusCodeParentTags(), 2, null, StatusCodeChildTags(), null, null },
            { "/statuscode/201", 201, false, "GET /statuscode/{value}", StatusCodeParentTags(), 2, null, StatusCodeChildTags(), null, null },
            { "/healthz", 200, false, "GET /healthz", HealthCheckTags(), 1, null, null, null, null },
            { "/echo", 200, false, "GET /echo", EchoTags(), 1, null, null, null, null },
            { "/echo/123", 200, false, "GET /echo/{value?}", EchoTags(), 1, null, null, null, null },
            { "/echo/false", 404, false, "GET /echo/false", EmptyTags(), 1, null, null, null, null },
            { "/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags(), 1, null, null, null, null },
            { "/Home/Error", 500, true, "GET /home/error", ErrorRouteParentTags(), 2, null, ErrorRouteChildTags(), null, null },
            { "/Home/UncaughtError", 500, true, "GET /home/uncaughterror", UncaughtErrorParentTags(), 2, null, UncaughtErrorChildTags(), null, null },
            { "/Home/BadHttpRequest", 400, true, "GET /home/badhttprequest", BadHttpRequestParentTags(), 2, null, BadHttpRequestChildTags(), null, null },
            { $"{CustomHandlerPrefix}/Home/Error", 500, true, $"GET {CustomHandlerPrefix}/home/error", ErrorRouteParentTags(), 2, null, ErrorRouteChildTags(), null, null },
            { $"{CustomHandlerPrefix}/Home/UncaughtError", 500, true, $"GET {CustomHandlerPrefix}/home/uncaughterror", UncaughtErrorParentTags(), 2, null, UncaughtErrorChildTags(), null, null },
            { $"{CustomHandlerPrefix}/Home/BadHttpRequest", 500, true, $"GET {CustomHandlerPrefix}/home/badhttprequest", BadHttpRequestParentTags(), 2, null, BadHttpRequestChildTags(), null, null },
            { $"{CustomHandlerPrefix}/throws", 500, true, $"GET {CustomHandlerPrefix}/throws", ThrowsTags(), 1, null, null, null, null },
            { $"{ExceptionPagePrefix}/Home/Error", 500, true, $"GET {ExceptionPagePrefix}/home/error", ErrorRouteParentTags(), 2, null, ErrorRouteChildTags(), null, null },
            { $"{ExceptionPagePrefix}/Home/BadHttpRequest", 400, true, $"GET {ExceptionPagePrefix}/home/badhttprequest", BadHttpRequestParentTags(), 2, null, BadHttpRequestChildTags(), null, null },
            { $"{ExceptionPagePrefix}/throws", 500, true, $"GET {ExceptionPagePrefix}/throws", ThrowsTags(), 1, null, null, null, null },
            { $"{ReExecuteHandlerPrefix}/Home/Error", 500, true, $"GET {ReExecuteHandlerPrefix}/home/error", ErrorRouteParentTags(), 3, null, ErrorRouteChildTags(), $"GET {ReExecuteHandlerPrefix}/home/index", ConventionalChildTags() },
            { $"{ReExecuteHandlerPrefix}/Home/BadHttpRequest", 500, true, $"GET {ReExecuteHandlerPrefix}/home/badhttprequest", BadHttpRequestParentTags(), 3, null, BadHttpRequestChildTags(), $"GET {ReExecuteHandlerPrefix}/home/index", ConventionalChildTags() },
            { $"{ReExecuteHandlerPrefix}/throws", 500, true, $"GET {ReExecuteHandlerPrefix}/throws", ThrowsTags(), 2, $"GET {ReExecuteHandlerPrefix}/home/index", ConventionalChildTags(), null, null },
            { $"{StatusCodeReExecutePrefix}/I/dont/123/exist/", 404, false, $"GET {StatusCodeReExecutePrefix}/i/dont/?/exist/", EmptyTags(), 2, $"GET {StatusCodeReExecutePrefix}/home/index", ConventionalChildTags(), null, null },
        };

        private static SerializableDictionary EmptyTags() => new()
        {
            { Tags.AspNetCoreRoute, null },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static SerializableDictionary ConventionalParentTags(
            string action = "index",
            string controller = "home",
            string endpoint = null) => new()
        {
            { Tags.AspNetCoreRoute, "{controller=home}/{action=index}/{id?}" },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, $"Datadog.Trace.IntegrationTests.DiagnosticListeners.{endpoint} (Datadog.Trace.IntegrationTests)" },
        };

        private static SerializableDictionary ConventionalChildTags(
            string action = "index",
            string controller = "home") => new()
        {
            { Tags.AspNetCoreRoute, "{controller=home}/{action=index}/{id?}" },
            { Tags.AspNetCoreController, controller },
            { Tags.AspNetCoreAction, action },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static SerializableDictionary ErrorRouteParentTags()
            => ConventionalParentTags(action: "error", endpoint: "HomeController.Error");

        private static SerializableDictionary ErrorRouteChildTags()
            => ConventionalChildTags(action: "error");

        private static SerializableDictionary BadHttpRequestParentTags()
            => ConventionalParentTags(action: "badhttprequest", endpoint: "HomeController.BadHttpRequest");

        private static SerializableDictionary BadHttpRequestChildTags()
            => ConventionalChildTags(action: "badhttprequest");

        private static SerializableDictionary UncaughtErrorParentTags()
            => ConventionalParentTags(action: "uncaughterror", endpoint: "HomeController.UncaughtError");

        private static SerializableDictionary UncaughtErrorChildTags()
            => ConventionalChildTags(action: "uncaughterror");

        private static SerializableDictionary StatusCodeParentTags() => new()
        {
            { Tags.AspNetCoreRoute, "statuscode/{value=200}" },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, "Datadog.Trace.IntegrationTests.DiagnosticListeners.MyTestController.SetStatusCode (Datadog.Trace.IntegrationTests)" },
        };

        private static SerializableDictionary StatusCodeChildTags() => new()
        {
            { Tags.AspNetCoreRoute, "statuscode/{value=200}" },
            { Tags.AspNetCoreController, "mytest" },
            { Tags.AspNetCoreAction, "setstatuscode" },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static SerializableDictionary HealthCheckTags() => new()
        {
            { Tags.AspNetCoreRoute, "/healthz" },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, "Custom Health Check" },
        };

        private static SerializableDictionary EchoTags() => new()
        {
            { Tags.AspNetCoreRoute, "/echo/{value:int?}" },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, "/echo/{value:int?} HTTP: GET" },
        };

        private static SerializableDictionary ThrowsTags() => new()
        {
            { Tags.AspNetCoreRoute, "/throws" },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, "/throws HTTP: GET" },
        };

        private static SerializableDictionary ApiIndexParentTags() => new()
        {
            { Tags.AspNetCoreRoute, "api/index" },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, "Datadog.Trace.IntegrationTests.DiagnosticListeners.ApiController.Index (Datadog.Trace.IntegrationTests)" },
        };

        private static SerializableDictionary ApiIndexChildTags() => new()
        {
            { Tags.AspNetCoreRoute, "api/index" },
            { Tags.AspNetCoreController, "api" },
            { Tags.AspNetCoreAction, "index" },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static SerializableDictionary ApiValueParentTags() => new()
        {
            { Tags.AspNetCoreRoute, "api/value/{value}" },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, "Datadog.Trace.IntegrationTests.DiagnosticListeners.ApiController.Value (Datadog.Trace.IntegrationTests)" },
        };

        private static SerializableDictionary ApiValueChildTags() => new()
        {
            { Tags.AspNetCoreRoute, "api/value/{value}" },
            { Tags.AspNetCoreController, "api" },
            { Tags.AspNetCoreAction, "value" },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };
    }
}
#endif
