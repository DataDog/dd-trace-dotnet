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
    protected string taintedPathValue = "c:\\path";

    public StreamReaderTests()
    {
        AddTainted(taintedPathValue);
        AddTainted(taintedValue);
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new StreamReader(taintedValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new StreamReader(taintedValue, true); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { new StreamReader(taintedValue, Encoding.UTF8); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { new StreamReader(taintedValue, Encoding.UTF8, true); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged5()
    {
        ExecuteAction(() => { new StreamReader(taintedValue, Encoding.UTF8, true, 4); });
        AssertVulnerable();
    }
#if NET60
        [Fact]
        public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged6()
        {
            ExecuteAction(() => { new StreamReader(taintedValue, new FileStreamOptions()); });
            AssertVulnerable();
        }

        [Fact]
        public void GivenAStreamReader_WhenCreatingFromTaintedString_VulnerabilityIsLogged7()
        {
            ExecuteAction(() => { new StreamReader(taintedValue, Encoding.UTF8, true, new FileStreamOptions()); });
            AssertVulnerable();
        }
#endif
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

    [Fact]
    public void GivenAStreamReader_WhenCreatingFromBadIndex_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new StreamReader(taintedValue, Encoding.UTF8, true, -4));
    }

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
