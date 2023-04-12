// <copyright file="FileStreamTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class FileStreamTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string notTaintedValue = "c:\\nottainte9d";
    protected string taintedPathValue = "c:\\path";

    public FileStreamTests()
    {
        AddTainted(taintedPathValue);
        AddTainted(taintedValue);
    }

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new FileStream(taintedPathValue, FileMode.Open); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsNotLogged()
    {
        ExecuteAction(() => { new FileStream(notTaintedValue, FileMode.Open); });
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new FileStream(taintedPathValue, FileMode.Open, FileAccess.Read); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { new FileStream(taintedPathValue, FileMode.Open, FileAccess.Read, FileShare.Read); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { new FileStream(taintedPathValue, FileMode.Open, FileAccess.Read, FileShare.Read, 1); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged5()
    {
        ExecuteAction(() => { new FileStream(taintedPathValue, FileMode.Open, FileAccess.Read, FileShare.Read, 1, true); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentNullException>(() => new FileStream(null, FileMode.Open, FileAccess.Read, FileShare.Read, 1, true));
    }

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_ExceptionIsThrown2()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new FileStream(notTaintedValue, FileMode.Open, FileAccess.Read, FileShare.Read, -1, true));
    }

#if NET6
        [Fact]
        public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged6()
        {
            ExecuteAction(() => { new FileStream(taintedPathValue, new FileStreamOptions()); });
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
