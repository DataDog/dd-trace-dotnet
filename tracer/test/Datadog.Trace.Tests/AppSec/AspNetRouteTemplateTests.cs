// <copyright file="AspNetRouteTemplateTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec.ApiSec;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.AppSec;

/// <summary>
/// Tests the normalized route of ASP.NET (System.Web) route templates, as used by MVC 5 and Web API 2
/// </summary>
public class AspNetRouteTemplateTests
{
    /// <summary>
    /// Gets the route template, the route values matched for the request ("name=value&amp;..."), the request path
    /// relative to the application path, and the expected normalized route
    /// </summary>
    public static TheoryData<string, string, string, string> Routes { get; } = new()
    {
        // Root and static routes
        { string.Empty, string.Empty, "/", "/" },
        { "api/values", string.Empty, "/api/values", "/api/values" },
        { "~/api/values", string.Empty, "/api/values", "/api/values" },
        { "/api/values", string.Empty, "/api/values", "/api/values" },
        { "Api/Values", string.Empty, "/Api/Values", "/Api/Values" },
        { "api/values/", string.Empty, "/api/values/", "/api/values/" },

        // The area, controller and action route values are parameters like any other (rule 4)
        { "{controller}/{action}/{id}", "controller=Home&action=Index&id=5", "/Home/Index/5", "/{controller}/{action}/{id}" },
        { "api/{controller}/{id}", "controller=Values&id=5", "/api/Values/5", "/api/{controller}/{id}" },
        { "Admin/{controller}/{action}", "controller=Users&action=List", "/Admin/Users/List", "/Admin/{controller}/{action}" },

        // MVC and Web API remove the optional parameters that the request doesn't contain
        { "{controller}/{action}/{id}", "controller=Home&action=Index", "/Home/Index", "/{controller}/{action}" },
        { "api/users/{id?}", string.Empty, "/api/users", "/api/users" },
        { "api/users/{id?}", "id=1", "/api/users/1", "/api/users/{id}" },

        // A value that only comes from the default is absent: its segment isn't in the request (RFC-1125)
        { "{controller}/{action}/{id}", "controller=Home&action=Index", "/", "/" },
        { "{controller}/{action}/{id}", "controller=Home&action=Index", "/Home", "/{controller}" },
        { "api/page/{page=1}", "page=1", "/api/page", "/api/page" },
        { "api/page/{page=1}", "page=1", "/api/page/1", "/api/page/{page}" },

        // Constraints and defaults are dropped from the parameter name
        { "api/users/{id:int}", "id=1", "/api/users/1", "/api/users/{id}" },
        { "api/users/{id:int:min(1)}", "id=1", "/api/users/1", "/api/users/{id}" },

        // The static text of a segment and each of its parameters are kept (RFC-1125)
        { "users/user-{id}", "id=1", "/users/user-1", "/users/user-{id}" },
        { "users/admin-{id}", "id=1", "/users/admin-1", "/users/admin-{id}" },
        { "photos/{id}.{format}", "id=1&format=json", "/photos/1.json", "/photos/{id}.{format}" },
        { "{controller}-{action}", "controller=Home&action=Index", "/Home-Index", "/{controller}-{action}" },

        // Parameter names only contain [a-zA-Z0-9_], other characters are replaced with "_", with a suffix on collision (RFC-1125)
        { "users/{user-id}", "user-id=1", "/users/1", "/users/{user_id}" },
        { "a/{user-id}/{user_id}", "user-id=1&user_id=2", "/a/1/2", "/a/{user_id_2}/{user_id}" },

        // Catch-all parameters, absent when empty, keeping the slash that precedes them in the request (RFC-1125)
        { "files/{*path}", "path=a/b", "/files/a/b", "/files/{path}" },
        { "files/{*path}", string.Empty, "/files", "/files" },
        { "files/{*path}", string.Empty, "/files/", "/files/" },

        // Static constants are percent-encoded outside [A-Za-z0-9.-~_], "{{" and "}}" are escaped braces
        { "path with space", string.Empty, "/path with space", "/path%20with%20space" },
        { "a{{b}}c", string.Empty, "/a{b}c", "/a%7Bb%7Dc" },
        { "café", string.Empty, "/café", "/caf%C3%A9" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public void AddRouteTemplate_NormalizesRouteTemplate(string template, string routeValues, string requestPath, string expected)
    {
        Normalize(template, routeValues, requestPath).Should().Be(expected);
    }

    [Theory]
    [InlineData("api/{id}", "/MyApp", "/MyApp/api/{id}")]
    [InlineData("api/{id}", "/My App/", "/My%20App/api/{id}")]
    [InlineData("api/{id}", "/", "/api/{id}")]
    [InlineData("", "/MyApp", "/MyApp")]
    public void AddRouteTemplate_IncludesApplicationPath(string template, string applicationPath, string expected)
    {
        Normalize(template, "id=1", "/api/1", applicationPath).Should().Be(expected);
    }

    [Fact]
    public void AddRouteTemplate_LeavesOutParametersWhenTheRequestPathIsUnknown()
    {
        // Presence can't be determined, so the RFC-1125 fallback leaves the parameters out
        Normalize("api/{controller}/{id}", "controller=Values&id=5", requestPath: null).Should().Be("/api");
    }

    [Theory]
    [InlineData("api/{id")]
    [InlineData("api/id}")]
    public void AddRouteTemplate_RejectsInvalidTemplates(string template)
    {
        Normalize(template, "id=1", "/api/1").Should().BeNull();
    }

    [Theory]
    [InlineData("~/home/index", "", "/home/index")]
    [InlineData("~/home/index", "/extra", "/home/index/extra")]
    [InlineData("~/", "", "/")]
    [InlineData(null, "", null)]
    public void GetMvcRequestPath_RemovesTheAppRelativePrefix(string appRelativePath, string pathInfo, string expected)
    {
        AspNetRouteTemplate.GetMvcRequestPath(appRelativePath, pathInfo).Should().Be(expected);
    }

    [Theory]
    [InlineData("/api/values/5", "/", "/api/values/5")]
    [InlineData("/MyApp/api/values/5", "/MyApp", "/api/values/5")]
    [InlineData("/MyApp/api/values/5", "/myapp/", "/api/values/5")]
    [InlineData("/api/values", null, "/api/values")]
    public void GetWebApiRequestPath_RemovesTheVirtualPathRoot(string absolutePath, string virtualPathRoot, string expected)
    {
        AspNetRouteTemplate.GetWebApiRequestPath(absolutePath, virtualPathRoot).Should().Be(expected);
    }

    // Mirrors AspNetRouteTemplate.SetTag, which doesn't emit the tag for an invalid template
    private static string Normalize(string template, string routeValues, string requestPath, string applicationPath = null)
    {
        var normalizedRoute = NormalizedRouteBuilder.Create(applicationPath, requestPath);
        var isValid = AspNetRouteTemplate.AddRouteTemplate(ref normalizedRoute, template, ParseRouteValues(routeValues));
        return normalizedRoute.Complete(isValid ? template : null);
    }

    private static Dictionary<string, object> ParseRouteValues(string routeValues)
    {
        // The route value dictionaries of MVC and Web API are case-insensitive
        var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in routeValues.Split(Separators.Ampersand, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            values[pair.Substring(0, separator)] = pair.Substring(separator + 1);
        }

        return values;
    }
}
