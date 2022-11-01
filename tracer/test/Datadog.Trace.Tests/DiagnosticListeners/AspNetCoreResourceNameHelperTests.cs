// <copyright file="AspNetCoreResourceNameHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.Routing.Template;
using Xunit;

namespace Datadog.Trace.Tests.DiagnosticListeners;

public class AspNetCoreResourceNameHelperTests
{
    private static readonly RouteValueDictionary Values = new()
    {
        { "controller", "Home" },
        { "action", "Index" },
        { "nonid", "oops" },
        { "idlike", 123 },
        { "FormValue", "View" },
    };

    private static readonly RouteValueDictionary Defaults = new()
    {
        { "controller", "Home" },
        { "action", "Index" },
        { "FormValue", "View" }
    };

    private static readonly RouteValueDictionary ParameterPolicies = new()
    {
        { "id", new OptionalRouteConstraint(new IntRouteConstraint()) }
    };

    /// <summary>
    /// Gets the route template, expected output, expand names
    /// </summary>
    public static TheoryData<string, string, bool> ValidRouteTemplates { get; } = new()
    {
        { "{controller}/{action}/{id}", "/home/index/{id}", false },
        { "{controller}/{action}/{id}", "/home/index/{id}", true },
        { "{controller}/{action}", "/home/index", false },
        { "{controller}/{action}", "/home/index", true },
        { "prefix/{controller}/{action}", "/prefix/home/index", false },
        { "prefix/{controller}/{action}", "/prefix/home/index", true },
        { "prefix-{controller}/{action}-suffix", "/prefix-home/index-suffix", false },
        { "prefix-{controller}/{action}-suffix", "/prefix-home/index-suffix", true },
        { "{controller}/{action}/{nonid}", "/home/index/{nonid}", false },
        { "{controller}/{action}/{nonid}", "/home/index/oops", true },
        { "{controller}/{action}/{nonid?}", "/home/index/{nonid?}", false },
        { "{controller}/{action}/{nonid?}", "/home/index/oops", true },
        { "{controller}/{action}/{nonid=2}", "/home/index/{nonid}", false },
        { "{controller}/{action}/{nonid=2}", "/home/index/oops", true },
        { "{controller}/{action}/{nonid:int}", "/home/index/{nonid}", false },
        { "{controller}/{action}/{nonid:int}", "/home/index/oops", true },
        { "{controller}/{action}/{FormValue}", "/home/index/{formvalue}", false },
        { "{controller}/{action}/{FormValue}", "/home/index/view", true },
        { "{controller}/{action}/{FormValue?}", "/home/index/{formvalue?}", false },
        { "{controller}/{action}/{FormValue?}", "/home/index/view", true },
        { "{controller}/{action}/{FormValue=Edit}", "/home/index/{formvalue}", false },
        { "{controller}/{action}/{FormValue=Edit}", "/home/index/view", true },
        { "{controller}/{action}/{FormValue:int}", "/home/index/{formvalue}", false },
        { "{controller}/{action}/{FormValue:int}", "/home/index/view", true },
        { "{controller}/{action}/{nonidentity}", "/home/index/{nonidentity}", false },
        { "{controller}/{action}/{nonidentity}", "/home/index/{nonidentity}", true },
        { "{controller}/{action}/{nonidentity?}", "/home/index", false },
        { "{controller}/{action}/{nonidentity?}", "/home/index", true },
        { "{controller}/{action}/{nonidentity=2}", "/home/index/{nonidentity}", false },
        { "{controller}/{action}/{nonidentity=2}", "/home/index/{nonidentity}", true },
        { "{controller}/{action}/{nonidentity:int}", "/home/index/{nonidentity}", false },
        { "{controller}/{action}/{nonidentity:int}", "/home/index/{nonidentity}", true },
        { "{controller}/{action}/{idlike}", "/home/index/{idlike}", false },
        { "{controller}/{action}/{idlike}", "/home/index/{idlike}", true },
        { "{controller}/{action}/{id:int}", "/home/index/{id}", false },
        { "{controller}/{action}/{id:int}", "/home/index/{id}", true },
        // Note: This is _wrong_ (flips the one * -> **), but it's a breaking change
        // so leave it as this
        { "{controller}/{action}/{*nonid}", "/home/index/{**nonid}", false },
        { "{controller}/{action}/{*nonid}", "/home/index/oops", true },
        { "{controller}/{action}/{**nonid}", "/home/index/{*nonid}", false },
        { "{controller}/{action}/{**nonid}", "/home/index/oops", true },
        { "{controller}", "/home", false },
        { "{controller}", "/home", true },
    };

    /// <summary>
    /// Gets the route template, expected output, expand names
    /// </summary>
    public static TheoryData<string, string, bool> ValidRouteTemplatesWithExternalDefaults { get; } = new()
    {
        { "{controller}/{action}/{id}", "/home/index/{id}", false },
        { "{controller}/{action}/{id}", "/home/index/{id}", true },
        { "{controller}/{action}/{id:int}", "/home/index/{id}", false },
        { "{controller}/{action}/{id:int}", "/home/index/{id}", true },
        { "{controller}/{action}/{FormValue}", "/home/index/{formvalue}", false },
        { "{controller}/{action}/{FormValue}", "/home/index/view", true },
        { "{controller}/{action}/{FormValue:int}", "/home/index/{formvalue}", false },
        { "{controller}/{action}/{FormValue:int}", "/home/index/view", true },
        { "{controller}/{action}/{identity}", "/home/index/{identity}", false },
        { "{controller}/{action}/{identity}", "/home/index/{identity}", true },
        { "{controller}/{action}", "/home/index", false },
        { "{controller}/{action}", "/home/index", true },
        { "{controller}/{*action}", "/home/index", false },
        { "{controller}/{*action}", "/home/index", true },
        { "{controller}/{action:string}", "/home/index", false },
        { "{controller}/{action:string}", "/home/index", true },
    };

    [Theory]
    [MemberData(nameof(ValidRouteTemplates))]
    public void SimplifyRoutePattern_CleansValidRouteTemplates(string template, string expected, bool expandRouteTemplates)
    {
        var originalPattern = RoutePatternFactory.Parse(template);
        var duckTypedPattern = originalPattern.DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var resource = AspNetCoreResourceNameHelper.SimplifyRoutePattern(
            routePattern: duckTypedPattern,
            routeValueDictionary: Values,
            areaName: null,
            controllerName: Values["controller"] as string,
            actionName: Values["action"] as string,
            expandRouteTemplates);

        resource.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ValidRouteTemplatesWithExternalDefaults))]
    public void SimplifyRoutePattern_CleansValidRouteTemplatesWithDefaults(string template, string expected, bool expandRouteTemplates)
    {
        var originalPattern = RoutePatternFactory.Parse(template, Defaults, parameterPolicies: ParameterPolicies);
        var duckTypedPattern = originalPattern.DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var resource = AspNetCoreResourceNameHelper.SimplifyRoutePattern(
            routePattern: duckTypedPattern,
            routeValueDictionary: Values,
            areaName: null,
            controllerName: Values["controller"] as string,
            actionName: Values["action"] as string,
            expandRouteTemplates);

        resource.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ValidRouteTemplates))]
    public void SimplifyRouteTemplate_CleansValidRouteTemplates(string template, string expected, bool expandRouteTemplates)
    {
        var routeTemplate = new RouteTemplate(RoutePatternFactory.Parse(template));
        var resource = AspNetCoreResourceNameHelper.SimplifyRouteTemplate(
            routePattern: routeTemplate,
            routeValueDictionary: Values,
            areaName: null,
            controllerName: Values["controller"] as string,
            actionName: Values["action"] as string,
            expandRouteTemplates);

        resource.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ValidRouteTemplatesWithExternalDefaults))]
    public void SimplifyRouteTemplate_CleansValidRouteTemplatesWithDefaults(string template, string expected, bool expandRouteTemplates)
    {
        var routeTemplate = new RouteTemplate(RoutePatternFactory.Parse(template));
        var resource = AspNetCoreResourceNameHelper.SimplifyRouteTemplate(
            routePattern: routeTemplate,
            routeValueDictionary: Values,
            areaName: null,
            controllerName: Values["controller"] as string,
            actionName: Values["action"] as string,
            expandRouteTemplates);

        resource.Should().Be(expected);
    }
}
#endif
