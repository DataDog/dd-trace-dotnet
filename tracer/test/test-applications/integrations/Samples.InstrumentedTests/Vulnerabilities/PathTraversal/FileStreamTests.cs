// <copyright file="FileStreamTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
using System.Security.AccessControl;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class FileStreamTests : InstrumentationTestsBase
{
    protected string notTaintedValue = "c:\\nottainte9d";
    protected string taintedPathValue = "c:\\path";

    public FileStreamTests()
    {
        AddTainted(taintedPathValue);
    }

    // Cover System.IO.FileStream::.ctor(System.String,System.IO.FileMode)

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

    // Cover System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.IO.FileAccess)

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new FileStream(taintedPathValue, FileMode.Open, FileAccess.Read); });
        AssertVulnerable();
    }

    // Cover System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare)

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { new FileStream(taintedPathValue, FileMode.Open, FileAccess.Read, FileShare.Read); });
        AssertVulnerable();
    }

    // Cover System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare,System.Int32)

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged4()
    {
        ExecuteAction(() => { new FileStream(taintedPathValue, FileMode.Open, FileAccess.Read, FileShare.Read, 1); });
        AssertVulnerable();
    }

    // Cover System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare,System.Int32,System.Boolean)

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

    // Cover System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.IO.FileAccess,System.IO.FileShare,System.Int32,System.IO.FileOptions)

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged7()
    {
        ExecuteAction(() => { new FileStream(taintedPathValue, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.None); });
        AssertVulnerable();
    }

#if NETFRAMEWORK

    // Cover System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.Security.AccessControl.FileSystemRights,System.IO.FileShare,System.Int32,System.IO.FileOptions)

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged9()
    {
        ExecuteAction(() => { new FileStream(taintedPathValue, FileMode.Open, FileSystemRights.Read, FileShare.Read, 1, FileOptions.None); });
        AssertVulnerable();
    }

    // Cover System.IO.FileStream::.ctor(System.String,System.IO.FileMode,System.Security.AccessControl.FileSystemRights,System.IO.FileShare,System.Int32,System.IO.FileOptions,System.Security.AccessControl.FileSecurity)

    [Fact]
    public void GivenAFileStream_WhenCreatingFromTaintedString_VulnerabilityIsLogged10()
    {
        ExecuteAction(() => { new FileStream(taintedPathValue, FileMode.Open, FileSystemRights.Read, FileShare.Read, 1, FileOptions.None, new FileSecurity()); });
        AssertVulnerable();
    }

#endif
#if NET6_0_OR_GREATER

    // Cover System.IO.FileStream::.ctor(System.String,System.IOFileStreamOptions)

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
