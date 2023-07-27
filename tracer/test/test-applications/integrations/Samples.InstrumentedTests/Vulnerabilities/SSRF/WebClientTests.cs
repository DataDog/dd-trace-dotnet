#if NETCOREAPP
using System;
using System.Collections.Specialized;
using System.Net;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class WebClientTests : SSRFTests
{
    protected static WebClient webclient = new WebClient();

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadData(System.Uri)")]

    [Fact]
    public void GivenAWebClient_WhenDownloadData_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadData(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadData_Exception()
    {
        Assert.Throws<NullReferenceException>(() => webclient.DownloadData((Uri) null));
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadDataAsync(System.Uri)")]

    [Fact]
    public void GivenAWebClient_WhenDownloadDataAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadDataAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadDataAsync(System.Uri,System.Object)", 1)]

    [Fact]
    public void GivenAWebClient_WhenDownloadDataAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadDataAsync(new Uri(taintedUrlValue), null));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadDataTaskAsync(System.Uri)")]

    [Fact]
    public void GivenAWebClient_WhenDownloadDataTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadDataTaskAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadFile(System.Uri,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenDownloadFile_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadFile(new Uri(taintedUrlValue), file));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadFileAsync(System.Uri,System.String)", 1)]


    [Fact]
    public void GivenAWebClient_WhenDownloadFileAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadFileAsync(new Uri(taintedUrlValue), file));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadFileAsync(System.Uri,System.String,System.Object)", 2)]


    [Fact]
    public void GivenAWebClient_WhenDownloadFileAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadFileAsync(new Uri(taintedUrlValue), file, null));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadFileTaskAsync(System.Uri,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenDownloadFileTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadFileTaskAsync(new Uri(taintedUrlValue), file));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadString(System.Uri)")]


    [Fact]
    public void GivenAWebClient_WhenDownloadString_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadString(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadStringAsync(System.Uri)")]


    [Fact]
    public void GivenAWebClient_WhenDownloadStringAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadStringAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadStringAsync(System.Uri,System.Object)", 1)]


    [Fact]
    public void GivenAWebClient_WhenDownloadStringAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadStringAsync(new Uri(taintedUrlValue), null));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadStringTaskAsync(System.Uri)")]

    [Fact]
    public void GivenAWebClient_WhenDownloadStringTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadStringTaskAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenRead(System.Uri)")]

    [Fact]
    public void GivenAWebClient_WhenOpenRead_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenRead(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenReadAsync(System.Uri)")]

    [Fact]
    public void GivenAWebClient_WhenOpenReadAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenReadAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenReadAsync(System.Uri,System.Object)", 1)]

    [Fact]
    public void GivenAWebClient_WhenOpenReadAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenReadAsync(new Uri(taintedUrlValue), null));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenReadTaskAsync(System.Uri)")]

    [Fact]
    public void GivenAWebClient_WhenOpenReadTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenReadTaskAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenWrite(System.Uri)")]


    [Fact]
    public void GivenAWebClient_WhenOpenWrite_Vulnerable4()
    {
        ExecuteAction(() => webclient.OpenWrite(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenWrite(System.Uri,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenOpenWrite_Vulnerable5()
    {
        ExecuteAction(() => webclient.OpenWrite(new Uri(taintedUrlValue), "GET"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteAsync(System.Uri)")]


    [Fact]
    public void GivenAWebClient_WhenOpenWriteAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenWriteAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteAsync(System.Uri,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenOpenWriteAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenWriteAsync(new Uri(taintedUrlValue), "GET"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteAsync(System.Uri,System.String,System.Object)", 2)]

    [Fact]
    public void GivenAWebClient_WhenOpenWriteAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.OpenWriteAsync(new Uri(taintedUrlValue), "GET", null));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteTaskAsync(System.Uri)")]


    [Fact]
    public void GivenAWebClient_WhenOpenWriteTaskAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.OpenWriteTaskAsync(new Uri(taintedUrlValue)));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteTaskAsync(System.Uri,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenOpenWriteTaskAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.OpenWriteTaskAsync(new Uri(taintedUrlValue), "GET"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadData(System.Uri,System.Byte[])", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadData_Vulnerable5()
    {
        ExecuteAction(() => webclient.UploadData(new Uri(taintedUrlValue), new Byte[] { }));
        AssertVulnerableSSRF();
    }
     
    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadData(System.Uri,System.String,System.Byte[])", 2)]

        [Fact]
    public void GivenAWebClient_WhenUploadData_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadData(new Uri(taintedUrlValue), "GET", new Byte[] { }));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadDataAsync(System.Uri,System.Byte[])", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadDataAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadDataAsync(new Uri(taintedUrlValue), new Byte[] { }));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadDataAsync(System.Uri,System.String,System.Byte[])", 2)]


    [Fact]
    public void GivenAWebClient_WhenUploadDataAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadDataAsync(new Uri(taintedUrlValue), "GET", new Byte[] { }));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadDataAsync(System.Uri,System.String,System.Byte[],System.Object)", 3)]

    [Fact]
    public void GivenAWebClient_WhenUploadDataAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadDataAsync(new Uri(taintedUrlValue), "GET", new Byte[] { }, null));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadDataTaskAsync(System.Uri,System.Byte[])", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadDataTaskAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadDataTaskAsync(new Uri(taintedUrlValue), new Byte[] { }));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadDataTaskAsync(System.Uri,System.String,System.Byte[])", 2)]


    [Fact]
    public void GivenAWebClient_WhenUploadDataTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadDataTaskAsync(new Uri(taintedUrlValue), "GET", new Byte[] { }));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadFile(System.Uri,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadFile_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadFile(new Uri(taintedUrlValue), file));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadFile(System.Uri,System.String,System.String)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadFile_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadFile(new Uri(taintedUrlValue), "GET", file));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadFileAsync(System.Uri,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadFileAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadFileAsync(new Uri(taintedUrlValue), file));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadFileAsync(System.Uri,System.String,System.String)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadFileAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadFileAsync(new Uri(taintedUrlValue), "GET", file));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadFileAsync(System.Uri,System.String,System.String,System.Object)", 3)]

    [Fact]
    public void GivenAWebClient_WhenUploadFileAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadFileAsync(new Uri(taintedUrlValue), "GET", file, null));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadFileTaskAsync(System.Uri,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadFileTaskAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadFileTaskAsync(new Uri(taintedUrlValue), file));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadFileTaskAsync(System.Uri,System.String,System.String)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadFileTaskAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadFileTaskAsync(new Uri(taintedUrlValue), "GET", file));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadString(System.Uri,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadString_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadString(new Uri(taintedUrlValue), "data"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadString(System.Uri,System.String,System.String)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadString_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadString(new Uri(taintedUrlValue), "GET", "data"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadStringAsync(System.Uri,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadStringAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadStringAsync(new Uri(taintedUrlValue), "data"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadStringAsync(System.Uri,System.String,System.String)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadStringAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadStringAsync(new Uri(taintedUrlValue), "GET", "data"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadStringAsync(System.Uri,System.String,System.String,System.Object)", 3)]

    [Fact]
    public void GivenAWebClient_WhenUploadStringAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadStringAsync(new Uri(taintedUrlValue), "GET", "data", null));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadStringTaskAsync(System.Uri,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadStringTaskAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadStringTaskAsync(new Uri(taintedUrlValue), "data"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadStringTaskAsync(System.Uri,System.String,System.String)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadStringTaskAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadStringTaskAsync(new Uri(taintedUrlValue), "GET", "data"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadValues(System.Uri,System.Collections.Specialized.NameValueCollection)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadValues_Vulnerable5()
    {
        ExecuteAction(() => webclient.UploadValues(new Uri(taintedUrlValue), new NameValueCollection()));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadValues(System.Uri,System.String,System.Collections.Specialized.NameValueCollection)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadValues_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadValues(new Uri(taintedUrlValue), "GET", new NameValueCollection()));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesAsync(System.Uri,System.Collections.Specialized.NameValueCollection)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadValuesAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadValuesAsync(new Uri(taintedUrlValue), new NameValueCollection()));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesAsync(System.Uri,System.String,System.Collections.Specialized.NameValueCollection)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadValuesAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadValuesAsync(new Uri(taintedUrlValue), "GET", new NameValueCollection()));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesAsync(System.Uri,System.String,System.Collections.Specialized.NameValueCollection,System.Object)", 3)]

    [Fact]
    public void GivenAWebClient_WhenUploadValuesAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadValuesAsync(new Uri(taintedUrlValue), "GET", new NameValueCollection(), null));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesTaskAsync(System.Uri,System.Collections.Specialized.NameValueCollection)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadValuesTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadValuesTaskAsync(new Uri(taintedUrlValue), new NameValueCollection()));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesTaskAsync(System.Uri,System.String,System.Collections.Specialized.NameValueCollection)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadValuesTaskAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadValuesTaskAsync(new Uri(taintedUrlValue), "GET", new NameValueCollection()));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadData(System.String)")]

    [Fact]
    public void GivenAWebClient_WhenDownloadData_Vulnerable2()
    {
        ExecuteAction(() => webclient.DownloadData(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAWebClient_WhenDownloadData_Exception2()
    {
        Assert.Throws<ArgumentNullException>(() => webclient.DownloadData((string)null));
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadDataTaskAsync(System.String)")]

    [Fact]
    public void GivenAWebClient_WhenDownloadDataTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadDataTaskAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadFile(System.String,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenDownloadFile_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadFile(taintedUrlValue, file));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadFileTaskAsync(System.String,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenDownloadFileTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadFileTaskAsync(taintedUrlValue, file));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadString(System.String)")]

    [Fact]
    public void GivenAWebClient_WhenDownloadString_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadString(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::DownloadStringTaskAsync(System.String)")]

    [Fact]
    public void GivenAWebClient_WhenDownloadStringTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.DownloadStringTaskAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenRead(System.String)")]

    [Fact]
    public void GivenAWebClient_WhenOpenRead_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenRead(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenReadTaskAsync(System.String)")]

    [Fact]
    public void GivenAWebClient_WhenOpenReadTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenReadTaskAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenWrite(System.String)")]

    [Fact]
    public void GivenAWebClient_WhenOpenWrite_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenWrite(taintedUrlValue));
        AssertVulnerableSSRF();
    }
    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenWrite(System.String,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenOpenWrite_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenWrite(taintedUrlValue, "GET"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::set_BaseAddress(System.String)")]

    [Fact]
    public void GivenAWebClient_WhenSetBaseAddress_Vulnerable4()
    {
        webclient.BaseAddress = taintedUrlValue;
        AssertVulnerableSSRF();
    }


    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteTaskAsync(System.String)")]


    [Fact]
    public void GivenAWebClient_WhenOpenWriteTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.OpenWriteTaskAsync(taintedUrlValue));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::OpenWriteTaskAsync(System.String,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenOpenWriteTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.OpenWriteTaskAsync(taintedUrlValue, "GET"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadData(System.String,System.Byte[])", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadData_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadData(taintedUrlValue, new Byte[] { }));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadData(System.String,System.String,System.Byte[])", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadData_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadData(taintedUrlValue, "GET", new Byte[] { }));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadData_Vulnerable3Builder()
    {
        var urlText = new UriBuilder(taintedUrlValue).ToString();
        ExecuteAction(() => webclient.UploadData(urlText, "GET", new Byte[] { }));
        AssertVulnerableSSRF(urlText);
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadDataTaskAsync(System.String,System.Byte[])", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadDataTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadDataTaskAsync(taintedUrlValue, new Byte[] { }));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadDataTaskAsync(System.String,System.String,System.Byte[])", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadDataTaskAsync_Vulnerable3()
    {
        ExecuteAction(() => webclient.UploadDataTaskAsync(taintedUrlValue, "GET", new Byte[] { }));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadFile(System.String,System.String)", 1)]


    [Fact]
    public void GivenAWebClient_WhenUploadFile_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadFile(taintedUrlValue, notTaintedHost));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFile_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadFile(notTaintedValue, taintedHost));
        AssertNotVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadFile(System.String,System.String,System.String)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadFile_Vulnerable5()
    {
        ExecuteAction(() => webclient.UploadFile(taintedUrlValue, AddTaintedString("GET"), "file"));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFile_NotVulnerable()
    {
        ExecuteAction(() => webclient.UploadFile(notTaintedValue, AddTaintedString("GET"), AddTaintedString("file")));
        AssertNotVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadFileTaskAsync(System.String,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadFileTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadFileTaskAsync(taintedUrlValue, file));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFileTaskAsync_NotVulnerable2()
    {
        ExecuteAction(() => webclient.UploadFileTaskAsync(notTaintedValue, taintedHost));
        AssertNotVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadFileTaskAsync(System.String,System.String,System.String)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadFileTaskAsync_Vulnerable5()
    {
        ExecuteAction(() => webclient.UploadFileTaskAsync(taintedUrlValue, "GET", file));
        AssertVulnerableSSRF();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFileTaskAsync_NotVulnerable6()
    {
        ExecuteAction(() => webclient.UploadFileTaskAsync(notTaintedValue, "GET", taintedHost));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAWebClient_WhenUploadFileTaskAsync_NotVulnerable7()
    {
        ExecuteAction(() => webclient.UploadFileTaskAsync(notTaintedValue, taintedHost, file));
        AssertNotVulnerable();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadString(System.String,System.String)", 1)]


    [Fact]
    public void GivenAWebClient_WhenUploadString_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadString(taintedUrlValue, "data"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadString(System.String,System.String,System.String)", 2)]


    [Fact]
    public void GivenAWebClient_WhenUploadString_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadString(taintedUrlValue, "GET", "data"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadStringTaskAsync(System.String,System.String)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadStringTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadStringTaskAsync(taintedUrlValue, "data"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadStringTaskAsync(System.String,System.String,System.String)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadStringTaskAsync_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadStringTaskAsync(taintedUrlValue, "GET", "data"));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadValues(System.String,System.Collections.Specialized.NameValueCollection)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadValues_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadValues(taintedUrlValue, new NameValueCollection()));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadValues(System.String,System.String,System.Collections.Specialized.NameValueCollection)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadValues_Vulnerable2()
    {
        ExecuteAction(() => webclient.UploadValues(taintedUrlValue, "GET", new NameValueCollection()));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesTaskAsync(System.String,System.Collections.Specialized.NameValueCollection)", 1)]

    [Fact]
    public void GivenAWebClient_WhenUploadValuesTaskAsync_Vulnerable()
    {
        ExecuteAction(() => webclient.UploadValuesTaskAsync(taintedUrlValue, new NameValueCollection()));
        AssertVulnerableSSRF();
    }

    // Testing [AspectMethodInsertBefore("System.Net.WebClient::UploadValuesTaskAsync(System.String,System.String,System.Collections.Specialized.NameValueCollection)", 2)]

    [Fact]
    public void GivenAWebClient_WhenUploadValuesTaskAsync_Vulnerable4()
    {
        ExecuteAction(() => webclient.UploadValuesTaskAsync(taintedUrlValue, "GET", new NameValueCollection()));
        AssertVulnerableSSRF();
    }
}
#endif
