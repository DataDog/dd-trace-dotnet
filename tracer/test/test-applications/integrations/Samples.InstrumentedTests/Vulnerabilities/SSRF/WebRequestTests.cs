using System;
using System.Net;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class WebRequestTests : SSRFTests
{
    // Test [AspectMethodInsertBefore("System.Net.WebRequest::CreateHttp(System.String)")]

    [Fact]
    public void GivenAWebRequest_WhenCreateHttp_Vulnerable()
    {
        WebRequest.CreateHttp(taintedUrlValue);
        AssertVulnerableSSRF();
    }

    // Test [AspectMethodInsertBefore("System.Net.WebRequest::Create(System.String)")]

    [Fact]
    public void GivenAWebRequest_WhenCreateHttp_Vulnerable2()
    {
        WebRequest.Create(taintedUrlValue);
        AssertVulnerableSSRF();
    }

    // Test [AspectMethodInsertBefore("System.Net.WebRequest::Create(System.Uri)")]

    [Fact]
    public void GivenAWebRequest_WhenCreateHttp_Vulnerable3()
    {
        WebRequest.Create(new Uri(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Test [AspectMethodInsertBefore("System.Net.WebRequest::CreateDefault(System.Uri)")]

    [Fact]
    public void GivenAWebRequest_WhenCreateHttp_Vulnerable4()
    {
        WebRequest.CreateDefault(new Uri(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Test [AspectMethodInsertBefore("System.Net.WebRequest::CreateHttp(System.Uri)")]

    [Fact]
    public void GivenAWebRequest_WhenCreateHttp_Vulnerable5()
    {
        WebRequest.CreateHttp(new Uri(taintedUrlValue));
        AssertVulnerableSSRF();
    }
}

