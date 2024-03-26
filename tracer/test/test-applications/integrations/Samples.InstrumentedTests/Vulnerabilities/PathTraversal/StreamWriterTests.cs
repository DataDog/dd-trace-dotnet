// <copyright file="StreamWriterTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class StreamWriterTests : InstrumentationTestsBase
{
    protected string taintedValue = "#invalidFileTainted#";
    protected string notTaintedValue = "nottainted";

    public StreamWriterTests()
    {
        AddTainted(taintedValue);
    }

    // Cover System.IO.StreamWriter::.ctor(System.String)

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new StreamWriter(taintedValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromNotTaintedString_VulnerabilityIsNotLogged()
    {
        ExecuteAction(() => { new StreamWriter(notTaintedValue); });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromNullString_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentNullException>(() => new StreamWriter((string)null));
    }

    // Cover System.IO.StreamWriter::.ctor(System.String,System.Boolean)

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new StreamWriter(taintedValue, true); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromNotTaintedString_VulnerabilityIsNotLogged2()
    {
        ExecuteAction(() => { new StreamWriter(notTaintedValue, true); });
        AssertNotVulnerable();
    }

    // cover System.IO.StreamWriter::.ctor(System.String,System.Boolean,System.Text.Encoding)

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { new StreamWriter(taintedValue, true, Encoding.UTF8); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromNotTaintedString_VulnerabilityIsNotLogged3()
    {
        ExecuteAction(() => { new StreamWriter(notTaintedValue, true, Encoding.UTF8); });
        AssertNotVulnerable();
    }

    // cover System.IO.StreamWriter::.ctor(System.String,System.Boolean,System.Text.Encoding,System.Int32)

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged5()
    {
        ExecuteAction(() => { new StreamWriter(taintedValue, true, Encoding.UTF8, 4); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromNotTaintedString_VulnerabilityIsNotLogged4()
    {
        ExecuteAction(() => { new StreamWriter(notTaintedValue, true, Encoding.UTF8, 4); });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromBadIndex_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StreamWriter(taintedValue, true, Encoding.UTF8, -4));
    }

    // cover System.IO.StreamWriter::.ctor(System.String, System.Text.Encoding, System.IO.FileStreamOptions)

#if NET6_0_OR_GREATER
        [Fact]
        public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged6()
        {
            ExecuteAction(() => { new StreamWriter(taintedValue, Encoding.UTF8, new FileStreamOptions()); });
            AssertVulnerable();
        }

        [Fact]
        public void GivenAStreamWriter_WhenCreatingFromNotTaintedString_VulnerabilityIsNotLogged5()
        {
            ExecuteAction(() => { new StreamWriter(notTaintedValue, Encoding.UTF8, new FileStreamOptions()); });
            AssertNotVulnerable();
        }

        // cover System.IO.StreamWriter::.ctor(System.String, System.IO.FileStreamOptions)

        [Fact]
        public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged7()
        {
            ExecuteAction(() => { new StreamWriter(taintedValue, new FileStreamOptions()); });
            AssertVulnerable();
        }

        [Fact]
        public void GivenAStreamWriter_WhenCreatingFromNotTaintedString_VulnerabilityIsNotLogged6()
        {
            ExecuteAction(() => { new StreamWriter(notTaintedValue, new FileStreamOptions()); });
            AssertNotVulnerable();
        }
#endif

    void ExecuteAction(Action c)
    {
        try
        {
            c.Invoke();
        }
        catch (ArgumentException)
        {

        }
        catch (IOException)
        {
            //We dont have a valid file. It is normal
        }
    }
}
