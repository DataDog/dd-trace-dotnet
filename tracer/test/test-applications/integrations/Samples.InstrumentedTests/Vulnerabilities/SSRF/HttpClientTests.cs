using System;
using Xunit;
using System.Threading;
using Moq;
using System.Net.Http;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class HttpClientTests : SSRFTests
{
    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStringAsync(System.String)")]

    [Fact]
    public void GivenAHttpClient_WhenDownloadString_Vulnerable()
    {
        ExecuteFunc(() => new HttpClient().GetStringAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetByteArrayAsync(System.String)")]

    [Fact]
    public void GivenAHttpClient_WhenGetByteArrayAsync_Vulnerable()
    {
        ExecuteFunc(() => new HttpClient().GetByteArrayAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStreamAsync(System.String,System.Threading.CancellationToken)", 1)]

#if NET5_0_OR_GREATER
    [Fact]
    public void GivenAHttpClient_WhenGetStreamAsync_Vulnerable3()
    {
        ExecuteFunc(() => new HttpClient().GetStreamAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }
#endif

#if NETCOREAPP

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PatchAsync(System.String,System.Net.Http.HttpContent) ", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetPatchAsync_Vulnerable4()
    {
        ExecuteFunc(() => new HttpClient().PatchAsync(taintedUrlValue, new StringContent("content")));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PatchAsync(System.Uri,System.Net.Http.HttpContent)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetPatchAsync_Vulnerable3()
    {
        ExecuteFunc(() => new HttpClient().PatchAsync(new Uri(taintedUrlValue), new StringContent("content")));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PatchAsync(System.String,System.Net.Http.HttpContent,System.Threading.CancellationToken) ", 2)]

    [Fact]
    public void GivenAHttpClient_WhenGetPatchAsync_Vulnerable5()
    {
        ExecuteFunc(() => new HttpClient().PatchAsync(taintedUrlValue, new StringContent("content"), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PatchAsync(System.Uri,System.Net.Http.HttpContent,System.Threading.CancellationToken)", 2)]

    [Fact]
    public void GivenAHttpClient_WhenGetPatchAsync_Vulnerable6()
    {
        ExecuteFunc(() => new HttpClient().PatchAsync(new Uri(taintedUrlValue), new StringContent("content"), CancellationToken.None));
        AssertVulnerableSSRF();
    }
#endif

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStreamAsync(System.String)")]

    [Fact]
    public void GivenAHttpClient_WhenGetStreamAsync_Vulnerable()
    {
        ExecuteFunc(() => new HttpClient().GetStreamAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.String)")]

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable()
    {
        ExecuteFunc(() => new HttpClient().GetAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.String,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable9()
    {
        ExecuteFunc(() => new HttpClient().GetAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.String,System.Net.Http.HttpCompletionOption)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable3()
    {
        ExecuteFunc(() => new HttpClient().GetAsync(taintedUrlValue, HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.String,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable5()
    {
        ExecuteFunc(() => new HttpClient().GetAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.Uri)")]

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable2()
    {
        ExecuteFunc(() => new HttpClient().GetAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.Uri,System.Net.Http.HttpCompletionOption)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable4()
    {
        ExecuteFunc(() => new HttpClient().GetAsync(new Uri(taintedUrlValue), HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.Uri,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable6()
    {
        ExecuteFunc(() => new HttpClient().GetAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.Uri,System.Net.Http.HttpCompletionOption,System.Threading.CancellationToken)", 2)]

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable8()
    {
        ExecuteFunc(() => new HttpClient().GetAsync(new Uri(taintedUrlValue), HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetAsync(System.String,System.Net.Http.HttpCompletionOption,System.Threading.CancellationToken)", 2)]

    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable7()
    {
        ExecuteFunc(() => new HttpClient().GetAsync(taintedUrlValue, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PostAsync(System.String,System.Net.Http.HttpContent)", 1)]


    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable()
    {
        ExecuteFunc(() => new HttpClient().PostAsync(taintedUrlValue, new Mock<HttpContent>().Object));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PostAsync(System.String,System.Net.Http.HttpContent,System.Threading.CancellationToken)", 2)]

    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable3()
    {
        ExecuteFunc(() => new HttpClient().PostAsync(taintedUrlValue, new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PostAsync(System.Uri,System.Net.Http.HttpContent)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable2()
    {
        ExecuteFunc(() => new HttpClient().PostAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PostAsync(System.Uri,System.Net.Http.HttpContent,System.Threading.CancellationToken)", 2)]

    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable4()
    {
        ExecuteFunc(() => new HttpClient().PostAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerableSSRF();
    }


    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PutAsync(System.Uri,System.Net.Http.HttpContent)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable2()
    {
        ExecuteFunc(() => new HttpClient().PutAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PutAsync(System.Uri,System.Net.Http.HttpContent,System.Threading.CancellationToken)", 2)]

    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable4()
    {
        ExecuteFunc(() => new HttpClient().PutAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PutAsync(System.String,System.Net.Http.HttpContent)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable()
    {
        ExecuteFunc(() => new HttpClient().PutAsync(taintedUrlValue, new Mock<HttpContent>().Object));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::PutAsync(System.String,System.Net.Http.HttpContent,System.Threading.CancellationToken)", 2)]

    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable3()
    {
        ExecuteFunc(() => new HttpClient().PutAsync(taintedUrlValue, new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::DeleteAsync(System.String)")]

    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable()
    {
        ExecuteFunc(() => new HttpClient().DeleteAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::DeleteAsync(System.String,System.Threading.CancellationToken)", 1)


    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable3()
    {
        ExecuteFunc(() => new HttpClient().DeleteAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::DeleteAsync(System.Uri)")]

    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable2()
    {
        ExecuteFunc(() => new HttpClient().DeleteAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::DeleteAsync(System.Uri,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable4()
    {
        ExecuteFunc(() => new HttpClient().DeleteAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetByteArrayAsync(System.Uri)")]

    [Fact]
    public void GivenAHttpClient_WhenGetByteArrayAsync_Vulnerable2()
    {
        ExecuteFunc(() => new HttpClient().GetByteArrayAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

#if NET5_0_OR_GREATER

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetByteArrayAsync(System.String,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetByteArrayAsync_Vulnerable3()
    {
        ExecuteFunc(() => new HttpClient().GetByteArrayAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetByteArrayAsync(System.Uri,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetByteArrayAsync_Vulnerable7()
    {
        ExecuteFunc(() => new HttpClient().GetByteArrayAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }
#endif

#if NET5_0_OR_GREATER

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStreamAsync(System.Uri,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetStreamAsync_Vulnerable4()
    {
        ExecuteFunc(() => new HttpClient().GetStreamAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStringAsync(System.Uri,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetStringAsync_Vulnerable3()
    {
        ExecuteFunc(() => new HttpClient().GetStringAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStringAsync(System.String,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenGetStringAsync_Vulnerable2()
    {
        ExecuteFunc(() => new HttpClient().GetStringAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerableSSRF();
    }
#endif

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStringAsync(System.Uri)")]

    [Fact]
    public void GivenAHttpClient_WhenGetStringAsync_Vulnerable()
    {
        ExecuteFunc(() => new HttpClient().GetStringAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::GetStreamAsync(System.Uri)")]

    [Fact]
    public void GivenAHttpClient_WhenGetStreamAsync_Vulnerable2()
    {
        ExecuteFunc(() => new HttpClient().GetStreamAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::set_BaseAddress(System.Uri)")]

    [Fact]
    public void GivenAURI_WhenSetBaseAddress_IsVulnerable()
    {
        HttpClient client = new HttpClient();
        client.BaseAddress = new Uri(taintedUrlValue);
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::SendAsync(System.Net.Http.HttpRequestMessage)")]

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable()
    {
        ExecuteFunc(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_VulnerableUri()
    {
        ExecuteFunc(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue))));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::SendAsync(System.Net.Http.HttpRequestMessage,System.Net.Http.HttpCompletionOption)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable2()
    {
        ExecuteFunc(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue), HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable5()
    {
        ExecuteFunc(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue)), HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::SendAsync(System.Net.Http.HttpRequestMessage,System.Net.Http.HttpCompletionOption,System.Threading.CancellationToken)", 2)]


    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable3()
    {
        ExecuteFunc(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue), HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable6()
    {
        var taintedUrlValue2 = taintedUrlValue + "ewewew";
        ExecuteFunc(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue)), HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::SendAsync(System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable4()
    {
        ExecuteFunc(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue), CancellationToken.None));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable7()
    {
        ExecuteFunc(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue)), CancellationToken.None));
        AssertVulnerableSSRF();
    }

#if NET5_0_OR_GREATER

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::Send(System.Net.Http.HttpRequestMessage,System.Net.Http.HttpCompletionOption,System.Threading.CancellationToken)", 2)]

    [Fact]
    public void GivenAURI_WhenSendFromtainted_Vulnerable()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteFunc(() => client.Send(message, HttpCompletionOption.ResponseContentRead, CancellationToken.None));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::Send(System.Net.Http.HttpRequestMessage)")]

    [Fact]
    public void GivenAURI_WhenSendFromtainted_Vulnerable2()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteFunc(() => client.Send(message));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpClient::Send(System.Net.Http.HttpRequestMessage,System.Net.Http.HttpCompletionOption)", 1)]

    [Fact]
    public void GivenAURI_WhenSendFromtainted_Vulnerable3()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteFunc(() => client.Send(message, HttpCompletionOption.ResponseContentRead));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpMessageInvoker::Send(System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAURI_WhenSendFromtainted_Vulnerable4()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteFunc(() => client.Send(message, CancellationToken.None));
        AssertVulnerableSSRF();
    }
#endif

    // Testing [AspectMethodInsertBefore("System.Net.Http.HttpMessageInvoker::SendAsync(System.Net.Http.HttpRequestMessage,System.Threading.CancellationToken)", 1)]

    [Fact]
    public void GivenAURI_WhenSendAsyncFromtainted_IsVulnerable()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        client.SendAsync(message, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
        AssertVulnerableSSRF();
    }
}

