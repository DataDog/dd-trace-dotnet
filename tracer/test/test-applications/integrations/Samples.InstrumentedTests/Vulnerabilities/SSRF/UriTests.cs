using System;
using Xunit;
using FluentAssertions;
#if NET6_0_OR_GREATER
using System.Net.Http;
#endif

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class UriTests : SSRFTests
{
    // Test [AspectCtorReplace("System.Uri::.ctor(System.String)", AspectFilter.StringLiterals)]

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted()
    {
        Uri uri = new Uri(taintedUrlValue);
        AssertTainted(uri.OriginalString);
    }



    // Test [AspectCtorReplace("System.Uri::.ctor(System.Uri,System.String,System.Boolean)")]

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
        Uri uri = new Uri(new Uri(taintedUrlValue), "eee", false);
        AssertTainted(uri.OriginalString);
    }

    // Test [AspectCtorReplace("System.Uri::.ctor(System.String,System.Boolean)")]

    [Obsolete("Testing")]
    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted2()
    {
        Uri uri = new Uri(taintedUrlValue, true);
        AssertTainted(uri.OriginalString);
    }

    // Test [AspectCtorReplace("System.Uri::.ctor(System.String,System.UriCreationOptions)")]

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

    // Test [AspectCtorReplace("System.Uri::.ctor(System.Uri,System.String)")]

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

    // Test [AspectCtorReplace("System.Uri::.ctor(System.String,System.UriKind)")]

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted7()
    {
        Uri uri = new Uri(taintedUrlValue, UriKind.Absolute);
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted11()
    {
        Uri uri = new Uri(taintedHost, UriKind.Relative);
        AssertTainted(uri.OriginalString);
    }

    // Test [AspectCtorReplace("System.Uri::.ctor(System.Uri,System.Uri)")]

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

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted13()
    {
        Uri uri = new Uri(new Uri(taintedUrlValue), new Uri(notTaintedHost, UriKind.Relative));
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenCreateFromtainted_IsTainted14()
    {
        Uri uri = new Uri(new Uri(notTaintedValue), new Uri(taintedHost, UriKind.Relative));
        AssertTainted(uri.OriginalString);
    }

    // Test [AspectMethodReplace("System.Uri::TryCreate(System.String,System.UriKind,System.Uri)")]

    [Fact]
    public void GivenAURI_WhenTryCreateFromtainted_IsTainted2()
    {
        Uri uri;
        bool result = Uri.TryCreate(taintedUrlValue, UriKind.Absolute, out uri);
        result.Should().BeTrue();
        AssertTainted(uri.OriginalString);
    }

    // Test [AspectMethodReplace("System.Uri::TryCreate(System.Uri,System.String,System.Uri)")]

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

    // Test [AspectMethodReplace("System.Uri::TryCreate(System.Uri,System.Uri,System.Uri)")]

    [Fact]
    public void GivenAURI_WhenTryCreateFromtainted_IsTainted5()
    {
        Uri uri = new Uri(taintedUrlValue);
        bool result = Uri.TryCreate(new Uri(taintedUrlValue), new Uri("relative", UriKind.Relative), out uri);
        result.Should().BeTrue();
        AssertTainted(uri.OriginalString);
    }

    [Fact]
    public void GivenAURI_WhenTryCreateFromtainted_IsTainted6()
    {
        Uri uri;
        bool result = Uri.TryCreate(new Uri(notTaintedValue), new Uri(taintedHost, UriKind.Relative), out uri);
        result.Should().BeTrue();
        AssertTainted(uri.OriginalString);
    }

    // Test [AspectMethodReplace("System.Uri::UnescapeDataString(System.String)")]

    [Fact]
    public void GivenAURI_WhenUnescapeDataStringFromtainted_IsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            Uri.UnescapeDataString(taintedUrlValue),
            () => Uri.UnescapeDataString(taintedUrlValue));
    }

    // Test [AspectMethodReplace("System.Uri::EscapeUriString(System.String)")]

    [Obsolete]
    [Fact]
    public void GivenAURI_WhenEscapeUriStringFromtainted_IsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            Uri.EscapeUriString(taintedUrlValue),
            () => Uri.EscapeUriString(taintedUrlValue));
    }

    // Test [AspectMethodReplace("System.Uri::EscapeDataString(System.String)")]

    [Fact]
    public void GivenAURI_WhenEscapeDataStringFromtainted_IsTainted()
    {
        AssertTaintedFormatWithOriginalCallCheck(
            Uri.EscapeDataString(taintedUrlValue),
            () => Uri.EscapeDataString(taintedUrlValue));
    }

    // Test [AspectMethodReplace("System.Uri::MakeRelative(System.Uri)")]

    [Fact]
    [Obsolete]
    public void GivenAURI_WhenMakeRelativeFromtainted_IsTainted()
    {
        Uri uri = new Uri(taintedUrlValue2);
        var uriRelative = uri.MakeRelative(new Uri(taintedUrlValue));
        uriRelative.Should().NotBeNullOrWhiteSpace();
        AssertTainted(uriRelative);
    }

    // Test [AspectMethodReplace("System.Uri::MakeRelativeUri(System.Uri)")]

    [Fact]
    public void GivenAURI_WhenMakeRelativeFromtainted_IsTainted2()
    {
        Uri uri = new Uri(taintedUrlValue2);
        var uriRelative = uri.MakeRelativeUri(new Uri(taintedUrlValue));
        uriRelative.Should().NotBeNull();
        AssertTainted(uriRelative.OriginalString);
    }

    // Test [AspectMethodReplace("System.Uri::get_AbsoluteUri()")]
    // Test [AspectMethodReplace("System.Uri::get_AbsolutePath()")]
    // Test [AspectMethodReplace("System.Uri::get_LocalPath()")]
    // Test [AspectMethodReplace("System.Uri::get_Host()")]
    // Test [AspectMethodReplace("System.Uri::get_PathAndQuery()")]
    // Test [AspectMethodReplace("System.Uri::get_Authority()")]
    // Test [AspectMethodReplace("System.Uri::get_Query()")]
    // Test [AspectMethodReplace("System.Object::ToString()", "System.Uri")]

    [Fact]
    public void GivenAUriTainted_WhengetProperties_tainted()
    {
        var uri = new Uri(taintedUrlValue);
        AssertTainted(uri.AbsoluteUri);
        AssertTainted(uri.ToString());
        AssertTainted(uri.AbsolutePath);
        AssertTainted(uri.Query);
        AssertTainted(uri.Authority);
        AssertTainted(uri.Host);
        AssertTainted(uri.LocalPath);
        AssertTainted(uri.OriginalString);
        AssertTainted(uri.PathAndQuery);
    }
}
