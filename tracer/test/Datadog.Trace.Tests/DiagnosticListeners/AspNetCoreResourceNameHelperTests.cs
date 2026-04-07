// <copyright file="AspNetCoreResourceNameHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>
#if !NETFRAMEWORK
using System;
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
        { "{area:exists}/{controller}/{action}", "/{area}/home/index", false },
        { "{area:exists}/{controller}/{action}", "/{area}/home/index", true },
        { "prefix/{controller}/{action}", "/prefix/home/index", false },
        { "prefix/{controller}/{action}", "/prefix/home/index", true },
        { "prefix-{controller}/{action}-suffix", "/prefix-home/index-suffix", false },
        { "prefix-{controller}/{action}-suffix", "/prefix-home/index-suffix", true },
        { "prefix-{controller}-{action}-{nonid}-{id}-{FormValue}-suffix", "/prefix-home-index-{nonid}-{id}-{formvalue}-suffix", false },
        { "prefix-{controller}-{action}-{nonid}-{id}-{FormValue}-suffix", "/prefix-home-index-oops-{id}-view-suffix", true },
        { "prefix-{controller}-{action}-{Area}-{id}-{FormValue}-suffix", "/prefix-home-index-{area}-{id}-{formvalue}-suffix", false },
        { "prefix-{controller}-{action}-{Area}-{id}-{FormValue}-suffix", "/prefix-home-index-{area}-{id}-view-suffix", true },
        { "prefix-{controller}-{action}-{id}-{FormValue}/{Area?}", "/prefix-home-index-{id}-{formvalue}", false },
        { "prefix-{controller}-{action}-{id}-{FormValue}/{Area?}", "/prefix-home-index-{id}-view", true },
        { "standalone/prefix-{controller}-{action}-{nonid}-{id}-{FormValue}-suffix/standalone", "/standalone/prefix-home-index-{nonid}-{id}-{formvalue}-suffix/standalone", false },
        { "standalone/prefix-{controller}-{action}-{nonid}-{id}-{FormValue}-suffix/standalone", "/standalone/prefix-home-index-oops-{id}-view-suffix/standalone", true },
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
        // Note: This is _wrong_ (flips the one * -> ** and vice versa),
        // but it's a breaking change to fix it, so leave it as is
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
        // RouteTemplate doesn't support the concept of encoded slashes, so "fix" the expected value
        expected = expected.Replace("**", "*");

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

    [Fact]
    public void IsIdentifierSegment_NullValue_ReturnsFalse()
    {
        var result = AspNetCoreResourceNameHelper.IsIdentifierSegment(null, out var valueAsString);
        result.Should().BeFalse();
        valueAsString.Should().BeNull();
    }

    [Theory]
    // Integers - both implementations agree these are identifiers
    [InlineData(123)] // true
    [InlineData(0)] // true
    [InlineData(-5)] // negative int: string "-5" has digit and hyphen is allowed // true
    [InlineData(9999999999L)] // true
    // Whole number floats - string representation has no decimal point
    [InlineData(123f)] // true
    [InlineData(12334567f)] // true
    [InlineData(123d)] // true
    [InlineData(1234567d)] // true
    // Fractional floats - we don't try to fast-track these, because for historical reasons
    // we do a culture--sensitive comparison, and may-or-may not mark the same value as identifier or not
    [InlineData(1.5f)] // true
    [InlineData(1.5d)] // true
    [InlineData(-1.5f)] // true
    [InlineData(-1.5d)] // true
    // Strings - both use UriHelpers directly
    [InlineData("123")] // true
    [InlineData("abc")] // short hex, no digits // false
    [InlineData("controller")] // false
    [InlineData("14bb2eed-34f0-4aa2-b2c3-09c0e2166d4d")] // GUID string with digits // true
    [InlineData("hello-world")] // contains non-hex letters // false
    public void IsIdentifierSegment_BothImplementationsAgree(object value)
    {
        // We're doing a culture-insensitive ToString() here for CI consistency,
        // even though in the real implementation it's culture sensitive!
        var stringValue = value as string ?? FormattableString.Invariant($"{value}");

        // Verify UriHelpers gives the same result for the string representation
        var expected = Trace.Util.UriHelpers.IsIdentifierSegment(stringValue, 0, stringValue.Length);

        var actual = AspNetCoreResourceNameHelper.IsIdentifierSegment(value, out _);
        actual.Should().Be(expected);
    }

    // Guid can't be used directly in InlineData
    [Theory]
    // GUIDs with digits - both implementations agree (passed as string, parsed to Guid)
    [InlineData("14bb2eed-34f0-4aa2-b2c3-09c0e2166d4d")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void IsIdentifierSegment_BothImplementationsAgree_Guids(string guidString)
    {
        var guid = new Guid(guidString);
        IsIdentifierSegment_BothImplementationsAgree(guid);
    }

    // Decimal can't be used in InlineData
    [Theory]
    [InlineData("123")]
    [InlineData("1234567")]
    [InlineData("1.5")]
    [InlineData("-1.5")]
    public void IsIdentifierSegment_BothImplementationsAgree_Decimal(string decimalString)
    {
        var value = decimal.Parse(decimalString);
        IsIdentifierSegment_BothImplementationsAgree(value);
    }

    // These differ from the IsIdentifierSegment() result, but we don't care,
    // because in this case our knowledge is better so we give a more accurate result
    [Theory]
    [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
    [InlineData("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA")]
    public void IsIdentifierSegment_Discrepancy_GuidWithNoDigits(string guidString)
    {
        var guid = new Guid(guidString);
        AspNetCoreResourceNameHelper.IsIdentifierSegment(guid, out _).Should().BeTrue();
    }

#if NET6_0_OR_GREATER
    [Theory]
    [MemberData(nameof(ValidRouteTemplates))]
    public void SingleSpan_SimplifyRoutePattern_CleansValidRouteTemplates(string template, string expected, bool expandRouteTemplates)
    {
        var originalPattern = RoutePatternFactory.Parse(template);
        var duckTypedPattern = originalPattern.DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var resource = AspNetCoreResourceNameHelper.SimplifyRoutePattern(
            routePattern: duckTypedPattern,
            routeValueDictionary: Values,
            expandRouteTemplates);

        resource.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ValidRouteTemplatesWithExternalDefaults))]
    public void SingleSpan_SimplifyRoutePattern_CleansValidRouteTemplatesWithDefaults(string template, string expected, bool expandRouteTemplates)
    {
        var originalPattern = RoutePatternFactory.Parse(template, Defaults, parameterPolicies: ParameterPolicies);
        var duckTypedPattern = originalPattern.DuckCast<Datadog.Trace.DiagnosticListeners.RoutePattern>();
        var resource = AspNetCoreResourceNameHelper.SimplifyRoutePattern(
            routePattern: duckTypedPattern,
            routeValueDictionary: Values,
            expandRouteTemplates);

        resource.Should().Be(expected);
    }
#endif
}
#endif
