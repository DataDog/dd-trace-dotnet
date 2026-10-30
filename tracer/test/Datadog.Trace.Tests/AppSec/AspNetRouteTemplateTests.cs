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
    /// Gets the route template, the route values matched for the request ("name=value&amp;..."), and the expected normalized route
    /// </summary>
    public static TheoryData<string, string, string> Routes { get; } = new()
    {
        // Root and static routes
        { string.Empty, string.Empty, "/" },
        { "api/values", string.Empty, "/api/values" },
        { "~/api/values", string.Empty, "/api/values" },
        { "/api/values", string.Empty, "/api/values" },
        { "Api/Values", string.Empty, "/Api/Values" },
        { "api/values/", string.Empty, "/api/values/" },

        // The area, controller and action route values are parameters like any other (rule 4)
        { "{controller}/{action}/{id}", "controller=Home&action=Index&id=5", "/{controller}/{action}/{id}" },
        { "api/{controller}/{id}", "controller=Values&id=5", "/api/{controller}/{id}" },
        { "Admin/{controller}/{action}", "controller=Users&action=List", "/Admin/{controller}/{action}" },

        // MVC and Web API remove the optional parameters that the request doesn't contain
        { "{controller}/{action}/{id}", "controller=Home&action=Index", "/{controller}/{action}" },
        { "api/users/{id?}", string.Empty, "/api/users" },
        { "api/users/{id?}", "id=1", "/api/users/{id}" },

        // Constraints and defaults are dropped from the parameter name
        { "api/users/{id:int}", "id=1", "/api/users/{id}" },
        { "api/users/{id:int:min(1)}", "id=1", "/api/users/{id}" },
        { "api/page/{page=1}", "page=1", "/api/page/{page}" },

        // Static text in a parameter segment is folded into the parameter, several parameters are combined
        { "users/user-{id}", "id=1", "/users/{id}" },
        { "photos/{id}.{format}", "id=1&format=json", "/photos/{id+format}" },

        // Catch-all parameters
        { "files/{*path}", "path=a/b", "/files/{path}" },
        { "files/{*path}", string.Empty, "/files" },

        // Static constants are percent-encoded outside [A-Za-z0-9.-~_], "{{" and "}}" are escaped braces
        { "path with space", string.Empty, "/path%20with%20space" },
        { "a{{b}}c", string.Empty, "/a%7Bb%7Dc" },
        { "café", string.Empty, "/caf%C3%A9" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public void GetNormalizedRoute_NormalizesRouteTemplate(string template, string routeValues, string expected)
    {
        AspNetRouteTemplate.GetNormalizedRoute(template, ParseRouteValues(routeValues)).Should().Be(expected);
    }

    [Theory]
    [InlineData("api/{id}", "/MyApp", "/MyApp/api/{id}")]
    [InlineData("api/{id}", "/My App/", "/My%20App/api/{id}")]
    [InlineData("api/{id}", "/", "/api/{id}")]
    [InlineData("", "/MyApp", "/MyApp")]
    public void GetNormalizedRoute_IncludesApplicationPath(string template, string applicationPath, string expected)
    {
        AspNetRouteTemplate.GetNormalizedRoute(template, ParseRouteValues("id=1"), applicationPath).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("api/{id")]
    [InlineData("api/id}")]
    public void GetNormalizedRoute_ReturnsNullForInvalidTemplates(string template)
    {
        AspNetRouteTemplate.GetNormalizedRoute(template, ParseRouteValues("id=1")).Should().BeNull();
    }

    [Fact]
    public void GetNormalizedRoute_ReturnsNullWithoutRouteValues()
    {
        AspNetRouteTemplate.GetNormalizedRoute("api/{id}", routeValues: null).Should().BeNull();
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
