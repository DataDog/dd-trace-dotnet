// <copyright file="DirectoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using System.ComponentModel;
using System.IO;
using System.Security.AccessControl;
using Xunit;

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class DirectoryTests : InstrumentationTestsBase
{
    protected string notTaintedValue = "z:";
    protected string taintedPathValue = "c:\\pat\"\0h";
    protected string taintedpattern = "*.password";

    public DirectoryTests()
    {
        AddTainted(taintedPathValue);
        AddTainted(taintedpattern);
    }

    [Fact]
    public void GivenADirectoryInfo_WhenCreatingFromInvalidCharsString_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => Directory.CreateDirectory(taintedPathValue));
        AssertVulnerable();
    }

#if NETFRAMEWORK
    [Fact]
    public void GivenADirectoryInfo_WhenCreatingFromTaintedStringDirectorySecurity_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.CreateDirectory(taintedPathValue, new DirectorySecurity()); });
        AssertVulnerable();
    }
#endif
    [Fact]
    public void GivenADirectoryInfo_WhenCreatingFromTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.CreateDirectory(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenCreatingFromIncorrectString_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => Directory.CreateDirectory(""));
    }

    [Fact]
    public void GivenADirectoryInfo_WhenCreatingFromIncorrectString_ExceptionIsThrown2()
    {
        Assert.Throws<ArgumentNullException>(() => Directory.CreateDirectory(null));
    }
#if NET7_0_OR_GREATER
    [Category("LinuxUnsupported")]
    [Fact]
    public void GivenADirectoryInfo_WhenCreatingFromTaintedString_VulnerabilityIsLogged2()
    {
#pragma warning disable CA1416 // Validate platform compatibility
        ExecuteAction(() => { Directory.CreateDirectory(taintedPathValue, UnixFileMode.None); });
#pragma warning restore CA1416 // Validate platform compatibility
        AssertVulnerable();
    }

    [Category("LinuxUnsupported")]
    [Fact]
    public void GivenADirectoryInfo_WhenCreatingFromTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { Directory.CreateTempSubdirectory(taintedPathValue); });
        AssertVulnerable();
    }
#endif
    [Fact]
    public void GivenADirectoryInfo_WhenDeleteTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.Delete(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenDeleteTaintedStringAndBool_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.Delete(taintedPathValue, true); });
        AssertVulnerable();
    }
#if !NET35
    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }
#if !NETFRAMEWORK

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedStringPathEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedStringPatternEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif
    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedStringPathPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK
    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedStringEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedStringEnumerationOptions_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemEntriesTaintedStringPathPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemEntriesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemEntriesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemEntriesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemEntriesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK
    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemEntriesTaintedStringPathEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemEntriesTaintedStringPatternEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif
#endif
    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedStringPathPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK
    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedStringPathEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedStringPatternEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoryRootTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectoryRoot(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedStringPathPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK
    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedStringPathEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedStringPatternEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemEntriesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemEntriesTaintedStringPathPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemEntriesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemEntriesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemEntriesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }


#if !NETFRAMEWORK
    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemEntriesTaintedStringPathEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemEntriesTaintedStringPatternEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

#if NETFRAMEWORK
    [Fact]
    public void GivenADirectoryInfo_WhenSetAccessControlTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.SetAccessControl(taintedPathValue, new DirectorySecurity()); });
        AssertVulnerable();
    }
#endif
    [Fact]
    public void GivenADirectoryInfo_WhenMoveTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.Move(notTaintedValue, taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenMoveTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { Directory.Move(taintedPathValue, notTaintedValue); });
        AssertVulnerable();
    }


    [Fact]
    public void GivenADirectoryInfo_WhenSetCurrentDirectoryTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.SetCurrentDirectory(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenSetCurrentDirectoryNotTaintedString_VulnerabilityIsNotLogged()
    {
        ExecuteAction(() => { Directory.SetCurrentDirectory(notTaintedValue); });
        AssertVulnerable();
    }

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
