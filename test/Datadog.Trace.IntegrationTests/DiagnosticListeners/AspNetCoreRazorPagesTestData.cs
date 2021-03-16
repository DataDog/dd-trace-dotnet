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
        /// (URL, StatusCode, isError, Resource, ParentSpanTags, Span Count, Child1SpanTags, Child2SpanResourceName, Child2SpanTags)
        /// </summary>
        public static TheoryData<string, int, bool, string, SerializableDictionary, int, string, SerializableDictionary, string, SerializableDictionary> WithFeatureFlag => new()
        {
            { "/", 200, false, "GET /", ConventionalParentTags("Index", route: string.Empty), 2, null, ConventionalChildTags("Index", route: string.Empty), null, null },
            { "/Index", 200, false, "GET /index", ConventionalParentTags("Index"), 2, null, ConventionalChildTags("Index"), null, null },
            { "/Privacy", 200, false, "GET /privacy", ConventionalParentTags("Privacy"), 2, null, ConventionalChildTags("Privacy"), null, null },
            { "/Error", 500, true, "GET /error", ConventionalParentTags("Error"), 2, null, ConventionalChildTags("Error"), null, null },
            { "/Products", 200, false, "GET /products", ConventionalParentTags("Products/Index", route: "Products"), 2, null, ConventionalChildTags("Products/Index", route: "Products"), null, null },
            { "/Products/Index", 200, false, "GET /products/index", ConventionalParentTags("Products/Index"), 2, null, ConventionalChildTags("Products/Index"), null, null },
            { "/Products/Product", 404, false, "GET /products/product", EmptyTags(), 1, null, null, null, null },
            { "/Products/Product/123", 200, false, "GET /products/product/{id}", ConventionalParentTags(page: "Products/Product", route: "Products/Product/{id}"), 2, null, ConventionalChildTags(page: "Products/Product", route: "Products/Product/{id}"), null, null },
            { "/Products/Product/Oops", 400, false, "GET /products/product/{id}", ConventionalParentTags(page: "Products/Product", route: "Products/Product/{id}"), 2, null, ConventionalChildTags(page: "Products/Product", route: "Products/Product/{id}"), null, null },
            { "/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags(), 1, null, null, null, null },
            { "/Error", 500, true, "GET /error", ConventionalParentTags(page: "Error"), 2, null, ConventionalChildTags(page: "Error"), null, null },
            { "/UncaughtError", 500, true, "GET /uncaughterror", ConventionalParentTags(page: "UncaughtError"), 2, null, ConventionalChildTags(page: "UncaughtError"), null, null },
            { "/BadHttpRequest", 400, true, "GET /badhttprequest", ConventionalParentTags(page: "BadHttpRequest"), 2, null, ConventionalChildTags(page: "BadHttpRequest"), null, null },
            { $"{CustomHandlerPrefix}/Error", 500, true, $"GET {CustomHandlerPrefix}/error", ConventionalParentTags(page: "Error"), 2, null, ConventionalChildTags(page: "Error"), null, null },
            { $"{CustomHandlerPrefix}/UncaughtError", 500, true, $"GET {CustomHandlerPrefix}/uncaughterror", ConventionalParentTags(page: "UncaughtError"), 2, null, ConventionalChildTags(page: "UncaughtError"), null, null },
            { $"{CustomHandlerPrefix}/BadHttpRequest", 500, true, $"GET {CustomHandlerPrefix}/badhttprequest", ConventionalParentTags(page: "BadHttpRequest"), 2, null, ConventionalChildTags(page: "BadHttpRequest"), null, null },
            { $"{ExceptionPagePrefix}/Error", 500, true, $"GET {ExceptionPagePrefix}/error", ConventionalParentTags(page: "Error"), 2, null, ConventionalChildTags(page: "Error"), null, null },
            { $"{ExceptionPagePrefix}/BadHttpRequest", 400, true, $"GET {ExceptionPagePrefix}/badhttprequest", ConventionalParentTags(page: "BadHttpRequest"), 2, null, ConventionalChildTags(page: "BadHttpRequest"), null, null },
            { $"{ReExecuteHandlerPrefix}/Error", 500, true, $"GET {ReExecuteHandlerPrefix}/error", ConventionalParentTags(page: "Error"), 3, null, ConventionalChildTags(page: "Error"), $"GET {ReExecuteHandlerPrefix}/", ConventionalChildTags("Index", route: string.Empty) },
            { $"{ReExecuteHandlerPrefix}/BadHttpRequest", 500, true, $"GET {ReExecuteHandlerPrefix}/badhttprequest", ConventionalParentTags(page: "BadHttpRequest"), 3, null, ConventionalChildTags(page: "BadHttpRequest"), $"GET {ReExecuteHandlerPrefix}/", ConventionalChildTags("Index", route: string.Empty) },
            { $"{StatusCodeReExecutePrefix}/I/dont/123/exist/", 404, false, $"GET {StatusCodeReExecutePrefix}/i/dont/?/exist/", EmptyTags(), 2, $"GET {StatusCodeReExecutePrefix}/", ConventionalChildTags("Index", route: string.Empty), null, null },
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

        private static SerializableDictionary ConventionalParentTags(string page, string route = null) => new()
        {
            { Tags.AspNetCoreRoute, (route ?? page).ToLowerInvariant() },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreArea, null },
#if NETCOREAPP && !NETCOREAPP2_1 && !NETCOREAPP3_0
            { Tags.AspNetCoreEndpoint, $"/{page}" },
#endif
        };

        private static SerializableDictionary ConventionalChildTags(string page, string route = null) => new()
        {
            { Tags.AspNetCoreRoute, (route ?? page).ToLowerInvariant() },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCorePage, $"/{page.ToLowerInvariant()}" },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCoreEndpoint, null },
        };
    }
}
#endif
