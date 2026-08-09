// <copyright file="LegacyAspNetCoreResourceNameHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK

using System;
using Datadog.Trace.DiagnosticListeners;
using Datadog.Trace.DuckTyping;
using FluentAssertions;
using Microsoft.AspNetCore.Routing.Template;
using Xunit;

namespace Datadog.Trace.Tests.DiagnosticListeners;

public class LegacyAspNetCoreResourceNameHelperTests
{
    [SkippableTheory]
    [MemberData(nameof(AspNetCoreResourceNameHelperTests.ValidRouteTemplates), MemberType = typeof(AspNetCoreResourceNameHelperTests))]
    public void SimplifyRouteTemplate_CleansValidRouteTemplates(string template, string expected, bool expandRouteTemplates)
    {
        // RouteTemplate doesn't support the concept of encoded slashes, so skip those cases
        // These were only introduced in ASP.NET Core 2.2 https://github.com/aspnet/Routing/pull/719,
        // and are only supported when you're using endpoint routing
        if (template.Contains("**") || expected.Contains("**"))
        {
            throw new SkipException();
        }

        var routeTemplate = TemplateParser.Parse(template);
        var resource = LegacyAspNetCoreResourceNameHelper.SimplifyRouteTemplate(
            routeTemplate: routeTemplate.DuckCast<LegacyAspNetCoreDiagnosticObserver.RouteTemplateStruct>(),
            routeValueDictionary: AspNetCoreResourceNameHelperTests.Values,
            areaName: null,
            controllerName: AspNetCoreResourceNameHelperTests.Values["controller"] as string,
            actionName: AspNetCoreResourceNameHelperTests.Values["action"] as string,
            expandRouteTemplates);

        resource.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(AspNetCoreResourceNameHelperTests.ValidRouteTemplatesWithExternalDefaults), MemberType = typeof(AspNetCoreResourceNameHelperTests))]
    public void SimplifyRouteTemplate_CleansValidRouteTemplatesWithDefaults(string template, string expected, bool expandRouteTemplates)
    {
        var routeTemplate = TemplateParser.Parse(template);
        var resource = LegacyAspNetCoreResourceNameHelper.SimplifyRouteTemplate(
            routeTemplate: routeTemplate.DuckCast<LegacyAspNetCoreDiagnosticObserver.RouteTemplateStruct>(),
            routeValueDictionary: AspNetCoreResourceNameHelperTests.Values,
            areaName: null,
            controllerName: AspNetCoreResourceNameHelperTests.Values["controller"] as string,
            actionName: AspNetCoreResourceNameHelperTests.Values["action"] as string,
            expandRouteTemplates);

        resource.Should().Be(expected);
    }

    [Fact]
    public void IsIdentifierSegment_NullValue_ReturnsFalse()
    {
        var result = LegacyAspNetCoreResourceNameHelper.IsIdentifierSegment(null, out var valueAsString);
        result.Should().BeFalse();
        valueAsString.Should().BeNull();
    }

    [Theory]
    [MemberData(nameof(AspNetCoreResourceNameHelperTests.IdentifierSegments), MemberType = typeof(AspNetCoreResourceNameHelperTests))]
    public void IsIdentifierSegment_BothImplementationsAgree(object value)
    {
        // We're doing a culture-insensitive ToString() here for CI consistency,
        // even though in the real implementation it's culture sensitive!
        var stringValue = value as string ?? FormattableString.Invariant($"{value}");

        // Verify UriHelpers gives the same result for the string representation
        var expected = Trace.Util.UriHelpers.IsIdentifierSegment(stringValue, 0, stringValue.Length);

        var actual = LegacyAspNetCoreResourceNameHelper.IsIdentifierSegment(value, out _);
        actual.Should().Be(expected);
    }

    // These differ from the IsIdentifierSegment() result, but we don't care,
    // because in this case our knowledge is better so we give a more accurate result
    [Theory]
    [InlineData("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF")]
    [InlineData("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA")]
    public void IsIdentifierSegment_Discrepancy_GuidWithNoDigits(string guidString)
    {
        var guid = new Guid(guidString);
        LegacyAspNetCoreResourceNameHelper.IsIdentifierSegment(guid, out _).Should().BeTrue();
    }
}
#endif
