// <copyright file="DirectoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class DirectoryInfoTests : InstrumentationTestsBase
{

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    [TestCategory(testCategory)]
    public void GivenADirectoryInfo_WhenCreatingFromInvalidCharsString_ExceptionIsThrown()
    {
        new DirectoryInfo(taintedPathValue);
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenCreatingFromTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(taintedPathValue); });
        AssertVulnerable();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    [TestCategory(testCategory)]
    public void GivenADirectoryInfo_WhenCreatingFromIncorrectString_ExceptionIsThrown()
    {
        new DirectoryInfo("");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenADirectoryInfo_WhenCreatingFromIncorrectString_ExceptionIsThrown2()
    {
        new DirectoryInfo(null);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenADirectoryInfo_WhenMoveToNullString_ArgumentNullException()
    {
        new DirectoryInfo(notTaintedValue).MoveTo(null);
    }

    [Fact]
    public void GivenADirectoryInfo_WhenMoveToTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).MoveTo(taintedPathValue); });
        AssertVulnerable();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenADirectoryInfo_WhenCreateSubdirectoryNullString_ArgumentNullException()
    {
        new DirectoryInfo(notTaintedValue).CreateSubdirectory(null);
        AssertVulnerable();
    }

#if !NETCORE31 && !NETCORE21 && !NET50 && !NET60
    [Fact]
    public void GivenADirectoryInfo_WhenCreateSubdirectoryTaintedStringDirectorySecurity_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).CreateSubdirectory(taintedSubFolderValue, new DirectorySecurity()); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenCreateSubdirectoryTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).CreateSubdirectory(taintedSubFolderValue); });
        AssertVulnerable();
    }
#else
        [Fact]
        public void GivenADirectoryInfo_WhenCreateSubdirectoryTaintedString_VulnerabilityIsLogged()
        {
            ExecuteAction(() => { new DirectoryInfo(".").CreateSubdirectory(taintedSubFolderValue); });
            AssertVulnerable();
        }
#endif
#if !NET35
    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateDirectories(taintedSubFolderValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateDirectories(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NET461 && !NET35
    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateDirectoriesTaintedStringSearchOption_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateDirectories(taintedSubFolderValue, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFiles(taintedSubFolderValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFiles(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NET461 && !NET35
    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFilesTaintedStringSearchOption_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFiles(taintedSubFolderValue, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemInfosTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFileSystemInfos(taintedSubFolderValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemInfosTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFileSystemInfos(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NET461 && !NET35
    [Fact]
    public void GivenADirectoryInfo_WhenEnumerateFileSystemInfosTaintedStringSearchOption_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).EnumerateFileSystemInfos(taintedSubFolderValue, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif
#endif
    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetDirectories(taintedSubFolderValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetDirectories(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }


#if !NET461 && !NET35
    [Fact]
    public void GivenADirectoryInfo_WhenGetDirectoriesTaintedStringSearchOption_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetDirectories(taintedSubFolderValue, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetFiles(taintedSubFolderValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetFiles(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }

#if !NET461 && !NET35
    [Fact]
    public void GivenADirectoryInfo_WhenGetFilesTaintedStringSearchOption_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetFiles(taintedSubFolderValue, new EnumerationOptions()); });
        AssertVulnerable();
    }
#endif

    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemInfosTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetFileSystemInfos(taintedSubFolderValue); });
        AssertVulnerable();
    }
#if !NET35
    [Fact]
    public void GivenADirectoryInfo_WhenGetFileSystemInfosTaintedStringSearchOption_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new DirectoryInfo(notTaintedValue).GetFileSystemInfos(taintedSubFolderValue, SearchOption.AllDirectories); });
        AssertVulnerable();
    }
#endif

#if !NET35 && !NET461
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
