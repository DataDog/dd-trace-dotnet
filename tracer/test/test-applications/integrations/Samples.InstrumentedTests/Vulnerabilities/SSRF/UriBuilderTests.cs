using System;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities.SSRF;

public class UriBuilderTests : SSRFTests
{
    // Test [AspectCtorReplace("System.UriBuilder::.ctor(System.String)")]

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    // Test [AspectCtorReplace("System.UriBuilder::.ctor(System.String,System.String)")]

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted2()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue, "host");
        // we don't taint the sheme
        AssertNotTainted(builder.ToString());
        AssertNotTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted3()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, taintedHost);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    // Test [AspectCtorReplace("System.UriBuilder::.ctor(System.String,System.String,System.Int32)")]

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted4()
    {
        UriBuilder builder = new UriBuilder(taintedUrlValue, "host", 22);
        // we don't taint the sheme
        AssertNotTainted(builder.ToString());
        AssertNotTainted(builder.Uri.OriginalString);
    }

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted5()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, taintedHost, 33);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    // Test [AspectCtorReplace("System.UriBuilder::.ctor(System.String,System.String,System.Int32,System.String)")]

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
        // we don't taint the sheme
        AssertNotTainted(builder.ToString());
        AssertNotTainted(builder.Uri.OriginalString);
    }


    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted8()
    {
        UriBuilder builder = new UriBuilder(notTaintedValue, notTaintedHost, 33, taintedUrlValue);
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }


    // Test [AspectCtorReplace("System.UriBuilder::.ctor(System.String,System.String,System.Int32,System.String,System.String)")]

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
        // we don't taint the sheme
        AssertNotTainted(builder.ToString());
        AssertNotTainted(builder.Uri.OriginalString);
    }

    // Test [AspectCtorReplace("System.UriBuilder::.ctor(System.Uri)")]

    [Fact]
    public void GivenAUriBuilder_WhenToString_IsTainted13()
    {
        UriBuilder builder = new UriBuilder(new Uri(taintedUrlValue));
        AssertTainted(builder.ToString());
        AssertTainted(builder.Uri.OriginalString);
    }

    // Test [AspectMethodReplace("System.UriBuilder::get_Query()")]

    [Fact]
    public void GivenAUriBuilder_WhenGetSensitiveProperty_IsTainted()
    {
        UriBuilder builder = new UriBuilder(new Uri(taintedUrlValue));
        AssertTainted(builder.Query);
    }

    // Test [AspectMethodReplace("System.UriBuilder::get_Host()")]

    [Fact]
    public void GivenAUriBuilder_WhenGetSensitiveProperty_IsTainted2()
    {
        UriBuilder builder = new UriBuilder(new Uri(taintedUrlValue));
        AssertTainted(builder.Host);
    }

    // Test [AspectMethodReplace("System.UriBuilder::get_Path()")]

    [Fact]
    public void GivenAUriBuilder_WhenGetSensitiveProperty_IsTainted3()
    {
        UriBuilder builder = new UriBuilder(new Uri(taintedUrlValue));
        AssertTainted(builder.Path);
    }

    // Test [AspectMethodReplace("System.Object::ToString()", "System.UriBuilder")]
    // Test [AspectMethodReplace("System.UriBuilder::set_Query(System.String)")]

    [Fact]
    public void GivenAUriBuilder_WhenSetSensitiveProperty_IsTainted14()
    {
        UriBuilder builder = new UriBuilder(new Uri(notTaintedValue));
        builder.Query = taintedQuery;
        AssertTainted(builder.ToString());
    }

    // Test [AspectMethodReplace("System.UriBuilder::set_Path(System.String)")]

    [Fact]
    public void GivenAUriBuilder_WhenSetSensitiveProperty_IsTainted15()
    {
        UriBuilder builder = new UriBuilder(new Uri(notTaintedValue));
        builder.Path = taintedUrlValue;
        AssertTainted(builder.ToString());
    }

    // Test [AspectMethodReplace("System.UriBuilder::set_Host(System.String)")]

    [Fact]
    public void GivenAUriBuilder_WhenSetSensitiveProperty_IsTainted17()
    {
        UriBuilder builder = new UriBuilder(new Uri(notTaintedValue));
        builder.Host = taintedHost;
        AssertTainted(builder.ToString());
    }
}

