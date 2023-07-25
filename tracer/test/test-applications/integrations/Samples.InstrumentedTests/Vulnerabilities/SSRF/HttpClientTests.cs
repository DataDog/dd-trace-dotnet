using System;
using Xunit;
using System.Threading;
using Moq;
using System.Net.Http;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class HttpClientTests : SSRFTests
{
    [Fact]
    public void GivenAURI_WhenSetBaseAddress_IsVulnerable()
    {
        HttpClient client = new HttpClient();
        client.BaseAddress = new Uri(taintedUrlValue);
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAURI_WhenGetAsyncFromtainted_IsVulnerable()
    {
        HttpClient client = new HttpClient();
        client.GetAsync(taintedUrlValue);
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAURI_WhenGetStreamAsyncFromtainted_IsVulnerable()
    {
        HttpClient client = new HttpClient();
        client.GetStreamAsync(taintedUrlValue);
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAURI_WhenSendAsyncFromtainted_IsVulnerable()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        client.SendAsync(message, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
        AssertVulnerableSSRF();
    }

#if NET5_0_OR_GREATER
    [Fact]
    public void GivenAURI_WhenSendFromtainted_Vulnerable()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteAction(() => client.Send(message, HttpCompletionOption.ResponseContentRead, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAURI_WhenSendFromtainted_Vulnerable2()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteAction(() => client.Send(message));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAURI_WhenSendFromtainted_Vulnerable3()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteAction(() => client.Send(message, HttpCompletionOption.ResponseContentRead));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAURI_WhenSendFromtainted_Vulnerable4()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteAction(() => client.Send(message, CancellationToken.None));
        AssertVulnerableSSRF();
    }
#endif

    [Fact]
    public void GivenAHttpClient_WhenDownloadString_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().GetStringAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

#if NET5_0_OR_GREATER

    [Fact]
    public void GivenAHttpClient_WhenGetStreamAsync_Vulnerable4()
    {
        ExecuteAction(() => new HttpClient().GetStreamAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetStringAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().GetStringAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetStringAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().GetStringAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }
#endif

    [Fact]
    public void GivenAHttpClient_WhenGetStringAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().GetStringAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetByteArrayAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().GetByteArrayAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetByteArrayAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().GetByteArrayAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

#if NET5_0_OR_GREATER
    [Fact]
    public void GivenAHttpClient_WhenGetByteArrayAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().GetByteArrayAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetByteArrayAsync_Vulnerable7()
    {
        ExecuteAction(() => new HttpClient().GetByteArrayAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }
#endif

    [Fact]
    public void GivenAHttpClient_WhenGetStreamAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().GetStreamAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

#if NETCOREAPP
    [Fact]
    public void GivenAHttpClient_WhenGetPatchAsync_Vulnerable4()
    {
        ExecuteAction(() => new HttpClient().PatchAsync(taintedUrlValue, new StringContent("content")));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetPatchAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().PatchAsync(new Uri(taintedUrlValue), new StringContent("content")));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetPatchAsync_Vulnerable5()
    {
        ExecuteAction(() => new HttpClient().PatchAsync(taintedUrlValue, new StringContent("content"), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetPatchAsync_Vulnerable6()
    {
        ExecuteAction(() => new HttpClient().PatchAsync(new Uri(taintedUrlValue), new StringContent("content"), CancellationToken.None));
        AssertVulnerableSSRF();
    }
#endif

#if NET5_0_OR_GREATER
    [Fact]
    public void GivenAHttpClient_WhenGetStreamAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().GetStreamAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }
#endif

    [Fact]
    public void GivenAHttpClient_WhenGetStreamAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().GetStreamAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().GetAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable9()
    {
        ExecuteAction(() => new HttpClient().GetAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().GetAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().GetAsync(taintedUrlValue, HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable4()
    {
        ExecuteAction(() => new HttpClient().GetAsync(new Uri(taintedUrlValue), HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable5()
    {
        ExecuteAction(() => new HttpClient().GetAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable6()
    {
        ExecuteAction(() => new HttpClient().GetAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable7()
    {
        ExecuteAction(() => new HttpClient().GetAsync(taintedUrlValue, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable8()
    {
        ExecuteAction(() => new HttpClient().GetAsync(new Uri(taintedUrlValue), HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().PostAsync(taintedUrlValue, new Mock<HttpContent>().Object));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().PostAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().PostAsync(taintedUrlValue, new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable4()
    {
        ExecuteAction(() => new HttpClient().PostAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().PutAsync(taintedUrlValue, new Mock<HttpContent>().Object));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().PutAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().PutAsync(taintedUrlValue, new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable4()
    {
        ExecuteAction(() => new HttpClient().PutAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().DeleteAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().DeleteAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().DeleteAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable4()
    {
        ExecuteAction(() => new HttpClient().DeleteAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_VulnerableUri()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue))));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue), HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue), HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable4()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable5()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue)), HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerableSSRF();
    }
    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable6()
    {
        var taintedUrlValue2 = taintedUrlValue + "ewewew";
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue)), HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable7()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue)), CancellationToken.None));
        AssertVulnerableSSRF();
    }
}

