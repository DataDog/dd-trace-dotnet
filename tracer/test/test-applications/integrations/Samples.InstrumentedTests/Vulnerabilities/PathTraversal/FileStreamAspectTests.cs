// <copyright file="DirectoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class FileTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string notTaintedValue = "c:\\nottainte9d";
    protected string taintedPathValue = "c:\\path";

    [TestInitialize]
    public void Init()
    {
        CaptureVulnerabilities(VulnerabilityType.PATH_TRAVERSAL);
        var context = ContextHolder.Current;
        context.TaintedObjects.Add(context, "param1", taintedValue, VulnerabilityOriginType.PATH_VARIABLE);
        context.TaintedObjects.Add(context, "param2", taintedPathValue, VulnerabilityOriginType.PATH_VARIABLE);
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

    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenAFileStream_WhenCreatingFromTaintedString_ExceptionIsThrown()
    {
        ExecuteAction(() => { new FileStream(null, FileMode.Open, FileAccess.Read, FileShare.Read, 1, true); });
        AssertVulnerable();
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    [TestCategory(testCategory)]
    public void GivenAFileStream_WhenCreatingFromTaintedString_ExceptionIsThrown2()
    {
        ExecuteAction(() => { new FileStream(notTaintedValue, FileMode.Open, FileAccess.Read, FileShare.Read, -1, true); });
        AssertVulnerable();
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
