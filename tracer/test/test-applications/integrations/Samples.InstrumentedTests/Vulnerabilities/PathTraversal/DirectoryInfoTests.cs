// <copyright file="DirectoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.IO;
#if NETFRAMEWORK
using System.Security.AccessControl;
#endif
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class DirectoryInfoTests : InstrumentationTestsBase
{
    protected string notTaintedValue = "c:\\nottaintedDir";
    protected string taintedPathValue = "c:\\pat\"\0h";
    protected string taintedSubFolderValue = "pa\"\0th";

    public DirectoryInfoTests()
    {
        AddTainted(taintedPathValue);
        AddTainted(taintedSubFolderValue);
    }

    // Cover System.IO.DirectoryInfo::.ctor(System.String)

    [Fact]
    public void GivenADirectoryInfo_WhenCreatingFromTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenCreatingFromIncorrectString_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => new DirectoryInfo(""));
    }

    // Cover System.IO.DirectoryInfo::MoveTo(System.String)

    [Fact]
    public void GivenADirectoryInfo_WhenCreatingFromIncorrectString_ExceptionIsThrown2()
    {
        Assert.Throws<ArgumentNullException>(() => new DirectoryInfo(null).MoveTo(null));
    }

    [Fact]
    public void GivenADirectoryInfo_WhenMoveToNullString_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DirectoryInfo(notTaintedValue).MoveTo(null));
    }

    [Fact]
    public void GivenADirectoryInfo_WhenMoveToTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).MoveTo(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenCreateSubdirectoryNullString_ArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new DirectoryInfo(notTaintedValue).CreateSubdirectory(null));
    }

#if NETFRAMEWORK

    // Cover System.IO.DirectoryInfo::CreateSubdirectory(System.String,System.Security.AccessControl.DirectorySecurity)"

    [Fact]
    public void GivenADirectoryInfo_WhenCreateSubdirectoryTaintedStringDirectorySecurity_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).CreateSubdirectory(taintedSubFolderValue, new DirectorySecurity()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.DirectoryInfo::CreateSubdirectory(System.String)

    [Fact]
    public void GivenADirectoryInfo_WhenCreateSubdirectoryTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).CreateSubdirectory(taintedSubFolderValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenCreateSubdirectoryTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { new DirectoryInfo(".").CreateSubdirectory(taintedSubFolderValue); });
        AssertVulnerable();
    }    

    // Cover System.IO.DirectoryInfo::EnumerateDirectories(System.String)

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateDirectories(taintedSubFolderValue); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::EnumerateFiles(System.String)

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFiles(taintedSubFolderValue); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::EnumerateDirectories(System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateDirectories(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::EnumerateFiles(System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFiles(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK

    // Cover System.IO.DirectoryInfo::EnumerateDirectories(System.String,System.IO.EnumerationOptions)

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedStringSearchOption_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateDirectories(taintedSubFolderValue, new EnumerationOptions()); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::EnumerateFiles(System.String,System.IO.EnumerationOptions)  

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedStringSearchOption_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFiles(taintedSubFolderValue, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.DirectoryInfo::EnumerateFileSystemInfos(System.String)

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemInfosTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFileSystemInfos(taintedSubFolderValue); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::EnumerateFileSystemInfos(System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemInfosTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFileSystemInfos(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::EnumerateFileSystemInfos(System.String,System.IO.EnumerationOptions)

#if !NETFRAMEWORK
    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemInfosTaintedStringSearchOption_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFileSystemInfos(taintedSubFolderValue, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.DirectoryInfo::GetDirectories(System.String)

    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetDirectories(taintedSubFolderValue); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::GetDirectories(System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetDirectories(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::GetDirectories(System.String,System.IO.EnumerationOptions)

#if !NETFRAMEWORK
    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedStringSearchOption_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetDirectories(taintedSubFolderValue, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.DirectoryInfo::GetFiles(System.String)

    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetFiles(taintedSubFolderValue); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::GetFiles(System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetFiles(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::GetFiles(System.String,System.IO.EnumerationOptions)

#if !NETFRAMEWORK
    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedStringSearchOption_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetFiles(taintedSubFolderValue, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.DirectoryInfo::GetFileSystemInfos(System.String)

    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemInfosTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetFileSystemInfos(taintedSubFolderValue); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::GetFileSystemInfos(System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemInfosTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetFileSystemInfos(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    // Cover System.IO.DirectoryInfo::GetFileSystemInfos(System.String,System.IO.EnumerationOptions)

#if !NETFRAMEWORK
    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemInfosTaintedStringSearchOption_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetFileSystemInfos(taintedSubFolderValue, new EnumerationOptions()); });
        AssertVulnerable();
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
            //We dont have a valid file. It is normal
        }
        catch (IOException)
        {
            //We dont have a valid file. It is normal
        }
    }


}
