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
    protected string taintedPathValue = "c:\\path";

    public StreamWriterTests()
    {
        AddTainted(taintedValue);
        AddTainted(taintedPathValue);
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new StreamWriter(taintedValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new StreamWriter(taintedValue, true); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { new StreamWriter(taintedValue, true, Encoding.UTF8); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged5()
    {
        ExecuteAction(() => { new StreamWriter(taintedValue, true, Encoding.UTF8, 4); });
        AssertVulnerable();
    }

#if NETCORE60
        [Fact]
        public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged6()
        {
            ExecuteAction(() => { new StreamWriter(taintedValue, Encoding.UTF8, new FileStreamOptions()); });
            AssertVulnerable();
        }

        [Fact]
        public void GivenAStreamWriter_WhenCreatingFromTaintedString_VulnerabilityIsLogged7()
        {
            ExecuteAction(() => { new StreamWriter(taintedValue, new FileStreamOptions()); });
            AssertVulnerable();
        }
#endif
    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromtaintedString_VulnerabilityIsNotLogged()
    {
        ExecuteAction(() => { new StreamWriter(notTaintedValue); });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromNullString_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentNullException>(() => new StreamWriter((string)null));
    }

    [Fact]
    public void GivenAStreamWriter_WhenCreatingFromBadIndex_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StreamWriter(taintedValue, true, Encoding.UTF8, -4));
    }

    void ExecuteAction(Action c)
    {
        try
        {
            c.Invoke();
        }
        catch (IOException)
        {
            //We dont have a valid file. It is normal
        }
    }
}
