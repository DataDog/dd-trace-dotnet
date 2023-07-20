using System;
using System.Collections.Specialized;
using System.Net;
using RestSharp;
using Moq;
using System.Net.Http;
using System.Threading;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class SSRFTests : InstrumentationTestsBase
{
    protected string notTaintedValue = "http://localhost/nottainted";
    protected string notTaintedHost = "myhost";
    protected string taintedHost = "localhost";
    protected string taintedUrlValue = "http://127.0.0.1/invalid";
    protected string file = "invalid@#file";
    protected static WebClient webclient = new WebClient();

    public SSRFTests()
    {
        AddTainted(taintedUrlValue);
        AddTainted(taintedHost);
    }

    [Fact]
    public void GivenAHttpWebRequest_WhenGetResponseTaintedURL_VulnerabilityIsLoged()
    {
        HttpWebRequest.Create(taintedUrlValue);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAHttpWebRequest_WhenGetResponseAsyncTaintedURL_VulnerabilityIsLoged()
    {
        var request = HttpWebRequest.Create(taintedUrlValue);
        request.GetResponseAsync();
        AssertVulnerable();
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted()
    {
        Uri uri = new Uri(taintedUrlValue);
        AssertTainted(uri);
    }

    [Obsolete("Testing")]
    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted2()
    {
        Uri uri = new Uri(taintedUrlValue, true);
        AssertTainted(uri);
    }

    [Obsolete("Testing")]
    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted3()
    {
        Uri uri = new Uri(new Uri(notTaintedValue), taintedUrlValue, true);
        AssertTainted(uri);
    }

    [Obsolete("Testing")]
    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted4()
    {
        Uri uri = new Uri(new Uri(taintedUrlValue), "eee", true);
        AssertTainted(uri);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted5()
    {
        Uri uri = new Uri(new Uri(taintedUrlValue), "eee");
        AssertTainted(uri);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted6()
    {
        Uri uri = new Uri(new Uri(notTaintedValue), taintedUrlValue);
        AssertTainted(uri);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted7()
    {
        Uri uri = new Uri(taintedUrlValue, UriKind.Absolute);
        AssertTainted(uri);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted8()
    {
        Uri uri = new Uri(new Uri(notTaintedValue), new Uri(taintedUrlValue));
        AssertTainted(uri);
    }

    [Fact]    
    public void GivenAURI_WhenCreateFromtainted_IsTainted9()
    {
        Uri uri = new Uri(new Uri(taintedUrlValue), new Uri(notTaintedValue));
        AssertTainted(uri);
    }

#if NET6_0_OR_GREATER
    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted10()
    {
        Uri uri = new Uri(taintedUrlValue, new UriCreationOptions());
        AssertTainted(uri);
    }
#endif

    [Fact]
    public void GivenAURI_WhenGetAsyncFromtainted_IsTainted()
    {
        HttpClient client = new HttpClient();
        client.GetAsync(taintedUrlValue);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAURI_WhenGetStreamAsyncFromtainted_IsTainted()
    {
        HttpClient client = new HttpClient();
        client.GetStreamAsync(taintedUrlValue);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAURI_WhenSendAsyncFromtainted_IsTainted()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        client.SendAsync(message, HttpCompletionOption.ResponseContentRead, CancellationToken.None);
        AssertVulnerable();
    }
#if NET5_0_OR_GREATER
    [Fact]    
    public void GivenAURI_WhenSendFromtainted_Vulnerable()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteAction(() => client.Send(message, HttpCompletionOption.ResponseContentRead, CancellationToken.None));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAURI_WhenSendFromtainted_Vulnerable2()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteAction(() => client.Send(message));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAURI_WhenSendFromtainted_Vulnerable3()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteAction(() => client.Send(message, HttpCompletionOption.ResponseContentRead));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAURI_WhenSendFromtainted_Vulnerable4()
    {
        HttpClient client = new HttpClient();
        var message = new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteAction(() => client.Send(message, CancellationToken.None));
        AssertVulnerable();
    }
#endif
    [Fact]
    public void GivenAHttpWebRequest_WhenCreated_Vulnerable2()
    {
        HttpWebRequest.Create(new Uri(taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAHttpWebRequest_WhenCreateDefault_Vulnerable()
    {
        HttpWebRequest.CreateDefault(new Uri(taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAHttpWebRequest_WhenCreateHttp_Vulnerable()
    {
        HttpWebRequest.CreateHttp(new Uri(taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAHttpWebRequest_WhenCreateHttp_Vulnerable2()
    {
        HttpWebRequest.CreateHttp(taintedUrlValue);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebRequest_WhenCreateHttp_Vulnerable()
    {
        WebRequest.CreateHttp(taintedUrlValue);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebRequest_WhenCreateHttp_Vulnerable2()
    {
        WebRequest.Create(taintedUrlValue);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadString_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadString(taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadData_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadData(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadDataAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadDataAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadDataAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadDataAsync(new Uri(taintedUrlValue), null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadDataTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadDataTaskAsync(taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadDataTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadDataTaskAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadFile_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadFile(taintedUrlValue, file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadFile_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadFile(new Uri(taintedUrlValue), file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadFileAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadFileAsync(new Uri(taintedUrlValue), file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadFileAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadFileAsync(new Uri(taintedUrlValue), file, null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadFileTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadFileTaskAsync(taintedUrlValue, file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadFileTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadFileTaskAsync(new Uri(taintedUrlValue), file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadString_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadString(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadStringAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadStringAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadStringAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadStringAsync(new Uri(taintedUrlValue), null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadStringTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadStringTaskAsync(taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadStringTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadStringTaskAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenRead_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenRead(taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenRead_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenRead(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenReadAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenReadAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenReadAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenReadAsync(new Uri(taintedUrlValue), null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenReadTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenReadTaskAsync(taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenReadTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenReadTaskAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenWrite_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenWrite(taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenWrite_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenWrite(taintedUrlValue, "GET"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenWrite_Vulnerable4()
    {
        ExecuteAction(() => webclient.OpenWrite(new Uri(taintedUrlValue), "GET"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenWriteAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenWriteAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenWriteAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenWriteAsync(new Uri(taintedUrlValue), "GET"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenWriteAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.OpenWriteAsync(new Uri(taintedUrlValue), "GET", null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenWriteTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenWriteTaskAsync(taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenWriteTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenWriteTaskAsync(taintedUrlValue, "GET"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenWriteTaskAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.OpenWriteTaskAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenOpenWriteTaskAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.OpenWriteTaskAsync(new Uri(taintedUrlValue), "GET"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadData_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadData(taintedUrlValue, new Byte[] { }));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadData_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadData(taintedUrlValue, "GET", new Byte[] { }));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadData_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadData(new Uri(taintedUrlValue), "GET", new Byte[] { }));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadDataAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadDataAsync(new Uri(taintedUrlValue), new Byte[] { }));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadDataAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadDataAsync(new Uri(taintedUrlValue), "GET", new Byte[] { }));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadDataAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadDataAsync(new Uri(taintedUrlValue), "GET", new Byte[] { }, null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadDataTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadDataTaskAsync(taintedUrlValue, new Byte[] { }));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadDataTaskAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadDataTaskAsync(taintedUrlValue, "GET", new Byte[] { }));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadDataTaskAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadDataTaskAsync(new Uri(taintedUrlValue), new Byte[] { }));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadDataTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadDataTaskAsync(new Uri(taintedUrlValue), "GET", new Byte[] { }));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFile_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadFile(taintedUrlValue, taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFile_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadFile(taintedUrlValue, "GET", file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFile_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadFile(new Uri(taintedUrlValue), file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFile_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadFile(new Uri(taintedUrlValue), "GET", file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFileAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadFileAsync(new Uri(taintedUrlValue), file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFileAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadFileAsync(new Uri(taintedUrlValue), "GET", file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFileAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadFileAsync(new Uri(taintedUrlValue), "GET", file, null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFileTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadFileTaskAsync(taintedUrlValue, file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFileTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadFileTaskAsync(taintedUrlValue, "GET", file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFileTaskAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadFileTaskAsync(new Uri(taintedUrlValue), file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFileTaskAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadFileTaskAsync(new Uri(taintedUrlValue), "GET", file));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadString_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadString(taintedUrlValue, "data"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadString_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadString(taintedUrlValue, "GET", "data"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadString_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadString(new Uri(taintedUrlValue), "GET", "data"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadStringAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadStringAsync(new Uri(taintedUrlValue), "data"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadStringAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadStringAsync(new Uri(taintedUrlValue), "GET", "data"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadStringAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadStringAsync(new Uri(taintedUrlValue), "GET", "data", null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadStringTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadStringTaskAsync(taintedUrlValue, "data"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadStringTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadStringTaskAsync(taintedUrlValue, "GET", "data"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadStringTaskAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadStringTaskAsync(new Uri(taintedUrlValue), "data"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadStringTaskAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadStringTaskAsync(new Uri(taintedUrlValue), "GET", "data"));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadValues_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadValues(taintedUrlValue, new NameValueCollection()));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadValues_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadValues(taintedUrlValue, "GET", new NameValueCollection()));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadValues_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadValues(new Uri(taintedUrlValue), "GET", new NameValueCollection()));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadValuesAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadValuesAsync(new Uri(taintedUrlValue), new NameValueCollection()));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadValuesAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadValuesAsync(new Uri(taintedUrlValue), "GET", new NameValueCollection()));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadValuesAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadValuesAsync(new Uri(taintedUrlValue), "GET", new NameValueCollection(), null));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadValuesTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadValuesTaskAsync(taintedUrlValue, new NameValueCollection()));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadValuesTaskAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadValuesTaskAsync(taintedUrlValue, "GET", new NameValueCollection()));
        System.Threading.Thread.Sleep(1000);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadValuesTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadValuesTaskAsync(new Uri(taintedUrlValue), new NameValueCollection()));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadValuesTaskAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadValuesTaskAsync(new Uri(taintedUrlValue), "GET", new NameValueCollection()));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAHttpClient_WhenDownloadString_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().GetStringAsync(taintedUrlValue));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAHttpClient_WhenGetStringAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().GetStringAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetByteArrayAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().GetByteArrayAsync(taintedUrlValue));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetByteArrayAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().GetByteArrayAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetStreamAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().GetStreamAsync(taintedUrlValue));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetStreamAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().GetStreamAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().GetAsync(taintedUrlValue));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().GetAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().GetAsync(taintedUrlValue, HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable4()
    {
        ExecuteAction(() => new HttpClient().GetAsync(new Uri(taintedUrlValue), HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable5()
    {
        ExecuteAction(() => new HttpClient().GetAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable6()
    {
        ExecuteAction(() => new HttpClient().GetAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable7()
    {
        ExecuteAction(() => new HttpClient().GetAsync(taintedUrlValue, HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenGetAsync_Vulnerable8()
    {
        ExecuteAction(() => new HttpClient().GetAsync(new Uri(taintedUrlValue), HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().PostAsync(taintedUrlValue, new Mock<HttpContent>().Object));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().PostAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().PostAsync(taintedUrlValue, new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenPostAsync_Vulnerable4()
    {
        ExecuteAction(() => new HttpClient().PostAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().PutAsync(taintedUrlValue, new Mock<HttpContent>().Object));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().PutAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().PutAsync(taintedUrlValue, new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenPutAsync_Vulnerable4()
    {
        ExecuteAction(() => new HttpClient().PutAsync(new Uri(taintedUrlValue), new Mock<HttpContent>().Object, CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().DeleteAsync(taintedUrlValue));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().DeleteAsync(new Uri(taintedUrlValue)));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().DeleteAsync(taintedUrlValue, CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenDeleteAsync_Vulnerable4()
    {
        ExecuteAction(() => new HttpClient().DeleteAsync(new Uri(taintedUrlValue), CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue)));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_VulnerableUri()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue))));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable2()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue), HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable3()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue), HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable4()
    {
        new HttpRequestMessage(HttpMethod.Get, taintedUrlValue);
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, taintedUrlValue), CancellationToken.None));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable5()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue)), HttpCompletionOption.ResponseHeadersRead));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable6()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue)), HttpCompletionOption.ResponseHeadersRead, CancellationToken.None));
        AssertVulnerable();
    }
    [Fact]
    public void GivenAHttpClient_WhenSendAsync_Vulnerable7()
    {
        ExecuteAction(() => new HttpClient().SendAsync(new HttpRequestMessage(HttpMethod.Get, new Uri(taintedUrlValue)), CancellationToken.None));
        AssertVulnerable();
    }

    [Fact]
    public void GivenARestSharpClient_WhenExecute_Vulnerable()
    {
        var client = new RestClient(taintedUrlValue);
        var request = new RestRequest("", Method.Get);
        ExecuteAction(() => client.Execute(request));
        AssertVulnerable();
    }

    [Fact]
    public void GivenARestSharpClient_WhenExecute_Vulnerable2()
    {
        var client = new RestClient(taintedUrlValue);
        var request = new RestRequest("", Method.Get);
        ExecuteAction(() => client.ExecuteAsync(request, CancellationToken.None));
        AssertVulnerable();
    }

    [Fact]
    public void GivenARestSharpClient_WhenExecute_Vulnerable3()
    {
        var client = new RestClient(taintedUrlValue);
        var request = new RestRequest("", Method.Get);
        ExecuteAction(() => client.Delete(request));
        AssertVulnerable();
    }

    [Fact]
    public void GivenARestSharpClient_WhenExecute_Vulnerable4()
    {
        var client = new RestClient(taintedUrlValue);
        var request = new RestRequest("", Method.Get);
        ExecuteAction(() => client.Post(request));
        AssertVulnerable();
    }

    [Fact]
    public void GivenARestSharpClient_WhenExecute_Vulnerable5()
    {
        var client = new RestClient(taintedUrlValue);
        var request = new RestRequest("", Method.Get);
        ExecuteAction(() => client.Get(request));
        AssertVulnerable();
    }

    [Fact]
    public void GivenAUriTainted_WhengetProperties_tainted()
    {
        var uri = new Uri(taintedUrlValue);
        AssertTainted(uri.AbsoluteUri);
        AssertTainted(uri.ToString());
        AssertTainted(uri.AbsolutePath);
        AssertTainted(uri.Scheme);
        AssertNotTainted(uri.Query); // query is empty ...
        AssertTainted(uri.Authority);
        AssertTainted(uri.Host);
        AssertTainted(uri.LocalPath);
        AssertTainted(uri.OriginalString);
        AssertTainted(uri.PathAndQuery);
        AssertNotTainted(uri.UserInfo); // user info is empty
    }

    [Fact]
    public void GivenAUriTainted_WhengetPropertiesMultipleTimes_RangesAreNotDuplicated()
    {
        var uri = new Uri(AddTainted("http://localhost/tainted?with=qs").ToString());
        AssertTainted(uri.Query);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted2()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue, "host");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted3()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, taintedHost);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted4()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue, "host", 22);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted5()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, taintedHost, 33);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted6()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, taintedHost, 33, "");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted7()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue, notTaintedHost, 33, "");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }


    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted8()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, notTaintedHost, 33, taintedUrlValue);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted9()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, notTaintedHost, 33, taintedUrlValue, "");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted10()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, notTaintedHost, 33, notTaintedValue, "?eee=" + taintedHost);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted11()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, taintedHost, 33, "", "");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted12()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue, notTaintedHost, 33, notTaintedValue, "");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted13()
    {
        UriBuilder builder = new UriBuilder(new Uri(taintedUrlValue));
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri);
    }

    void ExecuteAction(Action c)
    {
        try
        {
            c.Invoke();
        }
        catch (Exception)
        {
        }
    }
}
