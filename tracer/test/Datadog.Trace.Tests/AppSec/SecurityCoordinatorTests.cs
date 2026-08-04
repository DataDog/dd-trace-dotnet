// <copyright file="SecurityCoordinatorTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if !NETFRAMEWORK
using System;
using System.Collections.Generic;
using Datadog.Trace.Agent;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Configuration;
using Datadog.Trace.Sampling;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.AppSec;

public class SecurityCoordinatorTests
{
    /// <summary>
    /// ASP.NET Core pools <see cref="HttpContext"/> instances and uninitializes them once the request is
    /// over, which leaves the feature collection null. Reading the request afterwards throws a
    /// <see cref="NullReferenceException"/> from inside ASP.NET Core itself, and that used to escape
    /// <c>Scan</c> into the customer's pipeline, turning the request into a 500.
    /// </summary>
    [Fact]
    public void GivenAnUninitializedHttpContext_WhenScanning_NoExceptionIsThrown()
    {
        var context = new DefaultHttpContext();
        context.Uninitialize();

        // this is the failure the tracer has to survive: it comes from ASP.NET Core, not from our code
        Action readTheRequest = () => _ = context.Request.Cookies;
        readTheRequest.Should().Throw<NullReferenceException>();

        var transport = new SecurityCoordinator.HttpTransport(context);
        var securityCoordinator = CreateSecurityCoordinator(transport);

        IResult result = null;
        Action scan = () => result = securityCoordinator.Scan();

        scan.Should().NotThrow();
        result.Should().BeNull();
        transport.IsHttpContextDisposed.Should().BeTrue();
    }

    [Fact]
    public void GivenAnUninitializedHttpContext_WhenScanningTwice_NeitherScanThrows()
    {
        var context = new DefaultHttpContext();
        context.Uninitialize();

        var transport = new SecurityCoordinator.HttpTransport(context);
        var securityCoordinator = CreateSecurityCoordinator(transport);

        securityCoordinator.Scan().Should().BeNull();

        // the flag is what lets the second scan bail out before touching the context at all, though from
        // the outside both scans are indistinguishable: they return null either way
        transport.IsHttpContextDisposed.Should().BeTrue();
        securityCoordinator.Scan().Should().BeNull();
    }

    /// <summary>
    /// The route data is read on every <c>RunWaf</c>, RASP included. An exception there was contained by
    /// <c>RunWaf</c>'s catch, but at the cost of an error log and an error telemetry metric per call, and it
    /// threw before anything could set <see cref="SecurityCoordinator.HttpTransport.IsHttpContextDisposed"/>,
    /// so every later call paid for it again.
    /// </summary>
    [Fact]
    public void GivenAnUninitializedHttpContext_WhenReadingTheRouteData_NoExceptionIsThrown()
    {
        var context = new DefaultHttpContext();
        context.Uninitialize();

        var transport = new SecurityCoordinator.HttpTransport(context);

        IDictionary<string, object> routeData = null;
        Action readRouteData = () => routeData = transport.RouteData;

        readRouteData.Should().NotThrow();
        routeData.Should().BeNull();
        transport.IsHttpContextDisposed.Should().BeTrue();
    }

    /// <summary>
    /// Reachable from <c>EventTrackingSdk</c> and <c>EventTrackingSdkV2</c>, which don't catch anything, so
    /// this used to throw straight into the application that called the SDK.
    /// </summary>
    [Fact]
    public void GivenAnUninitializedHttpContext_WhenCollectingHeaders_NoExceptionIsThrown()
    {
        var context = new DefaultHttpContext();
        context.Uninitialize();

        var transport = new SecurityCoordinator.HttpTransport(context);
        var securityCoordinator = CreateSecurityCoordinator(transport);

        Action collectHeaders = () => securityCoordinator.Reporter.CollectHeaders();

        collectHeaders.Should().NotThrow();
        transport.IsHttpContextDisposed.Should().BeTrue();
    }

    private static SecurityCoordinator CreateSecurityCoordinator(SecurityCoordinator.HttpTransport transport)
    {
        var settings = TracerSettings.Create(new Dictionary<string, object>());
        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();
        var security = new Security(null, null, null);
        var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
        var scope = (Scope)tracer.StartActive("Root");

        return SecurityCoordinator.Get(security, scope.Span, transport);
    }
}
#endif
