// <copyright file="StreamReaderTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Text;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class StreamReaderTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string notTaintedValue = "c:\\nottainted2";

    public StreamReaderTests()
    {
        AddTainted(taintedValue);
    }

    // Cover System.IO.StreamReader::.ctor(System.String)

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new StreamReader(taintedValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromtaintedString_VulnerabilityIsNotLogged()
    {
        ExecuteAction(() => { new StreamReader(notTaintedValue); });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromNullString_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentNullException>(() => new StreamReader((string)null));
    }

    // Cover System.IO.StreamReader::.ctor(System.String,System.Boolean)

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new StreamReader(taintedValue, true); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromtaintedString_VulnerabilityIsNotLogged2()
    {
        ExecuteAction(() => { new StreamReader(notTaintedValue, true); });
        AssertNotVulnerable();
    }

    // Cover System.IO.StreamReader::.ctor(System.String,System.Text.Encoding)

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { new StreamReader(taintedValue, Encoding.UTF8); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromtaintedString_VulnerabilityIsNotLogged3()
    {
        ExecuteAction(() => { new StreamReader(notTaintedValue, Encoding.UTF8); });
        AssertNotVulnerable();
    }

    // Cover System.IO.StreamReader::.ctor(System.String,System.Text.Encoding,System.Boolean)

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { new StreamReader(taintedValue, Encoding.UTF8, true); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromtaintedString_VulnerabilityIsNotLogged4()
    {
        ExecuteAction(() => { new StreamReader(notTaintedValue, Encoding.UTF8, true); });
        AssertNotVulnerable();
    }

    // Cover System.IO.StreamReader::.ctor(System.String,System.Text.Encoding,System.Boolean,System.Int32)

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged5()
    {
        ExecuteAction(() => { new StreamReader(taintedValue, Encoding.UTF8, true, 4); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromtaintedString_VulnerabilityIsNotLogged5()
    {
        ExecuteAction(() => { new StreamReader(notTaintedValue, Encoding.UTF8, true, 10); });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromBadIndex_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StreamReader(taintedValue, Encoding.UTF8, true, -4));
    }

#if NET6_0_OR_GREATER

    // Cover System.IO.StreamReader::.ctor(System.String,System.IO.FileStreamOptions)

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged6()
    {
        ExecuteAction(() => { new StreamReader(taintedValue, new FileStreamOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromtaintedString_VulnerabilityIsNotLogged6()
    {
        ExecuteAction(() => { new StreamReader(notTaintedValue, new FileStreamOptions()); });
        AssertNotVulnerable();
    }

    // Cover System.IO.StreamReader::.ctor(System.String,System.Text.Encoding,System.Boolean,System.IO.FileStreamOptions)

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged7()
    {
        ExecuteAction(() => { new StreamReader(taintedValue, Encoding.UTF8, true, new FileStreamOptions()); });
        AssertVulnerable();
    }

#endif

    void ExecuteAction(Action c)
    {
        try
        {
            c.Invoke();
        }
        catch (FileNotFoundException)
        {
            //We dont have a valid file. It is normal
        }
    }

}
