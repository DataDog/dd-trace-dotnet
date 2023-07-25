using System;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class UriBuilderTests : SSRFTests
{
    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted2()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue, "host");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted3()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, taintedHost);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted4()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue, "host", 22);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted5()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, taintedHost, 33);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted6()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, taintedHost, 33, "");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted7()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue, notTaintedHost, 33, "");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }


    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted8()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, notTaintedHost, 33, taintedUrlValue);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted9()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, notTaintedHost, 33, taintedUrlValue, "");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted10()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, notTaintedHost, 33, notTaintedValue, "?eee=" + taintedHost);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted11()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, taintedHost, 33, "", "");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted12()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue, notTaintedHost, 33, notTaintedValue, "");
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted13()
    {
        UriBuilder builder = new UriBuilder(new Uri(taintedUrlValue));
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenGetSensitiveProperty_IsTainted()
    {
        UriBuilder builder = new UriBuilder(new Uri(taintedUrlValue));
        AssertTainted(builder.Query);
    }

    [Fact]
    public void GivenAUriBuilder_WhenGetSensitiveProperty_IsTainted2()
    {
        UriBuilder builder = new UriBuilder(new Uri(taintedUrlValue));
        AssertTainted(builder.Host);
    }

    [Fact]
    public void GivenAUriBuilder_WhenGetSensitiveProperty_IsTainted3()
    {
        UriBuilder builder = new UriBuilder(new Uri(taintedUrlValue));
        AssertTainted(builder.Path);
    }

    [Fact]
    public void GivenAUriBuilder_WhenSetSensitiveProperty_IsTainted14()
    {
        UriBuilder builder = new UriBuilder(new Uri(notTaintedValue));
        builder.Query = taintedQuery;
        AssertTainted(builder.ToString());
    }

    [Fact]
    public void GivenAUriBuilder_WhenSetSensitiveProperty_IsTainted15()
    {
        UriBuilder builder = new UriBuilder(new Uri(notTaintedValue));
        builder.Path = taintedUrlValue;
        AssertTainted(builder.ToString());
    }

    [Fact]
    public void GivenAUriBuilder_WhenSetSensitiveProperty_IsTainted17()
    {
        UriBuilder builder = new UriBuilder(new Uri(notTaintedValue));
        builder.Host = taintedHost;
        AssertTainted(builder.ToString());
    }
}

