using System.Net;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class WebRequestTests : SSRFTests
{
    [Fact]
    public void GivenAWebRequest_WhenCreateHttp_Vulnerable()
    {
        WebRequest.CreateHttp(taintedUrlValue);
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAWebRequest_WhenCreateHttp_Vulnerable2()
    {
        WebRequest.Create(taintedUrlValue);
        AssertVulnerableSSRF();
    }
}

