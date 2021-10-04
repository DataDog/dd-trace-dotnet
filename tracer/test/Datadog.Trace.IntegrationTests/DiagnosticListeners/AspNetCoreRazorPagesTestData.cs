// <copyright file="AspNetCoreRazorPagesTestData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System.Collections.Generic;
using NUnit.Framework;
using static Datadog.Trace.IntegrationTests.DiagnosticListeners.ErrorHandlingHelper;

namespace Datadog.Trace.IntegrationTests.DiagnosticListeners
{
    public static class AspNetCoreRazorPagesTestData
    {
        /// <summary>
        /// Gets data for MVC tests with the feature flags disabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static IEnumerable<TestCaseData> WithoutFeatureFlag => new TestCaseData[]
        {
            new("/", 200, 200, false, "GET ", EmptyTags()),
            new("/Index", 200, 200, false, "GET Index", EmptyTags()),
            new("/Privacy", 200, 200, false, "GET Privacy", EmptyTags()),
            new("/Products", 200, 200, false, "GET Products", EmptyTags()),
            new("/Products/Index", 200, 200, false, "GET Products/Index", EmptyTags()),
            new("/Products/Product", 404, 404, false, "GET /products/product", EmptyTags()),
            new("/Products/Product/123", 200, 200, false, "GET Products/Product/{id}", EmptyTags()),
            new("/Products/Product/Oops", 400, 400, false, "GET Products/Product/{id}", EmptyTags()),
            new("/I/dont/123/exist/", 404, 404, false, "GET /i/dont/?/exist/", EmptyTags()),
            new("/Error", 500, 500, true, "GET Error", EmptyTags()),
            new("/UncaughtError", 500, 500, true, "GET UncaughtError", EmptyTags()),
            new("/BadHttpRequest", 400, 400, true, "GET BadHttpRequest", EmptyTags()),
            new($"{CustomHandlerPrefix}/Error", 500, 500, true, "GET Error", EmptyTags()),
            new($"{CustomHandlerPrefix}/UncaughtError", 500, 500, true, "GET UncaughtError", EmptyTags()),
            new($"{CustomHandlerPrefix}/BadHttpRequest", 500, 500, true, "GET BadHttpRequest", EmptyTags()),
            new($"{ExceptionPagePrefix}/Error", 500, 500, true, "GET Error", EmptyTags()),
            new($"{ExceptionPagePrefix}/BadHttpRequest", 500, 400, true, "GET BadHttpRequest", EmptyTags()),
            // The below is the ideal behaviour, but we can't achieve that currently
            // { $"{ReExecuteHandlerPrefix}/Error", 500, true, "GET Error", EmptyTags() },
            // { $"{ReExecuteHandlerPrefix}/BadHttpRequest", 500, true, "GET BadHttpRequest", EmptyTags() },
        };

        /// <summary>
        /// Gets data for MVC tests with the feature flags enabled
        /// (URL, StatusCode, isError, Resource, ParentSpanTags, Span Count, Child1SpanTags, Child2SpanResourceName, Child2SpanTags)
        /// </summary>
        public static IEnumerable<TestCaseData> WithFeatureFlag => new TestCaseData[]
        {
            new("/", 200, 200, false, "GET /", ConventionalParentTags("Index", route: string.Empty), 2, null, ConventionalChildTags("Index", route: string.Empty), null, null),
            new("/Index", 200, 200, false, "GET /index", ConventionalParentTags("Index"), 2, null, ConventionalChildTags("Index"), null, null),
            new("/Privacy", 200, 200, false, "GET /privacy", ConventionalParentTags("Privacy"), 2, null, ConventionalChildTags("Privacy"), null, null),
            new("/Error", 500, 500, true, "GET /error", ConventionalParentTags("Error"), 2, null, ConventionalChildTags("Error"), null, null),
            new("/Products", 200, 200, false, "GET /products", ConventionalParentTags("Products/Index", route: "Products"), 2, null, ConventionalChildTags("Products/Index", route: "Products"), null, null),
            new("/Products/Index", 200, 200, false, "GET /products/index", ConventionalParentTags("Products/Index"), 2, null, ConventionalChildTags("Products/Index"), null, null),
            new("/Products/Product", 404, 404, false, "GET /products/product", EmptyTags(), 1, null, null, null, null),
            new("/Products/Product/123", 200, 200, false, "GET /products/product/{id}", ConventionalParentTags(page: "Products/Product", route: "Products/Product/{id}"), 2, null, ConventionalChildTags(page: "Products/Product", route: "Products/Product/{id}"), null, null),
            new("/Products/Product/Oops", 400, 400, false, "GET /products/product/{id}", ConventionalParentTags(page: "Products/Product", route: "Products/Product/{id}"), 2, null, ConventionalChildTags(page: "Products/Product", route: "Products/Product/{id}"), null, null),
            new("/I/dont/123/exist/", 404, 404, false, "GET /i/dont/?/exist/", EmptyTags(), 1, null, null, null, null),
            new("/Error", 500, 500, true, "GET /error", ConventionalParentTags(page: "Error"), 2, null, ConventionalChildTags(page: "Error"), null, null),
            new("/UncaughtError", 500, 500, true, "GET /uncaughterror", ConventionalParentTags(page: "UncaughtError"), 2, null, ConventionalChildTags(page: "UncaughtError"), null, null),
            new("/BadHttpRequest", 400, 400, true, "GET /badhttprequest", ConventionalParentTags(page: "BadHttpRequest"), 2, null, ConventionalChildTags(page: "BadHttpRequest"), null, null),
            new($"{CustomHandlerPrefix}/Error", 500, 500, true, $"GET {CustomHandlerPrefix}/error", ConventionalParentTags(page: "Error"), 2, null, ConventionalChildTags(page: "Error"), null, null),
            new($"{CustomHandlerPrefix}/UncaughtError", 500, 500, true, $"GET {CustomHandlerPrefix}/uncaughterror", ConventionalParentTags(page: "UncaughtError"), 2, null, ConventionalChildTags(page: "UncaughtError"), null, null),
            new($"{CustomHandlerPrefix}/BadHttpRequest", 500, 500, true, $"GET {CustomHandlerPrefix}/badhttprequest", ConventionalParentTags(page: "BadHttpRequest"), 2, null, ConventionalChildTags(page: "BadHttpRequest"), null, null),
            new($"{ExceptionPagePrefix}/Error", 500, 500, true, $"GET {ExceptionPagePrefix}/error", ConventionalParentTags(page: "Error"), 2, null, ConventionalChildTags(page: "Error"), null, null),
            new($"{ExceptionPagePrefix}/BadHttpRequest", 500, 400, true, $"GET {ExceptionPagePrefix}/badhttprequest", ConventionalParentTags(page: "BadHttpRequest"), 2, null, ConventionalChildTags(page: "BadHttpRequest"), null, null),
            new($"{ReExecuteHandlerPrefix}/Error", 500, 500, true, $"GET {ReExecuteHandlerPrefix}/error", ConventionalParentTags(page: "Error"), 3, null, ConventionalChildTags(page: "Error"), $"GET {ReExecuteHandlerPrefix}/", ConventionalChildTags("Index", route: string.Empty)),
            new($"{ReExecuteHandlerPrefix}/BadHttpRequest", 500, 500, true, $"GET {ReExecuteHandlerPrefix}/badhttprequest", ConventionalParentTags(page: "BadHttpRequest"), 3, null, ConventionalChildTags(page: "BadHttpRequest"), $"GET {ReExecuteHandlerPrefix}/", ConventionalChildTags("Index", route: string.Empty)),
            new($"{StatusCodeReExecutePrefix}/I/dont/123/exist/", 404, 404, false, $"GET {StatusCodeReExecutePrefix}/i/dont/?/exist/", EmptyTags(), 2, $"GET {StatusCodeReExecutePrefix}/", ConventionalChildTags("Index", route: string.Empty), null, null),
        };

        private static IReadOnlyDictionary<string, string> EmptyTags() => new Dictionary<string, string>
        {
            { Tags.AspNetCoreRoute, null },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static IReadOnlyDictionary<string, string> ConventionalParentTags(string page, string route = null) => new Dictionary<string, string>
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

        private static IReadOnlyDictionary<string, string> ConventionalChildTags(string page, string route = null) => new Dictionary<string, string>
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
