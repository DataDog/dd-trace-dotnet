using Datadog.Trace.TestHelpers;
using Xunit;

namespace Datadog.Trace.Tests.DiagnosticListeners
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
            { "/Error", 500, true, "GET Error", EmptyTags() },
            { "/Products", 200, false, "GET Products", EmptyTags() },
            { "/Products/Index", 200, false, "GET Products/Index", EmptyTags() },
            { "/Products/Product", 404, false, "GET /products/product", EmptyTags() },
            { "/Products/Product/123", 200, false, "GET Products/Product/{id}", EmptyTags() },
            { "/Products/Product/Oops", 400, false, "GET Products/Product/{id}", EmptyTags() },
            { "/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags() },
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
