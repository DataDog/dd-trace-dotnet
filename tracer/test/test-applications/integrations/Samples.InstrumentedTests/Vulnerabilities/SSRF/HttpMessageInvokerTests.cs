#if NET5_0_OR_GREATER
using System.Net.Http;
using System.Threading;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class HttpMessageInvokerTests : SSRFTests
{
    //HttpMessageInvoker

    [Fact]
    public void GivenAHttpMessageInvoker_WhenSend_VulnerabilityIsLoged()
    {
        var httpMessageInvoker = new HttpMessageInvoker(new HttpClientHandler());
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        httpMessageInvoker.Send(message, CancellationToken.None);
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpMessageInvoker_WhenSendAsync_VulnerabilityIsLoged()
    {
        var httpMessageInvoker = new HttpMessageInvoker(new HttpClientHandler());
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        _ = httpMessageInvoker.SendAsync(message, CancellationToken.None).Result;
        AssertVulnerableSSRF();
    }
}
#endif
