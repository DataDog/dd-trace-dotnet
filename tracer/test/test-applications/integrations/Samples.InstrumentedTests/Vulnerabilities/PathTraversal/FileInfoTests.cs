// <copyright file="DirectoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class FileTests : InstrumentationTestsBase
{
    protected string notTaintedValue = "c:\\nottainted2";
    protected string taintedPathValue = "c:\\path";

    [TestInitialize]
    public void Init()
    {
        CaptureVulnerabilities(VulnerabilityType.PATH_TRAVERSAL);
        var context = ContextHolder.Current;
        context.TaintedObjects.Add(context, "param2", taintedPathValue, VulnerabilityOriginType.PATH_VARIABLE);
    }

    [Fact]
    public void GivenAFileInfo_WhenCreatingFromTaintedString_VulnerabilityIsLogged()
    {
        new FileInfo(taintedPathValue);
        AssertVulnerable();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    [TestCategory(testCategory)]
    public void GivenAFileInfo_WhenCreatingFromIncorrectString_ExceptionIsThrown()
    {
        new FileInfo("");
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenAFileInfo_WhenCreatingFromIncorrectString_ExceptionIsThrown2()
    {
        new FileInfo(null);
    }

    [Fact]
    public void GivenAFileInfo_WhenCopyToTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new FileInfo(notTaintedValue).CopyTo(taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFileInfo_WhenCopyToTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new FileInfo(notTaintedValue).CopyTo(taintedPathValue, true); });
        AssertVulnerable();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenAFileInfo_WhenCopyToNullString_ArgumentNullException()
    {
        new FileInfo(notTaintedValue).CopyTo(null, true);
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFileInfo_WhenMoveToTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new FileInfo(notTaintedValue).MoveTo(taintedPathValue); });
        AssertVulnerable();
    }
#if !NET35 && !NET461 && !NETCORE21
    [Fact]
    public void GivenAFileInfo_WhenMoveToTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new FileInfo(notTaintedValue).MoveTo(taintedPathValue, true); });
        AssertVulnerable();
    }
#endif
    [TestMethod]
    [ExpectedException(typeof(FileNotFoundException))]
    [TestCategory(testCategory)]
    public void GivenAFileInfo_WhenMoveToNoExistingFile_FileNotFoundException()
    {
        new FileInfo(notTaintedValue).MoveTo(taintedPathValue);
    }

    [Fact]
    public void GivenAFileInfo_WhenReplaceTaintedString_VulnerabilityIsLogged()
    {
        ExecuteAction(() => { new FileInfo(notTaintedValue).Replace(taintedPathValue, "dummy"); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFileInfo_WhenReplaceTaintedString_VulnerabilityIsLogged2()
    {
        ExecuteAction(() => { new FileInfo(notTaintedValue).Replace("dummy", taintedPathValue); });
        AssertVulnerable();
    }

    [Fact]
    public void GivenAFileInfo_WhenReplaceTaintedString_VulnerabilityIsLogged3()
    {
        ExecuteAction(() => { new FileInfo(notTaintedValue).Replace("dummy", taintedPathValue, true); });
        AssertVulnerable();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenAFileInfo_WhenReplaceNullString_ArgumentNullException()
    {
        new FileInfo(notTaintedValue).Replace(null, taintedPathValue, true);
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
        catch (UnauthorizedAccessException)
        {
            //This is a expected behaviour
        }
    }

}
