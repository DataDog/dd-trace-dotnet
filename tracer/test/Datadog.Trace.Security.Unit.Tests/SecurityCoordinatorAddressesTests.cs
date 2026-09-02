// <copyright file="SecurityCoordinatorAddressesTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Collections.Generic;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.AppSec.Waf;
using Datadog.Trace.Security.Unit.Tests.Utils;
using FluentAssertions;
#if NETFRAMEWORK
using System.IO;
using System.Web;
using System.Web.Routing;
#else
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
#endif
using Moq;
using Xunit;
using static Datadog.Trace.AppSec.Coordinator.SecurityCoordinator;

namespace Datadog.Trace.Security.Unit.Tests;

/// <summary>
/// Who sends which address to the WAF, and when. The request addresses go out once per request and the
/// response status only when the response is actually known, so these pin the coordinator side of it
/// rather than what the WAF makes of it.
/// </summary>
public class SecurityCoordinatorAddressesTests : WafLibraryRequiredTest
{
    [Fact]
    public void GivenARequestContext_WhenTheRequestAddressesAreClaimedTwice_ThenOnlyTheFirstCallerSendsThem()
    {
        var appsecRequestContext = new AppSecRequestContext();

        appsecRequestContext.ShouldSendRequestAddresses().Should().BeTrue();
        appsecRequestContext.ShouldSendRequestAddresses().Should().BeFalse();
    }

    [Fact]
    public void GivenARequestContext_WhenTheResponseScanIsClaimedTwice_ThenOnlyTheFirstCallerScansIt()
    {
        var appsecRequestContext = new AppSecRequestContext();

        appsecRequestContext.ShouldScanResponse().Should().BeTrue();
        appsecRequestContext.ShouldScanResponse().Should().BeFalse();
    }

#if !NETFRAMEWORK
    [Fact]
    public void GivenACoreRequest_WhenItIsScannedSeveralTimes_ThenTheAddressesGoOutOnceAndTheStatusOnlyAtTheEnd()
    {
        var runs = new List<Dictionary<string, object>>();
        var wafContext = new Mock<IContext>();
        wafContext.Setup(x => x.Run(It.IsAny<IDictionary<string, object>>(), It.IsAny<ulong>()))
                  .Callback<IDictionary<string, object>, ulong>((args, _) => runs.Add(new Dictionary<string, object>(args)));
        var waf = new Mock<IWaf>();
        waf.Setup(x => x.CreateContext()).Returns(wafContext.Object);
        waf.Setup(x => x.GetKnownAddresses()).Returns([]);

        using var security = new AppSec.Security(waf: waf.Object);
        var securityCoordinator = Get(security, CreateWebSpan(), new HttpTransport(CreateCoreHttpContext(statusCode: 404)));

        securityCoordinator.Scan();
        securityCoordinator.Scan();
        securityCoordinator.Scan(lastTime: true);

        runs.Should().HaveCount(2);
        runs[0].Should().Contain(AddressesConstants.RequestMethod, "GET").And.NotContainKey(AddressesConstants.ResponseStatus);
        runs[1].Should().Contain(AddressesConstants.ResponseStatus, "404").And.NotContainKey(AddressesConstants.RequestMethod);
    }
#else
    [Fact]
    public void GivenAFrameworkRequest_WhenTheRequestStarts_ThenNoResponseStatusIsSent()
    {
        var securityCoordinator = GetFrameworkCoordinator(statusCode: 404, out _);

        securityCoordinator.GetBasicRequestArgsForWaf().Should().NotContainKey(AddressesConstants.ResponseStatus);
    }

    [Fact]
    public void GivenAFrameworkRequest_WhenTheRequestEnds_ThenTheRealStatusAndTheLateAddressesAreSent()
    {
        var securityCoordinator = GetFrameworkCoordinator(statusCode: 404, out var routeValues);
        routeValues.Add("controller", "home");

        var args = securityCoordinator.GetEndRequestArgsForWaf();

        args.Should().Contain(AddressesConstants.ResponseStatus, "404");
        args[AddressesConstants.RequestPathParams].Should().BeEquivalentTo(new Dictionary<string, object> { { "controller", "home" } });
        args[AddressesConstants.RequestCookies].Should().BeEquivalentTo(new Dictionary<string, object> { { "sid", "abc" } });
    }
#endif

    private static Span CreateWebSpan()
    {
        var traceContext = new TraceContext(new EmptyDatadogTracer());
        var spanContext = new SpanContext(parent: null, traceContext, serviceName: "My Service Name", traceId: (TraceId)100, spanId: 200);
        return new Span(spanContext, DateTimeOffset.Now);
    }

#if !NETFRAMEWORK
    private static HttpContext CreateCoreHttpContext(int statusCode)
    {
        var headers = new Mock<IHeaderDictionary>();
        headers.Setup(x => x.Keys).Returns([]);

        var request = new Mock<HttpRequest>();
        request.Setup(x => x.Method).Returns("GET");
        request.Setup(x => x.Path).Returns(new PathString("/etc/passwd"));
        request.Setup(x => x.Headers).Returns(headers.Object);

        var response = new Mock<HttpResponse>();
        response.Setup(x => x.StatusCode).Returns(statusCode);

        var routing = new Mock<IRoutingFeature>();
        routing.Setup(x => x.RouteData).Returns(new RouteData());

        var context = new Mock<HttpContext>();
        context.Setup(x => x.Request).Returns(request.Object);
        context.Setup(x => x.Response).Returns(response.Object);
        context.Setup(x => x.Features[typeof(IRoutingFeature)]).Returns(routing.Object);
        return context.Object;
    }
#else
    private static SecurityCoordinator GetFrameworkCoordinator(int statusCode, out RouteValueDictionary routeValues)
    {
        var request = new HttpRequest("file", "http://localhost/benchmarks", "data=param");
        request.Cookies.Add(new HttpCookie("sid", "abc"));
        var response = new HttpResponse(new StringWriter()) { StatusCode = statusCode };
        var context = new HttpContext(request, response);
        var routeData = new RouteData();
        routeValues = routeData.Values;
        request.RequestContext = new RequestContext(new HttpContextWrapper(context), routeData);

        return Get(new AppSec.Security(), CreateWebSpan(), new HttpTransport(context));
    }
#endif
}
