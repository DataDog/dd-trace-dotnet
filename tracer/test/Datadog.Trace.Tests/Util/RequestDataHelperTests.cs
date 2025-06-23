// <copyright file="RequestDataHelperTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETFRAMEWORK
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web;
using System.Web.Hosting;
using Datadog.Trace.Agent;
using Datadog.Trace.AppSec;
using Datadog.Trace.AppSec.Coordinator;
using Datadog.Trace.Configuration;
using Datadog.Trace.Iast;
using Datadog.Trace.Sampling;
using Datadog.Trace.Util;
using FluentAssertions;
using Moq;
using Xunit;
using Xunit.Sdk;

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
    public void GivenADangerousBody_WhenGetKeys_NoException()
    {
        var request = CreateRequestWithValidationAndBody(new Dictionary<string, string>() { { "data", "<script>alert(1)</script>" } }, false);
        var form = request.Form;
        request.ValidateInput();
        // Getting keys should not throw an exception
        _ = form.Keys;
        EnableGranularValidation(request);
        _ = request.Form.Keys;
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
    public void BuildUrl_WhenUrlHasNotBeenAccessed_ShouldReturnUrlAndResetUrlField()
    {
        var request = CreateHttpRequest("GET");
        var urlField = typeof(HttpRequest).GetField("_url", BindingFlags.NonPublic | BindingFlags.Instance);

        // Ensure _url is null before the test
        // this will mean that Url will need to be built
        urlField.GetValue(request).Should().BeNull();

        var url = RequestDataHelper.BuildUrl(request);

        url.Should().NotBeNull();
        url.ToString().Should().Be("http://127.0.0.1/test/test.aspx");

        // Verify that _url was reset to null
        var urlValueAfter = urlField.GetValue(request) as Uri;
        urlValueAfter.Should().BeNull();
    }

    [Fact]
    public void BuildUrl_WhenUrlIsResetAndHttpRequestIsModified_ShouldReturnUpdatedUrl()
    {
        var initialHost = "localhost";
        var newHost = "example.com";

        // Create a TestWorkerRequest with the initial host
        var workerRequest = new TestWorkerRequest("/test", null, new StringWriter(), initialHost);
        var context = new HttpContext(workerRequest);
        var request = context.Request;

        var urlField = typeof(HttpRequest).GetField("_url", BindingFlags.NonPublic | BindingFlags.Instance);

        // Ensure _url is null before the test
        // this will mean that Url will need to be built
        urlField.SetValue(request, null);

        // simulate tracer calling HttpRequest.Url
        // Call BuildUrl to build the URL and ensure it doesn't get cached
        var initialUrl = RequestDataHelper.BuildUrl(request);

        // Modify the HttpRequest object by changing the host
        // This is emulating some middleware that is running after us
        workerRequest.SetHost(newHost);

        // Reset _url to ensure the URL will be rebuilt
        urlField.SetValue(request, null);

        // Call request.Url to get the updated URL
        // This is emulating someone access the HttpRequest.Url directly
        // after they have modified it and after we have already accessed it
        // this means that there will be a disconnect between the values
        // that we get and that they get
        // we get old outdated values that may not perfectly represent their URL
        // but they don't have to worry about their URL not having the changes take effect
        var updatedUrl = request.Url;
        var updatedUrlString = updatedUrl?.ToString();

        initialUrl.Should().NotBeNull(); // what tracer would get:   http://localhost/test/test.aspx
        updatedUrl.Should().NotBeNull(); // what customer would get: http://example.com/test/test.aspx
        initialUrl.ToString().Should().NotBe(updatedUrlString);
        updatedUrl.ToString().Should().Contain(newHost);
    }

    [Fact]
    public void GetUrl_CachesInitialHttpRequestUrl_ShouldReturnCachedUrl()
    {
        var initialHost = "localhost";
        var newHost = "example.com";

        // Create a TestWorkerRequest with the initial host
        var workerRequest = new TestWorkerRequest("/test", null, new StringWriter(), initialHost);
        var context = new HttpContext(workerRequest);
        var request = context.Request;

        var urlField = typeof(HttpRequest).GetField("_url", BindingFlags.NonPublic | BindingFlags.Instance);

        // Ensure _url is null before the test
        // this will mean that Url will need to be built
        urlField.SetValue(request, null);

        // simulate tracer calling HttpRequest.Url
        // Call GetUrl to cause the HttpRequest to build the URL and cache the result
        var initialUrl = RequestDataHelper.GetUrl(request); // this would be http://localhost/test/test.aspx

        // Modify the HttpRequest object by changing the host
        // This is emulating some middleware that is running after us
        workerRequest.SetHost(newHost); // expected URL would be http://example.com/test/test.aspx

        // Note: we aren't resetting the private _url field of the HTTPRequest here to demonstrate the caching behavior

        // directly call request.Url to emulate what a customer may do
        // they would expect to get the updated URL (example.com)
        // but instead they'd get the cached URL (localhost)
        var updatedUrl = request.Url;
        var updatedUrlString = updatedUrl?.ToString();

        initialUrl.Should().NotBeNull(); // what tracer would get:   http://localhost/test/test.aspx
        updatedUrl.Should().NotBeNull(); // what customer would get: http://localhost/test/test.aspx
        // NOTE: the customer would ideally get http://example.com/test/test.aspx
        initialUrl.ToString().Should().Be(updatedUrlString);
        updatedUrl.ToString().Should().NotContain(newHost);
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
        var security = new Security(null, null, null);
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

    private static HttpRequest CreateRequestWithValidationAndBody(Dictionary<string, string> values, bool validate = true)
    {
        var request = new HttpRequest("file", "http://localhost/benchmarks", string.Empty);
        var field = request.Form.GetType().BaseType.BaseType.GetField("_readOnly", BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(request.Form, false);

        foreach (var pair in values)
        {
            request.Form.Add(pair.Key, pair.Value);
        }

        if (validate)
        {
            request.ValidateInput();
        }

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

    private HttpRequest CreateHttpRequest(string httpMethod)
    {
        var workerRequest = new SimpleWorkerRequest("/test", "/test", "test.aspx", null, new StringWriter());
        var context = new HttpContext(workerRequest);
        var request = context.Request;

        return request;
    }

    public class TestWorkerRequest : SimpleWorkerRequest
    {
        private readonly string _protocol;
        private string _host;

        public TestWorkerRequest(string page, string query, TextWriter output, string host, string protocol = "http")
            : base(page, page, "test.aspx", query, output)
        {
            _host = host;
            _protocol = protocol;
        }

        public override string GetServerName()
        {
            return _host;
        }

        public override string GetProtocol()
        {
            return _protocol;
        }

        public void SetHost(string host)
        {
            _host = host;
        }
    }
}
#endif
