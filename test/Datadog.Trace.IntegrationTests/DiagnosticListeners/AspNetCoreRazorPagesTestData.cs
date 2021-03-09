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
            // The below is the ideal behaviour, but we can't achieve that currently
            // { $"{ReExecuteHandlerPrefix}/Error", 500, true, "GET Error", EmptyTags() },
            // { $"{ReExecuteHandlerPrefix}/BadHttpRequest", 500, true, "GET BadHttpRequest", EmptyTags() },
        };

        /// <summary>
        /// Gets data for MVC tests with the feature flags enabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static TheoryData<string, int, bool, string, SerializableDictionary> WithFeatureFlag => new()
        {
            { "/", 200, false, "GET /", ConventionalRouteTags("Index", route: string.Empty) },
            { "/Index", 200, false, "GET /index", ConventionalRouteTags("Index") },
            { "/Privacy", 200, false, "GET /privacy", ConventionalRouteTags("Privacy") },
            { "/Error", 500, true, "GET /error", ConventionalRouteTags("Error") },
            { "/Products", 200, false, "GET /products", ConventionalRouteTags("Products/Index", route: "Products") },
            { "/Products/Index", 200, false, "GET /products/index", ConventionalRouteTags("Products/Index") },
            { "/Products/Product", 404, false, "GET /products/product", EmptyTags() },
            { "/Products/Product/123", 200, false, "GET /products/product/{id}", ConventionalRouteTags(page: "Products/Product", route: "Products/Product/{id}") },
            { "/Products/Product/Oops", 400, false, "GET /products/product/{id}", ConventionalRouteTags(page: "Products/Product", route: "Products/Product/{id}") },
            { "/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags() },
            { "/Error", 500, true, "GET /error", ConventionalRouteTags(page: "Error") },
            { "/UncaughtError", 500, true, "GET /uncaughterror", ConventionalRouteTags(page: "UncaughtError") },
            { "/BadHttpRequest", 400, true, "GET /badhttprequest", ConventionalRouteTags(page: "BadHttpRequest") },
            { $"{CustomHandlerPrefix}/Error", 500, true, $"GET {CustomHandlerPrefix}/error", ConventionalRouteTags(page: "Error") },
            { $"{CustomHandlerPrefix}/UncaughtError", 500, true, $"GET {CustomHandlerPrefix}/uncaughterror", ConventionalRouteTags(page: "UncaughtError") },
            { $"{CustomHandlerPrefix}/BadHttpRequest", 500, true, $"GET {CustomHandlerPrefix}/badhttprequest", ConventionalRouteTags(page: "BadHttpRequest") },
            { $"{ExceptionPagePrefix}/Error", 500, true, $"GET {ExceptionPagePrefix}/error", ConventionalRouteTags(page: "Error") },
            { $"{ExceptionPagePrefix}/BadHttpRequest", 400, true, $"GET {ExceptionPagePrefix}/badhttprequest", ConventionalRouteTags(page: "BadHttpRequest") },
            { $"{ReExecuteHandlerPrefix}/Error", 500, true, $"GET {ReExecuteHandlerPrefix}/error", ConventionalRouteTags(page: "Error") },
            { $"{ReExecuteHandlerPrefix}/BadHttpRequest", 500, true, $"GET {ReExecuteHandlerPrefix}/badhttprequest", ConventionalRouteTags(page: "BadHttpRequest") },
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

        private static SerializableDictionary ConventionalRouteTags(string page, string route = null) => new()
        {
            { Tags.AspNetRoute, (route ?? page).ToLowerInvariant() },
            { Tags.AspNetController, null },
            { Tags.AspNetAction, null },
            { Tags.AspNetPage, $"/{page.ToLowerInvariant()}" },
            { Tags.AspNetArea, null },
#if NETCOREAPP && !NETCOREAPP2_1 && !NETCOREAPP3_0
            { Tags.AspNetEndpoint, $"/{page}" },
#endif
        };
    }
}
#endif
