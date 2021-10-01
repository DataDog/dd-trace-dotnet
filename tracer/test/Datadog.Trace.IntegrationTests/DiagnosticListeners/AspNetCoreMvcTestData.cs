// <copyright file="AspNetCoreMvcTestData.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System.Collections.Generic;
using NUnit.Framework;
using static Datadog.Trace.IntegrationTests.DiagnosticListeners.ErrorHandlingHelper;

namespace Datadog.Trace.IntegrationTests.DiagnosticListeners
{
    public static class AspNetCoreMvcTestData
    {
        /// <summary>
        /// Gets data for MVC tests with the feature flags disabled
        /// (URL, isError, Resource, Tags)
        /// </summary>
        public static IEnumerable<TestCaseData> WithoutFeatureFlag => new TestCaseData[]
        {
            new("/", 200, false, "GET Home/Index", EmptyTags()),
            new("/Home", 200, false, "GET Home/Index", EmptyTags()),
            new("/Home/Index", 200, false, "GET Home/Index", EmptyTags()),
            new("/MyTest", 200, false, "GET MyTest/Index", EmptyTags()),
            new("/MyTest/index", 200, false, "GET MyTest/Index", EmptyTags()),
            new("/Api/index", 200, false, "GET api/Index", EmptyTags()),
            new("/Api/Value/3", 200, false, "GET api/Value/{value}", EmptyTags()),
            new("/Api/Value/100", 400, false, "GET api/Value/{value}", EmptyTags()),
            new("/statuscode/401", 401, false, "GET statuscode/{value=200}", EmptyTags()),
            new("/statuscode/200", 200, false, "GET statuscode/{value=200}", EmptyTags()),
            new("/statuscode/201", 201, false, "GET statuscode/{value=200}", EmptyTags()),
            new("/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags()),
            new("/Home/Error", 500, true, "GET Home/Error", EmptyTags()),
            new("/Home/UncaughtError", 500, true, "GET Home/UncaughtError", EmptyTags()),
            new("/Home/BadHttpRequest", 400, true, "GET Home/BadHttpRequest", EmptyTags()),
            new($"{CustomHandlerPrefix}/Home/Error", 500, true, "GET Home/Error", EmptyTags()),
            new($"{CustomHandlerPrefix}/Home/UncaughtError", 500, true, "GET Home/UncaughtError", EmptyTags()),
            new($"{CustomHandlerPrefix}/Home/BadHttpRequest", 500, true, "GET Home/BadHttpRequest", EmptyTags()),
            new($"{CustomHandlerPrefix}/throws", 500, true, $"GET {CustomHandlerPrefix}/throws", EmptyTags()),
            new($"{ExceptionPagePrefix}/Home/Error", 500, true, "GET Home/Error", EmptyTags()),
            new($"{ExceptionPagePrefix}/Home/BadHttpRequest", 400, true, "GET Home/BadHttpRequest", EmptyTags()),
            new($"{ExceptionPagePrefix}/throws", 500, true, $"GET {ExceptionPagePrefix}/throws", EmptyTags()),
            // The below is the ideal behaviour, but we can't achieve that currently
            // { $"{ReExecuteHandlerPrefix}/Home/Error", 500, true, "GET Home/Error", EmptyTags() },
            // { $"{ReExecuteHandlerPrefix}/Home/BadHttpRequest", 500, true, "GET Home/BadHttpRequest", EmptyTags() },
            // { $"{ReExecuteHandlerPrefix}/throws", 500, true, $"GET {ReExecuteHandlerPrefix}/throws", EmptyTags() },
        };

        /// <summary>
        /// Gets data for MVC tests with the feature flags enabled
        /// (URL, StatusCode, isError, Resource, ParentSpanTags, Span Count, Child1SpanResourceName, Child1SpanTags, Child2SpanResourceName, Child2SpanTags)
        /// </summary>
        public static IEnumerable<TestCaseData> WithFeatureFlag => new TestCaseData[]
        {
            new("/", 200, false, "GET /home/index", ConventionalParentTags(), 2, null, ConventionalChildTags(), null, null),
            new("/Home", 200, false, "GET /home/index", ConventionalParentTags(), 2, null, ConventionalChildTags(), null, null),
            new("/Home/Index", 200, false, "GET /home/index", ConventionalParentTags(), 2, null, ConventionalChildTags(), null, null),
            new("/Api/index", 200, false, "GET /api/index", ApiIndexParentTags(), 2, null, ApiIndexChildTags(), null, null),
            new("/Api/Value/3", 200, false, "GET /api/value/{value}", ApiValueParentTags(), 2, null, ApiValueChildTags(), null, null),
            new("/Api/Value/100", 400, false, "GET /api/value/{value}", ApiValueParentTags(), 2, null, ApiValueChildTags(), null, null),
            new("/MyTest", 200, false, "GET /mytest/index", ConventionalParentTags(), 2, null, ConventionalChildTags(controller: "mytest"), null, null),
            new("/MyTest/index", 200, false, "GET /mytest/index", ConventionalParentTags(), 2, null, ConventionalChildTags(controller: "mytest"), null, null),
            new("/statuscode", 200, false, "GET /statuscode/{value}", StatusCodeParentTags(), 2, null, StatusCodeChildTags(), null, null),
            new("/statuscode/401", 401, false, "GET /statuscode/{value}", StatusCodeParentTags(), 2, null, StatusCodeChildTags(), null, null),
            new("/statuscode/404", 404, false, "GET /statuscode/{value}", StatusCodeParentTags(), 2, null, StatusCodeChildTags(), null, null),
            new("/statuscode/200", 200, false, "GET /statuscode/{value}", StatusCodeParentTags(), 2, null, StatusCodeChildTags(), null, null),
            new("/statuscode/201", 201, false, "GET /statuscode/{value}", StatusCodeParentTags(), 2, null, StatusCodeChildTags(), null, null),
            new("/I/dont/123/exist/", 404, false, "GET /i/dont/?/exist/", EmptyTags(), 1, null, null, null, null),
            new("/Home/Error", 500, true, "GET /home/error", ConventionalParentTags(), 2, null, ConventionalChildTags(action: "error"), null, null),
            new("/Home/UncaughtError", 500, true, "GET /home/uncaughterror", ConventionalParentTags(), 2, null, ConventionalChildTags(action: "uncaughterror"), null, null),
            new("/Home/BadHttpRequest", 400, true, "GET /home/badhttprequest", ConventionalParentTags(), 2, null, ConventionalChildTags(action: "badhttprequest"), null, null),
            new($"{CustomHandlerPrefix}/Home/Error", 500, true, $"GET {CustomHandlerPrefix}/home/error", ConventionalParentTags(), 2, null, ConventionalChildTags(action: "error"), null, null),
            new($"{CustomHandlerPrefix}/Home/UncaughtError", 500, true, $"GET {CustomHandlerPrefix}/home/uncaughterror", ConventionalParentTags(), 2, null, ConventionalChildTags(action: "uncaughterror"), null, null),
            new($"{CustomHandlerPrefix}/Home/BadHttpRequest", 500, true, $"GET {CustomHandlerPrefix}/home/badhttprequest", ConventionalParentTags(), 2, null, ConventionalChildTags(action: "badhttprequest"), null, null),
            new($"{CustomHandlerPrefix}/throws", 500, true, $"GET {CustomHandlerPrefix}/throws", EmptyTags(), 1, null, null, null, null),
            new($"{ExceptionPagePrefix}/Home/Error", 500, true, $"GET {ExceptionPagePrefix}/home/error", ConventionalParentTags(), 2, null, ConventionalChildTags(action: "error"), null, null),
            new($"{ExceptionPagePrefix}/Home/BadHttpRequest", 400, true, $"GET {ExceptionPagePrefix}/home/badhttprequest", ConventionalParentTags(), 2, null, ConventionalChildTags(action: "badhttprequest"), null, null),
            new($"{ExceptionPagePrefix}/throws", 500, true, $"GET {ExceptionPagePrefix}/throws", EmptyTags(), 1, null, null, null, null),
            new($"{ReExecuteHandlerPrefix}/Home/Error", 500, true, $"GET {ReExecuteHandlerPrefix}/home/error", ConventionalParentTags(), 3, null, ConventionalChildTags(action: "error"), $"GET {ReExecuteHandlerPrefix}/home/index", ConventionalChildTags()),
            new($"{ReExecuteHandlerPrefix}/Home/BadHttpRequest", 500, true, $"GET {ReExecuteHandlerPrefix}/home/badhttprequest", ConventionalParentTags(), 3, null, ConventionalChildTags(action: "badhttprequest"), $"GET {ReExecuteHandlerPrefix}/home/index", ConventionalChildTags()),
            new($"{ReExecuteHandlerPrefix}/throws", 500, true, $"GET {ReExecuteHandlerPrefix}/throws", EmptyTags(), 2, $"GET {ReExecuteHandlerPrefix}/home/index", ConventionalChildTags(), null, null),
            new($"{StatusCodeReExecutePrefix}/I/dont/123/exist/", 404, false, $"GET {StatusCodeReExecutePrefix}/i/dont/?/exist/", EmptyTags(), 2, $"GET {StatusCodeReExecutePrefix}/home/index", ConventionalChildTags(), null, null),
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

        private static IReadOnlyDictionary<string, string> ConventionalParentTags(
            string action = "index",
            string controller = "home") => new Dictionary<string, string>
        {
            { Tags.AspNetCoreRoute, "{controller=home}/{action=index}/{id?}" },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static IReadOnlyDictionary<string, string> ConventionalChildTags(
            string action = "index",
            string controller = "home") => new Dictionary<string, string>
        {
            { Tags.AspNetCoreRoute, "{controller=home}/{action=index}/{id?}" },
            { Tags.AspNetCoreController, controller },
            { Tags.AspNetCoreAction, action },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static IReadOnlyDictionary<string, string> StatusCodeParentTags() => new Dictionary<string, string>
        {
            { Tags.AspNetCoreRoute, "statuscode/{value=200}" },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static IReadOnlyDictionary<string, string> StatusCodeChildTags() => new Dictionary<string, string>
        {
            { Tags.AspNetCoreRoute, "statuscode/{value=200}" },
            { Tags.AspNetCoreController, "mytest" },
            { Tags.AspNetCoreAction, "setstatuscode" },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static IReadOnlyDictionary<string, string> ApiIndexParentTags() => new Dictionary<string, string>
        {
            { Tags.AspNetCoreRoute, "api/index" },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static IReadOnlyDictionary<string, string> ApiIndexChildTags() => new Dictionary<string, string>
        {
            { Tags.AspNetCoreRoute, "api/index" },
            { Tags.AspNetCoreController, "api" },
            { Tags.AspNetCoreAction, "index" },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static IReadOnlyDictionary<string, string> ApiValueParentTags() => new Dictionary<string, string>
        {
            { Tags.AspNetCoreRoute, "api/value/{value}" },
            { Tags.AspNetCoreController, null },
            { Tags.AspNetCoreAction, null },
            { Tags.AspNetCoreArea, null },
            { Tags.AspNetCorePage, null },
            { Tags.AspNetCoreEndpoint, null },
        };

        private static IReadOnlyDictionary<string, string> ApiValueChildTags() => new Dictionary<string, string>
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
