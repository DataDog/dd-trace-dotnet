// <copyright file="NormalizedRouteTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Linq;
using Datadog.Trace.AppSec.ApiSec;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using Datadog.Trace.Util;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
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

        // Trailing slash is kept only when declared
        { "api/{v}/", "v=1", "/api/{v}/" },

        // Static text in a parameter segment is kept (RFC-1125)
        { "users/user-{id}", "id=1", "/users/user-{id}" },
        { "users/u-{id}-x", "id=1", "/users/u-{id}-x" },
        { "users/admin-{id}", "id=1", "/users/admin-{id}" },

        // Several parameters in a segment are kept, with the text between them (RFC-1125)
        { "photos/{id}.{format}", "id=1&format=json", "/photos/{id}.{format}" },
        { "files/{a}.{b}.{c}", "a=1&b=2&c=3", "/files/{a}.{b}.{c}" },

        // Parameter names only contain [a-zA-Z0-9_], other characters are replaced with "_" (RFC-1125)
        { "users/{user_id}", "user_id=1", "/users/{user_id}" },
        { "users/{user-id}", "user-id=1", "/users/{user_id}" },
        { "users/{café}", "café=1", "/users/{caf_}" },
        { "u/{f\U0001F600o}", "f\U0001F600o=1", "/u/{f_o}" },

        // A replaced name that collides with another name gets the smallest free suffix, in path order (RFC-1125)
        { "a/{user-id}/{user_id}", "user-id=1&user_id=2", "/a/{user_id_2}/{user_id}" },
        { "a/{x-y}/{x_y}/{x_y_2}", "x-y=1&x_y=2&x_y_2=3", "/a/{x_y_3}/{x_y}/{x_y_2}" },
        { "a/{x-y}/{x.y}", "x-y=1&x.y=2", "/a/{x_y}/{x_y_2}" },

        // Catch-all parameters, absent when empty or only "/" (RFC-1125)
        { "files/{*path}", "path=a/b", "/files/{path}" },
        { "files/{**path}", "path=a/b", "/files/{path}" },
        { "files/{*path}", string.Empty, "/files" },
        { "files/{*path}", "path=/", "/files" },

        // Optional parameters are resolved against the matched values, with the separator that precedes them
        { "users/{id?}", "id=1", "/users/{id}" },
        { "users/{id?}", string.Empty, "/users" },
        { "posts/{id}.{format?}", "id=1&format=json", "/posts/{id}.{format}" },
        { "posts/{id}.{format?}", "id=1", "/posts/{id}" },
        { "{controller}.{format?}", "controller=Home", "/{controller}" },
        { "{controller}.{format?}", "controller=Home&format=json", "/{controller}.{format}" },

        // The area, controller and action route values are parameters like any other (rule 4)
        { "{area:exists}/{controller}/{action}", "area=Admin&controller=Home&action=Index", "/{area}/{controller}/{action}" },
        { "prefix-{controller}-{id}", "controller=Home&id=1", "/prefix-{controller}-{id}" },
        { "{id}-{controller}", "controller=Home&id=1", "/{id}-{controller}" },
        { "slug/{controller:slugify}", "controller=TestHome", "/slug/{controller}" },

        // Static constants are case sensitive and percent-encoded outside [A-Za-z0-9.-~_]
        { "Api/Users", string.Empty, "/Api/Users" },
        { "path with space", string.Empty, "/path%20with%20space" },
        { "café", string.Empty, "/caf%C3%A9" },
        { "a~b_c.d-e", string.Empty, "/a~b_c.d-e" },
        { "100%", string.Empty, "/100%25" },
    };

    /// <summary>
    /// Gets the <see cref="Routes"/> that <see cref="TemplateParser"/> accepts in every ASP.NET Core version
    /// </summary>
    public static TheoryData<string, string, string> TemplateRoutes { get; } = GetTemplateRoutes();

    /// <summary>
    /// Gets the route template, the route values matched for the request, the request path, and the expected normalized
    /// route, for the parameters whose presence depends on the request path: defaults and empty catch-alls (RFC-1125)
    /// </summary>
    public static TheoryData<string, string, string, string> RequestPathRoutes { get; } = new()
    {
        // A value that only comes from the default is absent
        { "posts/{page=1}", "page=1", "/posts", "/posts" },
        { "posts/{page=1}", "page=1", "/posts/1", "/posts/{page}" },
        { "posts/{page=1}", "page=1", "/posts/", "/posts" },
        { "{controller=Home}/{action=Index}/{id?}", "controller=Home&action=Index", "/", "/" },
        { "{controller=Home}/{action=Index}/{id?}", "controller=Home&action=Index", "/Home", "/{controller}" },
        { "{controller=Home}/{action=Index}/{id?}", "controller=Home&action=Index", "/Home/Index", "/{controller}/{action}" },
        { "{controller=Home}/{action=Index}/{id?}", "controller=Home&action=Index&id=5", "/Home/Index/5", "/{controller}/{action}/{id}" },

        // When the request path is unknown, a parameter with a default is left out (fallback)
        { "posts/{page=1}", "page=1", null, "/posts" },

        // An empty catch-all keeps the slash that precedes it in the request
        { "files/{*path}", string.Empty, "/files", "/files" },
        { "files/{*path}", string.Empty, "/files/", "/files/" },
        { "files/{*path}", "path=/", "/files//", "/files/" },
    };

    /// <summary>
    /// Gets the route template, the path base, and the expected normalized route
    /// </summary>
    public static TheoryData<string, string, string> PathBaseRoutes { get; } = new()
    {
        { "users/{id}", "/api", "/api/users/{id}" },
        { "users/{id}", "/api/v1/", "/api/v1/users/{id}" },
        { "users/{id}", "/my app", "/my%20app/users/{id}" },
        { "users/{id}", "/tenant%2fblue", "/tenant%2Fblue/users/{id}" },
        { "users/{id}", "/100%", "/100%25/users/{id}" },
        { "users/{id}", "/a//b", "/a/b/users/{id}" },
        { "users/{id}", string.Empty, "/users/{id}" },
        { string.Empty, "/api", "/api" },
    };

    [Theory]
    [MemberData(nameof(Routes))]
    public void SimplifyRoutePattern_BuildsNormalizedRoute(string template, string routeValues, string expected)
    {
        var routePattern = RoutePatternFactory.Parse(template).DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var values = ParseRouteValues(routeValues);
        var normalizedRoute = NormalizedRouteBuilder.Create(pathBase: null);

        _ = AspNetCoreResourceNameHelper.SimplifyRoutePattern(
            routePattern,
            values,
            areaName: values.TryGetValue("area", out var area) ? area as string : null,
            controllerName: values.TryGetValue("controller", out var controller) ? controller as string : null,
            actionName: values.TryGetValue("action", out var action) ? action as string : null,
            expandRouteParameters: false,
            ref normalizedRoute);

        normalizedRoute.Complete(routePattern.RawText).Should().Be(expected);
    }

#if NET6_0_OR_GREATER
    [Theory]
    [MemberData(nameof(Routes))]
    public void SingleSpan_SimplifyRoutePattern_BuildsNormalizedRoute(string template, string routeValues, string expected)
    {
        var routePattern = RoutePatternFactory.Parse(template).DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var normalizedRoute = NormalizedRouteBuilder.Create(pathBase: null);

        _ = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, ParseRouteValues(routeValues), expandRouteParameters: false, ref normalizedRoute);

        normalizedRoute.Complete(routePattern.RawText).Should().Be(expected);
    }
#endif

    [Theory]
    [MemberData(nameof(RequestPathRoutes))]
    public void SimplifyRoutePattern_ResolvesPresenceFromTheRequestPath(string template, string routeValues, string requestPath, string expected)
    {
        var routePattern = RoutePatternFactory.Parse(template).DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var values = ParseRouteValues(routeValues);
        var normalizedRoute = NormalizedRouteBuilder.Create(pathBase: null, requestPath);

        _ = AspNetCoreResourceNameHelper.SimplifyRoutePattern(
            routePattern,
            values,
            areaName: null,
            controllerName: values.TryGetValue("controller", out var controller) ? controller as string : null,
            actionName: values.TryGetValue("action", out var action) ? action as string : null,
            expandRouteParameters: false,
            ref normalizedRoute);

        normalizedRoute.Complete(routePattern.RawText).Should().Be(expected);
    }

#if NET6_0_OR_GREATER
    [Theory]
    [MemberData(nameof(RequestPathRoutes))]
    public void SingleSpan_SimplifyRoutePattern_ResolvesPresenceFromTheRequestPath(string template, string routeValues, string requestPath, string expected)
    {
        var routePattern = RoutePatternFactory.Parse(template).DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var normalizedRoute = NormalizedRouteBuilder.Create(pathBase: null, requestPath);

        _ = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, ParseRouteValues(routeValues), expandRouteParameters: false, ref normalizedRoute);

        normalizedRoute.Complete(routePattern.RawText).Should().Be(expected);
    }
#endif

    [Theory]
    [MemberData(nameof(RequestPathRoutes))]
    public void SimplifyRouteTemplate_ResolvesPresenceFromTheRequestPath(string template, string routeValues, string requestPath, string expected)
    {
        var routeTemplate = TemplateParser.Parse(template);
        var values = ParseRouteValues(routeValues);
        var normalizedRoute = NormalizedRouteBuilder.Create(pathBase: null, requestPath);

        _ = AspNetCoreResourceNameHelper.SimplifyRouteTemplate(
            routeTemplate,
            values,
            areaName: null,
            controllerName: (values.TryGetValue("controller", out var controller) ? controller as string : null)?.ToLowerInvariant(),
            actionName: (values.TryGetValue("action", out var action) ? action as string : null)?.ToLowerInvariant(),
            expandRouteParameters: false,
            ref normalizedRoute);

        normalizedRoute.Complete(routeTemplate.TemplateText).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(TemplateRoutes))]
    public void SimplifyRouteTemplate_BuildsNormalizedRoute(string template, string routeValues, string expected)
    {
        var routeTemplate = TemplateParser.Parse(template);
        var values = ParseRouteValues(routeValues);
        var normalizedRoute = NormalizedRouteBuilder.Create(pathBase: null);

        _ = AspNetCoreResourceNameHelper.SimplifyRouteTemplate(
            routeTemplate,
            values,
            areaName: (values.TryGetValue("area", out var area) ? area as string : null)?.ToLowerInvariant(),
            controllerName: (values.TryGetValue("controller", out var controller) ? controller as string : null)?.ToLowerInvariant(),
            actionName: (values.TryGetValue("action", out var action) ? action as string : null)?.ToLowerInvariant(),
            expandRouteParameters: false,
            ref normalizedRoute);

        normalizedRoute.Complete(routeTemplate.TemplateText).Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(PathBaseRoutes))]
    public void NormalizedRoute_IncludesPathBase(string template, string pathBase, string expected)
    {
        var routePattern = RoutePatternFactory.Parse(template).DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var routeTemplate = TemplateParser.Parse(template);
        var values = ParseRouteValues("id=1");

        var fromPattern = NormalizedRouteBuilder.Create(pathBase);
        _ = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, values, null, null, null, expandRouteParameters: false, ref fromPattern);
        fromPattern.Complete(routePattern.RawText).Should().Be(expected);

        var fromTemplate = NormalizedRouteBuilder.Create(pathBase);
        _ = AspNetCoreResourceNameHelper.SimplifyRouteTemplate(routeTemplate, values, null, null, null, expandRouteParameters: false, ref fromTemplate);
        fromTemplate.Complete(routeTemplate.TemplateText).Should().Be(expected);
    }

    [Fact]
    public void NormalizedRoute_KeepsTheDeclaredRootSlashUnderPathBase()
    {
        var routePattern = RoutePatternFactory.Parse("/").DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var normalizedRoute = NormalizedRouteBuilder.Create("/api");

        _ = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, new RouteValueDictionary(), null, null, null, expandRouteParameters: false, ref normalizedRoute);

        normalizedRoute.Complete(routePattern.RawText).Should().Be("/api/");
    }

    [Fact]
    public void NormalizedRoute_DoesNotChangeTheResourceName()
    {
        var routePattern = RoutePatternFactory.Parse("{controller}/{action}/{id?}").DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var values = ParseRouteValues("controller=Home&action=Index&id=5");
        var normalizedRoute = NormalizedRouteBuilder.Create(pathBase: null);

        var withNormalizedRoute = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, values, null, "Home", "Index", expandRouteParameters: false, ref normalizedRoute);
        var withoutNormalizedRoute = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, values, null, "Home", "Index", expandRouteParameters: false);

        withNormalizedRoute.Should().Be(withoutNormalizedRoute);
        normalizedRoute.Complete(routePattern.RawText).Should().Be("/{controller}/{action}/{id}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public void NormalizedRoute_EncodesNonAsciiRuns(int length)
    {
        var literal = new string('é', length);
        var routePattern = RoutePatternFactory.Parse($"a{literal}b").DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var normalizedRoute = NormalizedRouteBuilder.Create(pathBase: null);

        _ = AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, new RouteValueDictionary(), null, null, null, expandRouteParameters: false, ref normalizedRoute);

        normalizedRoute.Complete(routePattern.RawText).Should().Be($"/a{string.Concat(Enumerable.Repeat("%C3%A9", length))}b");
    }

    [Fact]
    public void NormalizedRoute_IsBuiltWithoutTheResourceName()
    {
        var routePattern = RoutePatternFactory.Parse("users/{id}").DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var routeTemplate = TemplateParser.Parse("users/{id}");
        var values = ParseRouteValues("id=1");

        var fromPattern = NormalizedRouteBuilder.Create(pathBase: null);
        AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, values, null, null, null, expandRouteParameters: false, ref fromPattern, returnResourceName: false).Should().BeEmpty();
        fromPattern.Complete(routePattern.RawText).Should().Be("/users/{id}");

        var fromTemplate = NormalizedRouteBuilder.Create(pathBase: null);
        AspNetCoreResourceNameHelper.SimplifyRouteTemplate(routeTemplate, values, null, null, null, expandRouteParameters: false, ref fromTemplate, returnResourceName: false).Should().BeEmpty();
        fromTemplate.Complete(routeTemplate.TemplateText).Should().Be("/users/{id}");

#if NET6_0_OR_GREATER
        var fromSingleSpan = NormalizedRouteBuilder.Create(pathBase: null);
        AspNetCoreResourceNameHelper.SimplifyRoutePattern(routePattern, values, expandRouteParameters: false, ref fromSingleSpan, returnResourceName: false).Should().BeEmpty();
        fromSingleSpan.Complete(routePattern.RawText).Should().Be("/users/{id}");
#endif
    }

    [Fact]
    public void DisabledBuilder_ReturnsNull()
    {
        var normalizedRoute = default(NormalizedRouteBuilder);

        normalizedRoute.IsEnabled.Should().BeFalse();
        normalizedRoute.Complete("users/{id}").Should().BeNull();
    }

    private static TheoryData<string, string, string> GetTemplateRoutes()
    {
        var data = new TheoryData<string, string, string>();
        foreach (var row in Routes)
        {
            var template = (string)row[0];

            // MVC templates have no leading slash, and "**" catch-alls only exist since ASP.NET Core 3.0
            if (!template.StartsWith("/") && !template.Contains("{**"))
            {
                data.Add(template, (string)row[1], (string)row[2]);
            }
        }

        return data;
    }

    private static RouteValueDictionary ParseRouteValues(string routeValues)
    {
        var values = new RouteValueDictionary();
        foreach (var pair in routeValues.Split(Separators.Ampersand, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            values[pair.Substring(0, separator)] = pair.Substring(separator + 1);
        }

        return values;
    }
}
#endif
