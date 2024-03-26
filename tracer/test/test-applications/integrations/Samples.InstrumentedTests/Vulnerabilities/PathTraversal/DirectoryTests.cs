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

    // Cover System.IO.Directory::CreateDirectory(System.String)

#if NETFRAMEWORK

    // Cover System.IO.Directory::CreateDirectory(System.String,System.Security.AccessControl.DirectorySecurity)

    [Fact]
    public void GivenADirectory_WhenCreatingFromTaintedStringDirectorySecurity_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.CreateDirectory(taintedPathValue, new DirectorySecurity()); });
        AssertVulnerable();
    }
#endif
    [Fact]
    public void GivenADirectory_WhenCreatingFromTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.CreateDirectory(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenCreatingFromIncorrectString_ExceptionIsThrown()
    {
        Assert.Throws<ArgumentException>(() => Directory.CreateDirectory(""));
    }

    [Fact]
    public void GivenADirectory_WhenCreatingFromIncorrectString_ExceptionIsThrown2()
    {
        Assert.Throws<ArgumentNullException>(() => Directory.CreateDirectory(null));
    }

    // Cover System.IO.Directory CreateDirectory(System.String, System.IO.UnixFileMode)

#if NET7_0_OR_GREATER
    [Fact]
    public void GivenADirectory_WhenCreatingFromTaintedString_VulnerabilityIsLogged2()
    {
#pragma warning disable CA1416 // Validate platform compatibility
        ExecuteAction(() => {Directory.CreateDirectory(taintedPathValue, UnixFileMode.None); });
#pragma warning restore CA1416 // Validate platform compatibility
        AssertVulnerable();
    }

    // Cover System.IO.Directory CreateTempSubdirectory(System.String)

    [Fact]
    public void GivenADirectory_WhenCreatingFromTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { Directory.CreateTempSubdirectory(taintedPathValue); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.Directory::Delete(System.String)

    [Fact]
    public void GivenADirectory_WhenDeleteTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.Delete(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::Delete(System.String,System.Boolean)

    [Fact]
    public void GivenADirectory_WhenDeleteTaintedStringAndBool_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.Delete(taintedPathValue, true); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::EnumerateDirectories(System.String)

    [Fact]
    public void GivenADirectory_WhenEnumerateDirectoriesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::EnumerateDirectories(System.String,System.String)

    [Fact]
    public void GivenADirectory_WhenEnumerateDirectoriesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenEnumerateDirectoriesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::GetFileSystemEntries(System.String,System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectory_WhenEnumerateDirectoriesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenEnumerateDirectoriesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }
#if !NETFRAMEWORK

    // Cover System.IO.Directory::EnumerateDirectories(System.String,System.String,System.IO.EnumerationOptions)

    [Fact]
    public void GivenADirectory_WhenEnumerateDirectoriesTaintedStringPathEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenEnumerateDirectoriesTaintedStringPatternEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateDirectories(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.Directory::EnumerateFiles(System.String)

    [Fact]
    public void GivenADirectory_WhenEnumerateFilesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::EnumerateFiles(System.String,System.String)

    [Fact]
    public void GivenADirectory_WhenEnumerateFilesTaintedStringPathPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenEnumerateFilesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::EnumerateFiles(System.String,System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectory_WhenEnumerateFilesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenEnumerateFilesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK

    // Cover System.IO.Directory::EnumerateFiles(System.String,System.String,System.IO.EnumerationOptions)

    [Fact]
    public void GivenADirectory_WhenEnumerateFilesTaintedStringEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenEnumerateFilesTaintedStringEnumerationOptions_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { Directory.EnumerateFiles(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.Directory::EnumerateFileSystemEntries(System.String,System.String)

    [Fact]
    public void GivenADirectory_WhenEnumerateFileSystemEntriesTaintedStringPathPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenEnumerateFileSystemEntriesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::EnumerateFileSystemEntries(System.String)

    [Fact]
    public void GivenADirectory_WhenEnumerateFileSystemEntriesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::EnumerateFileSystemEntries(System.String,System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectory_WhenEnumerateFileSystemEntriesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenEnumerateFileSystemEntriesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK

    // Cover System.IO.Directory::EnumerateFileSystemEntries(System.String,System.String,System.IO.EnumerationOptions)
    [Fact]
    public void GivenADirectory_WhenEnumerateFileSystemEntriesTaintedStringPathEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenEnumerateFileSystemEntriesTaintedStringPatternEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.EnumerateFileSystemEntries(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.Directory::GetDirectories(System.String)

    [Fact]
    public void GivenADirectory_WhenGetDirectoriesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::GetDirectories(System.String,System.String)

    [Fact]
    public void GivenADirectory_WhenGetDirectoriesTaintedStringPathPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenGetDirectoriesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::GetDirectories(System.String,System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectory_WhenGetDirectoriesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenGetDirectoriesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK

    // Cover System.IO.Directory::GetDirectories(System.String,System.String,System.IO.EnumerationOptions)

    [Fact]
    public void GivenADirectory_WhenGetDirectoriesTaintedStringPathEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenGetDirectoriesTaintedStringPatternEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectories(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.Directory::GetDirectoryRoot(System.String)

    [Fact]
    public void GivenADirectory_WhenGetDirectoryRootTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetDirectoryRoot(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::GetFiles(System.String)

    [Fact]
    public void GivenADirectory_WhenGetFilesTaintedStringPathPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::GetFiles(System.String,System.String)

    [Fact]
    public void GivenADirectory_WhenGetFilesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenGetFilesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::GetFiles(System.String,System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectory_WhenGetFilesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenGetFilesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NETFRAMEWORK

    // Cover System.IO.Directory::GetFiles(System.String,System.String,System.IO.EnumerationOptions)

    [Fact]
    public void GivenADirectory_WhenGetFilesTaintedStringPathEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenGetFilesTaintedStringPatternEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFiles(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.Directory::GetFileSystemEntries(System.String)

    [Fact]
    public void GivenADirectory_WhenGetFileSystemEntriesTaintedStringPath_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(taintedPathValue); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::GetFileSystemEntries(System.String,System.String)

    [Fact]
    public void GivenADirectory_WhenGetFileSystemEntriesTaintedStringPathPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(taintedPathValue, "*"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenGetFileSystemEntriesTaintedStringPattern_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(notTaintedValue, taintedpattern); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::EnumerateDirectories(System.String,System.String,System.IO.SearchOption)

    [Fact]
    public void GivenADirectory_WhenGetFileSystemEntriesTaintedStringPathSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(taintedPathValue, "*", SearchOption.AllDirectories); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenGetFileSystemEntriesTaintedStringPatternSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(notTaintedValue, taintedpattern, SearchOption.AllDirectories); });
        AssertVulnerable();
    }


#if !NETFRAMEWORK

    // Cover System.IO.Directory::GetFileSystemEntries(System.String,System.String,System.IO.EnumerationOptions)

    [Fact]
    public void GivenADirectory_WhenGetFileSystemEntriesTaintedStringPathEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(taintedPathValue, "*", new EnumerationOptions()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenGetFileSystemEntriesTaintedStringPatternEnumerationOptions_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.GetFileSystemEntries(notTaintedValue, taintedpattern, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

#if NETFRAMEWORK

    // Cover System.IO.Directory::SetAccessControl(System.String,System.Security.AccessControl.DirectorySecurity)

    [Fact]
    public void GivenADirectory_WhenSetAccessControlTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.SetAccessControl(taintedPathValue, new DirectorySecurity()); });
        AssertVulnerable();
    }
#endif

    // Cover System.IO.Directory::Move(System.String,System.String)

    [Fact]
    public void GivenADirectory_WhenMoveTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.Move(notTaintedValue, taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenMoveTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { Directory.Move(taintedPathValue, notTaintedValue); });
        AssertVulnerable();
    }

    // Cover System.IO.Directory::SetCurrentDirectory(System.String)

    [Fact]
    public void GivenADirectory_WhenSetCurrentDirectoryTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { Directory.SetCurrentDirectory(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectory_WhenSetCurrentDirectoryNotTaintedString_VulnerabilityIsNotLogged()
    {
        ExecuteAction(() => { Directory.SetCurrentDirectory(notTaintedValue); });
        AssertNotVulnerable();
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
        catch (PlatformNotSupportedException)
        {
            //We dont have a valid file. It is normal
        }
    }

}
