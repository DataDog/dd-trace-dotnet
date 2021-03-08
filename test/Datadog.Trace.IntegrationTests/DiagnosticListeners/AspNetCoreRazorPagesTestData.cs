#if !NETFRAMEWORK
using Datadog.Trace.TestHelpers;
using Xunit;
using static Datadog.Trace.IntegrationTests.DiagnosticListeners.ErrorHandlingHelper;

namespace Datadog.Trace.IntegrationTests.DiagnosticListeners
{
    public static class AspNetCoreRazorPagesTestData
    {
        /// <summary>
        /// Gets data for MVC tests with the feature flags disabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static TheoryData<string, int, bool, string, SerializableDictionary> WithoutFeatureFlag => new()
        {
            { "/", 200, false, "GET ", EmptyTags() },
            { "/Index", 200, false, "GET Index", EmptyTags() },
            { "/Privacy", 200, false, "GET Privacy", EmptyTags() },
            { "/Products", 200, false, "GET Products", EmptyTags() },
            { "/Products/Index", 200, false, "GET Products/Index", EmptyTags() },
            { "/Products/Product", 404, false, "GET /products/product", EmptyTags() },
            { "/Products/Product/123", 200, false, "GET Products/Product/{id}", EmptyTags() },
            { "/Products/Product/Oops", 400, false, "GET Products/Product/{id}", EmptyTags() },
            { "/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags() },
            { "/Error", 500, true, "GET Error", EmptyTags() },
            { "/UncaughtError", 500, true, "GET UncaughtError", EmptyTags() },
            { "/BadHttpRequest", 400, true, "GET BadHttpRequest", EmptyTags() },
            { $"{CustomHandlerPrefix}/Error", 500, true, "GET Error", EmptyTags() },
            { $"{CustomHandlerPrefix}/UncaughtError", 500, true, "GET UncaughtError", EmptyTags() },
            { $"{CustomHandlerPrefix}/BadHttpRequest", 500, true, "GET BadHttpRequest", EmptyTags() },
            { $"{ExceptionPagePrefix}/Error", 500, true, "GET Error", EmptyTags() },
            { $"{ExceptionPagePrefix}/BadHttpRequest", 400, true, "GET BadHttpRequest", EmptyTags() },
            // { $"{ReExecuteHandlerPrefix}/Error", 500, true, "GET Error", EmptyTags() },
            // { $"{ReExecuteHandlerPrefix}/BadHttpRequest", 500, true, "GET BadHttpRequest", EmptyTags() },
        };

        /// <summary>
        /// Gets data for MVC tests with the feature flags enabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static TheoryData<string, int, bool, string, SerializableDictionary> WithFeatureFlag => new()
        {
            { "/", 200, false, "GET Index", EmptyTags() },
            { "/Index", 200, false, "GET Index", EmptyTags() },
            { "/Privacy", 200, false, "GET Privacy", EmptyTags() },
            { "/Error", 500, true, "GET Error", EmptyTags() },
            { "/Products", 200, false, "GET Products", EmptyTags() },
            { "/Products/Index", 200, false, "GET Products/Index", EmptyTags() },
            { "/Products/Product", 404, true, "GET Products/Product", EmptyTags() },
            { "/Products/Product/123", 200, false, "GET Products/Product/{id}", EmptyTags() },
            { "/Products/Product/Oops", 400, false, "GET Products/Product/{id}", EmptyTags() },
            { "/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags() },
            { "/Error", 500, true, "GET /error", ConventionalRouteTags(action: "error") },
            { "/UncaughtError", 500, true, "GET /uncaughterror", ConventionalRouteTags(action: "uncaughterror") },
            { "/BadHttpRequest", 400, true, "GET /badhttprequest", ConventionalRouteTags(action: "badhttprequest") },
            { $"{CustomHandlerPrefix}/Error", 500, true, $"GET {CustomHandlerPrefix}/Error", ConventionalRouteTags(action: "error") },
            { $"{CustomHandlerPrefix}/UncaughtError", 500, true, $"GET {CustomHandlerPrefix}/uncaughterror", ConventionalRouteTags(action: "uncaughterror") },
            { $"{CustomHandlerPrefix}/BadHttpRequest", 500, true, $"GET {CustomHandlerPrefix}/badhttprequest", ConventionalRouteTags(action: "badhttprequest") },
            { $"{ExceptionPagePrefix}/Error", 500, true, $"GET {ExceptionPagePrefix}/Error", ConventionalRouteTags(action: "error") },
            { $"{ExceptionPagePrefix}/BadHttpRequest", 400, true, $"GET {ExceptionPagePrefix}/badhttprequest", ConventionalRouteTags(action: "badhttprequest") },
            { $"{ReExecuteHandlerPrefix}/Error", 500, true, $"GET {ReExecuteHandlerPrefix}/Error", ConventionalRouteTags(action: "error") },
            { $"{ReExecuteHandlerPrefix}/BadHttpRequest", 500, true, $"GET {ReExecuteHandlerPrefix}/badhttprequest", ConventionalRouteTags(action: "badhttprequest") },
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
    }
}
#endif
