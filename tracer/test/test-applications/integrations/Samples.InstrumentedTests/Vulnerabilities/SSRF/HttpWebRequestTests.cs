using System;
using Xunit;
using System.Net;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class HttpWebRequestTests : SSRFTests
{
    // Test [AspectMethodInsertBefore("System.Net.WebRequest::Create(System.String)")]

    [Fact]
    public void GivenAHttpWebRequest_WhenGetResponseTaintedURL_VulnerabilityIsLoged()
    {
        HttpWebRequest.Create(taintedUrlValue);
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpWebRequest_WhenGetResponseAsyncTaintedURL_VulnerabilityIsLoged()
    {
        var request = HttpWebRequest.Create(taintedUrlValue);
        request.GetResponseAsync();
        AssertVulnerableSSRF();
    }

    // Test [AspectMethodInsertBefore("System.Net.WebRequest::Create(System.Uri)")]

    [Fact]
    public void GivenAHttpWebRequest_WhenCreated_Vulnerable2()
    {
        HttpWebRequest.Create(new Uri(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Test [AspectMethodInsertBefore("System.Net.WebRequest::CreateDefault(System.Uri)")]

    [Fact]
    public void GivenAHttpWebRequest_WhenCreateDefault_Vulnerable()
    {
        HttpWebRequest.CreateDefault(new Uri(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Test [AspectMethodInsertBefore("System.Net.WebRequest::CreateHttp(System.Uri)")]

    [Fact]
    public void GivenAHttpWebRequest_WhenCreateHttp_Vulnerable()
    {
        HttpWebRequest.CreateHttp(new Uri(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Test [AspectMethodInsertBefore("System.Net.WebRequest::CreateHttp(System.String)")]

    [Fact]
    public void GivenAHttpWebRequest_WhenCreateHttp_Vulnerable2()
    {
        HttpWebRequest.CreateHttp(taintedUrlValue);
        AssertVulnerableSSRF();
    }
}

