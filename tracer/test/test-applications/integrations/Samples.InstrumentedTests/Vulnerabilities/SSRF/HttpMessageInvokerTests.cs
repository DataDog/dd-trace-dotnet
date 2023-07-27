using System.Net.Http;
using System.Threading;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class HttpMessageInvokerTests : SSRFTests
{
    //HttpMessageInvoker

#if NET5_0_OR_GREATER

    // Test [AspectMethodInsertBefore("System.Net.Http.HttpMessageInvoker::Send(System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpMessageInvoker_WhenSend_VulnerabilityIsLoged()
    {
        var httpMessageInvoker = new HttpMessageInvoker(new HttpClientHandler());
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteFunc(() => httpMessageInvoker.Send(message, CancellationToken.None));
        AssertVulnerableSSRF();
    }

#endif

    // Test [AspectMethodInsertBefore("System.Net.Http.HttpMessageInvoker::SendAsync(System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpMessageInvoker_WhenSendAsync_VulnerabilityIsLoged()
    {
        var httpMessageInvoker = new HttpMessageInvoker(new HttpClientHandler());
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteFunc(() => httpMessageInvoker.SendAsync(message, CancellationToken.None).Result);
        AssertVulnerableSSRF();
    }
}
