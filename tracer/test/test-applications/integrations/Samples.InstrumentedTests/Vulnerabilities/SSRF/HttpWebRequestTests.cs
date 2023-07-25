using System;
using Xunit;
using System.Net;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class HttpWebRequestTests : SSRFTests
{
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


    [Fact]
    public void GivenAHttpWebRequest_WhenCreated_Vulnerable2()
    {
        HttpWebRequest.Create(new Uri(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpWebRequest_WhenCreateDefault_Vulnerable()
    {
        HttpWebRequest.CreateDefault(new Uri(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpWebRequest_WhenCreateHttp_Vulnerable()
    {
        HttpWebRequest.CreateHttp(new Uri(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpWebRequest_WhenCreateHttp_Vulnerable2()
    {
        HttpWebRequest.CreateHttp(taintedUrlValue);
        AssertVulnerableSSRF();
    }
}

