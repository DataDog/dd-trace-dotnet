// <copyright file="RequestDataHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System.Collections.Generic;
using System.Reflection;
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
    public void GivenADangerousBody_WhenGetQueryString_HelperAvoidsException()
    {
        var request = CreateRequestWithValidationAndBody(new Dictionary<string, string>() { { "data", "<script>alert(1)</script>" } });

        try
        {
            _ = request.Form;
            Assert.True(false);
        }
        catch (HttpRequestValidationException)
        {
            var request2 = CreateRequestWithValidationAndBody(new Dictionary<string, string>() { { "data", "<script>alert(1)</script>" } });
            var body = RequestDataHelper.GetForm(request2);
            body.Should().BeNull();
        }
    }

    [Fact]
    public void GivenADangerousBody_WhenGetValue_HelperAvoidsException()
    {
        var request = CreateRequestWithValidationAndBody(new Dictionary<string, string>() { { "data", "<script>alert(1)</script>" } });

        // We need to enable the granular validation for the request
        // This translates validation to the HttpValueCollections contained in the query instead of being done at a HttpRequest level
        EnableGranularValidation(request);

        // Now, calling the Form property should not throw an exception
        var form = request.Form;

        try
        {
            // Calling Get would trigger the exception
            _ = form["data"];
            Assert.True(false);
        }
        catch (HttpRequestValidationException)
        {
            var request2 = CreateRequestWithValidationAndBody(new Dictionary<string, string>() { { "data", "<script>alert(1)</script>" } });
            EnableGranularValidation(request2);
            var value = RequestDataHelper.GetNameValueCollectionValue(request.Form, "data");
            value.Should().BeNull();
        }
    }

    [Fact]
    public void GivenADangerousBody_WhenGetValues_HelperAvoidsException()
    {
        var request = CreateRequestWithValidationAndBody(new Dictionary<string, string>() { { "data", "<script>alert(1)</script>" } });

        // We need to enable the granular validation for the request
        // This translates validation to the HttpValueCollections contained in the query instead of being done at a HttpRequest level
        EnableGranularValidation(request);

        // Now, calling the Form property should not throw an exception
        var form = request.Form;

        try
        {
            // Calling GetValues would trigger the exception
            _ = form.GetValues("data");
            Assert.True(false);
        }
        catch (HttpRequestValidationException)
        {
            var request2 = CreateRequestWithValidationAndBody(new Dictionary<string, string>() { { "data", "<script>alert(1)</script>" } });
            EnableGranularValidation(request2);
            var values = RequestDataHelper.GetNameValueCollectionValues(request.Form, "data");
            values.Should().BeNull();
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

    private static HttpRequest CreateRequestWithValidationAndBody(Dictionary<string, string> values)
    {
        var request = new HttpRequest("file", "http://localhost/benchmarks", string.Empty);
        var field = request.Form.GetType().BaseType.BaseType.GetField("_readOnly", BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(request.Form, false);

        foreach (var pair in values)
        {
            request.Form.Add(pair.Key, pair.Value);
        }

        request.ValidateInput();
        return request;
    }

    private static void EnableGranularValidation(HttpRequest request)
    {
        var flagsType = request.GetType().GetField("_flags", BindingFlags.NonPublic | BindingFlags.Instance);
        var flags = flagsType.GetValue(request);
        var setMethod = flagsType.GetValue(request).GetType().GetMethod("Set", BindingFlags.NonPublic | BindingFlags.Instance);
        setMethod.Invoke(flags, new object[] { 1073741824 | 2 });
        flagsType.SetValue(request, flags);
    }
}
#endif
