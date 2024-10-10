// <copyright file="RequestDataHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Web;
using Datadog.Trace.Agent;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;

namespace Datadog.Trace.Tests.Util;

public class RequestDataHelperTests
{
    [Fact]
    public void GivenADangerousQueryString_WhenGetQueryString_HelperAvoidsException()
    {
        var request = new HttpRequest("file", "http://localhost/benchmarks", "data=<script>alert(1)</script>");
        request.ValidateInput();

        try
        {
            _ = request.QueryString;
            Assert.True(false);
        }
        catch (HttpRequestValidationException)
        {
            var request2 = new HttpRequest("file", "http://localhost/benchmarks", "data=<script>alert(1)</script>");
            request2.ValidateInput();
            var queryString = RequestDataHelper.GetQueryString(request2);
            queryString.Should().BeNull();
        }
    }

    [Fact]
    public void GivenADangerousQueryString_WhenCallingASMAndIAST_NoExceptionIsThrown()
    {
        var request = new HttpRequest("file", "http://localhost/benchmarks", "data=<script>alert(1)</script>");
        CheckRequest(request);
    }

    [Fact]
    public void GivenADangerousCookie_WhenCallingASMAndIAST__NoExceptionIsThrown()
    {
        var dangerous = "script>alert(1)</script>";
        var request = new HttpRequest("file", "http://localhost/benchmarks", null);
        request.Cookies.Add(new HttpCookie("data", dangerous));
        CheckRequest(request);
    }

    [Fact]
    public void GivenADangerousCookie_WhenCallingASMAndIAST_NoExceptionIsThrown()
    {
        var request = new HttpRequest("<script>alert(1)</script>", "http://localhost/<script>alert(1)</script>", null);
        CheckRequest(request);
    }

    private static void CheckRequest(HttpRequest request)
    {
        var settings = TracerSettings.Create(new()
        {
            { ConfigurationKeys.PeerServiceDefaultsEnabled, "true" },
            { ConfigurationKeys.PeerServiceNameMappings, "a-peer-service:a-remmaped-peer-service" }
        });

        var writerMock = new Mock<IAgentWriter>();
        var samplerMock = new Mock<ITraceSampler>();
        var security = new AppSec.Security(null, null, null);
        var tracer = new Tracer(settings, writerMock.Object, samplerMock.Object, scopeManager: null, statsd: null);
        var scope = (Scope)tracer.StartActive("Root");
        scope.Span.ServiceName = "service";
        HttpContext context = new HttpContext(request, new HttpResponse(new System.IO.StringWriter()));
        request.ValidateInput();
        var transport = new SecurityCoordinator.HttpTransport(context);
        var securityCoordinator = SecurityCoordinator.Get(security, scope.Span, transport);
        // We should not launch any exception here
        var result = securityCoordinator.GetBasicRequestArgsForWaf();
        var iastContext = new IastRequestContext();
        iastContext.AddRequestData(request);
        result.Should().NotBeNull();
    }
}
#endif
