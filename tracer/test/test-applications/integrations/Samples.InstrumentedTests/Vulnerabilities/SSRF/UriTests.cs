using System;
using System.Collections.Specialized;
using System.Net;
using RestSharp;
using Moq;
using System.Net.Http;
using System.Threading;
using Xunit;
using FluentAssertions;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class UriTests : SSRFTests
{
    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted()
    {
        Uri uri = new Uri(taintedUrlValue);
        AssertTainted(uri.OriginalString);
    }

    [Obsolete("Testing")]
    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted2()
    {
        Uri uri = new Uri(taintedUrlValue, true);
        AssertTainted(uri.OriginalString);
    }

    [Obsolete("Testing")]
    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted3()
    {
        Uri uri = new Uri(new Uri(notTaintedValue), taintedUrlValue, true);
        AssertTainted(uri.OriginalString);
    }

    [Obsolete("Testing")]
    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted4()
    {
        Uri uri = new Uri(new Uri(taintedUrlValue), "eee", true);
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted5()
    {
        Uri uri = new Uri(new Uri(taintedUrlValue), "eee");
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted6()
    {
        Uri uri = new Uri(new Uri(notTaintedValue), taintedUrlValue);
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted7()
    {
        Uri uri = new Uri(taintedUrlValue, UriKind.Absolute);
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted8()
    {
        Uri uri = new Uri(new Uri(notTaintedValue), new Uri(taintedUrlValue));
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted9()
    {
        Uri uri = new Uri(new Uri(taintedUrlValue), new Uri(notTaintedValue));
        AssertTainted(uri.OriginalString);
    }

    [Obsolete]
    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted10()
    {
        Uri uri = new Uri(taintedUrlValue, true);
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted11()
    {
        Uri uri = new Uri(taintedUrlValue, UriKind.Absolute);
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted13()
    {
        Uri uri = new Uri(new Uri(taintedUrlValue), new Uri(notTaintedHost));
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted14()
    {
        Uri uri = new Uri(new Uri(notTaintedValue), new Uri(taintedHost));
        AssertTainted(uri.OriginalString);
    }

#if NET6_0_OR_GREATER
    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted15()
    {
        Uri uri = new Uri(taintedUrlValue, new UriCreationOptions());
        AssertTainted(uri.OriginalString);
        HttpClient client = new HttpClient();
    }

    [Fact]
    public void GivenAURI_WhenTryCreateFromtainted_IsTainted()
    {
        Uri uri;
        bool result = Uri.TryCreate(taintedUrlValue, new UriCreationOptions(), out uri);
        result.Should().BeTrue();
        AssertTainted(uri.OriginalString);
    }
#endif

    [Fact]
    public void GivenAURI_WhenTryCreateFromtainted_IsTainted2()
    {
        Uri uri;
        bool result = Uri.TryCreate(taintedUrlValue, UriKind.Absolute, out uri);
        result.Should().BeTrue();
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenTryCreateFromtainted_IsTainted3()
    {
        Uri uri;
        bool result = Uri.TryCreate(new Uri(taintedUrlValue), "relative", out uri);
        result.Should().BeTrue();
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenTryCreateFromtainted_IsTainted4()
    {
        Uri uri;
        bool result = Uri.TryCreate(new Uri(notTaintedValue), taintedHost, out uri);
        result.Should().BeTrue();
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenTryCreateFromtainted_IsTainted5()
    {
        Uri uri;
        bool result = Uri.TryCreate(new Uri(taintedUrlValue), new Uri("relative"), out uri);
        result.Should().BeTrue();
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenTryCreateFromtainted_IsTainted6()
    {
        Uri uri;
        bool result = Uri.TryCreate(new Uri(notTaintedValue), new Uri(taintedHost), out uri);
        result.Should().BeTrue();
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenUnescapeDataStringFromtainted_IsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            Uri.UnescapeDataString(taintedUrlValue),
            () => Uri.UnescapeDataString(taintedUrlValue));
    }

    [Obsolete]
    [Fact]
    public void GivenAURI_WhenEscapeUriStringFromtainted_IsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            Uri.EscapeUriString(taintedUrlValue),
            () => Uri.EscapeUriString(taintedUrlValue));
    }

    [Fact]
    public void GivenAURI_WhenEscapeDataStringFromtainted_IsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            Uri.EscapeDataString(taintedUrlValue),
            () => Uri.EscapeDataString(taintedUrlValue));
    }
}
