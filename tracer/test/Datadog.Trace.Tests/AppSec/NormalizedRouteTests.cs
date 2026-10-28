// <copyright file="NormalizedRouteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using Datadog.Trace.AppSec.ApiSec;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;

namespace Datadog.Trace.Tests.AppSec;

public class NormalizedRouteTests
{
    /// <summary>
    /// Gets the route template, the route values matched for the request ("name=value&amp;..."), and the expected normalized route
    /// </summary>
    public static TheoryData<string, string, string> Routes { get; } = new()
    {
        // Root and static routes
        { string.Empty, string.Empty, "/" },
        { "/", string.Empty, "/" },
        { "waf", string.Empty, "/waf" },
        { "/api/v1/users", string.Empty, "/api/v1/users" },

        // One parameter per segment, constraints are dropped
        { "/sample_rate_route/{i:int}", "i=0", "/sample_rate_route/{i}" },
        { "tag_value/{tag_value}/{status_code}", "tag_value=foo&status_code=200", "/tag_value/{tag_value}/{status_code}" },
        { "v2/{name:regex(^[a-z]+$)}/blobs", "name=foo", "/v2/{name}/blobs" },
        { "items/{page=1}", "page=1", "/items/{page}" },

        // Trailing slash is kept only when declared
        { "api/{v}/", "v=1", "/api/{v}/" },

        // Static text in a parameter segment is folded into the parameter
        { "users/user-{id}", "id=1", "/users/{id}" },
        { "users/u-{id}-x", "id=1", "/users/{id}" },

        // Several parameters in a segment are combined
        { "photos/{id}.{format}", "id=1&format=json", "/photos/{id+format}" },
        { "files/{a}.{b}.{c}", "a=1&b=2&c=3", "/files/{a+b+c}" },

        // Catch-all parameters
        { "files/{*path}", "path=a/b", "/files/{path}" },
        { "files/{**path}", "path=a/b", "/files/{path}" },
        { "files/{*path}", string.Empty, "/files" },

        // Optional parameters are resolved against the matched values
        { "users/{id?}", "id=1", "/users/{id}" },
        { "users/{id?}", string.Empty, "/users" },
        { "posts/{id}.{format?}", "id=1&format=json", "/posts/{id+format}" },
        { "posts/{id}.{format?}", "id=1", "/posts/{id}" },

        // MVC route values identify the endpoint, so they are static constants
        { "{controller=Home}/{action=Index}/{id?}", "controller=Home&action=Index", "/Home/Index" },
        { "{controller=Home}/{action=Index}/{id?}", "controller=Home&action=Index&id=5", "/Home/Index/{id}" },
        { "{area:exists}/{controller}/{action}", "area=Admin&controller=Home&action=Index", "/Admin/Home/Index" },
        { "prefix-{controller}-{id}", "controller=Home&id=1", "/{id}" },

        // Static constants are case sensitive and percent-encoded outside [A-Za-z0-9.-~_]
        { "Api/Users", string.Empty, "/Api/Users" },
        { "path with space", string.Empty, "/path%20with%20space" },
        { "café", string.Empty, "/caf%C3%A9" },
        { "a~b_c.d-e", string.Empty, "/a~b_c.d-e" },
        { "{controller}/{action}", "controller=My Home&action=Index", "/My%20Home/Index" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public void Get_NormalizesRoute(string template, string routeValues, string expected)
    {
        var routePattern = RoutePatternFactory.Parse(template).DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();

        NormalizedRoute.Get(routePattern, ParseRouteValues(routeValues)).Should().Be(expected);
    }

    private static RouteValueDictionary ParseRouteValues(string routeValues)
    {
        var values = new RouteValueDictionary();
        foreach (var pair in routeValues.Split(new[] { '&' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            values[pair.Substring(0, separator)] = pair.Substring(separator + 1);
        }

        return values;
    }
}
#endif
