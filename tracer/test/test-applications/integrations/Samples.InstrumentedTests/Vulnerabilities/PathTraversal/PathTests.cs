// <copyright file="DirectoryTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

namespace Samples.InstrumentedTests.Iast.Vulnerabilities;

public class FileTests : InstrumentationTestsBase
{
    protected string taintedValue = "tainted";
    protected string taintedValueFile = "mydll.dll";
    protected string notTaintedValue = "nottainted";
    protected string taintedPathValue = Path.DirectorySeparatorChar + "c" + Path.DirectorySeparatorChar + "path";
    protected string fulltaintedPathValue = Path.DirectorySeparatorChar + "c" + Path.DirectorySeparatorChar + "path" + Path.DirectorySeparatorChar + "file.txt";

    [TestInitialize]
    public void Init()
    {
        CaptureVulnerabilities(VulnerabilityType.PATH_TRAVERSAL);
        var context = ContextHolder.Current;
        context.TaintedObjects.Add(context, "param1", taintedValue, VulnerabilityOriginType.PATH_VARIABLE);
        context.TaintedObjects.Add(context, "param2", taintedPathValue, VulnerabilityOriginType.PATH_VARIABLE);
        context.TaintedObjects.Add(context, "param3", taintedValueFile, VulnerabilityOriginType.PATH_VARIABLE);
        context.TaintedObjects.Add(context, "param4", fulltaintedPathValue, VulnerabilityOriginType.PATH_VARIABLE);
    }

    [Fact]
    public void GivenAPath_WhenCombiningWithTainted_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("c:" + Path.DirectorySeparatorChar + ":+-tainted-+:", () => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue), () => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenCombiningTaintedRouteWithNotTainted_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-" + Path.DirectorySeparatorChar + "c" + Path.DirectorySeparatorChar + "path-+:" + Path.DirectorySeparatorChar + "nottainted", () => Path.Combine(taintedPathValue, notTaintedValue), () => Path.Combine(taintedPathValue, notTaintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenCombiningWithNotTainted_ResultIsTainted()
    {
        AssertNotTaintedWithOriginalCallCheck(() => Path.Combine("c:" + Path.DirectorySeparatorChar, notTaintedValue), () => Path.Combine("c:" + Path.DirectorySeparatorChar, notTaintedValue));
        AssertNotVulnerable();
    }

#if !NET35

    [Fact]
    public void GivenAPath_WhenCombiningWithTainted2Params_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("c:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:", () => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue, taintedValue), () => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue, taintedValue));
        AssertNotVulnerable();
    }


    [Fact]
    public void GivenAPath_WhenCombiningWithTainted3Params_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("c:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:", () => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue, taintedValue, taintedValue), () => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue, taintedValue, taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenCombiningWithTainted4Params_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("c:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + "nottainted" + Path.DirectorySeparatorChar + ":+-tainted-+:", () => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue, taintedValue, notTaintedValue, taintedValue), () => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue, taintedValue, notTaintedValue, taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenCombiningWithTainted5Params_ResultIsTainted()
    {
        AssertOriginalInstrumentedSameResult(() => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue, taintedValue, notTaintedValue, taintedValue, notTaintedValue), () => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue, taintedValue, notTaintedValue, taintedValue, notTaintedValue));
        AssertNotVulnerable();
    }

    [TestMethod]
    //[ExpectedException(typeof(System.ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenAPath_WhenCombiningWithOneNullParam_ResultIsTainted()
    {
        AssertOriginalInstrumentedSameResult(() => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue, null), () => Path.Combine("c:" + Path.DirectorySeparatorChar, taintedValue, null));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenCombiningTaintedRouteWithTainted2Params_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-" + Path.DirectorySeparatorChar + "c" + Path.DirectorySeparatorChar + "path-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:", () => Path.Combine(taintedPathValue, taintedValue, taintedValue), () => Path.Combine(taintedPathValue, taintedValue, taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenCombiningTaintedRouteWithTainted3Params_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-" + Path.DirectorySeparatorChar + "c" + Path.DirectorySeparatorChar + "path-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:", () => Path.Combine(taintedPathValue, taintedValue, taintedValue, taintedValue), () => Path.Combine(taintedPathValue, taintedValue, taintedValue, taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenCombiningTaintedRouteWithTainted4Params_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-" + Path.DirectorySeparatorChar + "c" + Path.DirectorySeparatorChar + "path-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + "nottainted" + Path.DirectorySeparatorChar + ":+-tainted-+:", () => Path.Combine(taintedPathValue, taintedValue, taintedValue, notTaintedValue, taintedValue), () => Path.Combine(taintedPathValue, taintedValue, taintedValue, notTaintedValue, taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenCombiningTaintedRouteWithTainted5Params_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-" + Path.DirectorySeparatorChar + "c" + Path.DirectorySeparatorChar + "path-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + "nottainted" + Path.DirectorySeparatorChar + ":+-tainted-+:" + Path.DirectorySeparatorChar + "nottainted", () => Path.Combine(taintedPathValue, taintedValue, taintedValue, notTaintedValue, taintedValue, notTaintedValue), () => Path.Combine(taintedPathValue, taintedValue, taintedValue, notTaintedValue, taintedValue, notTaintedValue));
        AssertNotVulnerable();
    }

    [TestMethod]
    //[ExpectedException(typeof(System.ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenAPath_WhenCombiningTaintedRouteWithOneNullParam_ResultIsTainted()
    {
        AssertOriginalInstrumentedSameResult(() => Path.Combine(taintedPathValue, taintedValue, null), () => Path.Combine(taintedPathValue, taintedValue, null));
    }
#endif
    [TestMethod]
    //[ExpectedException(typeof(System.ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenAPath_WhenCombiningWithOneNullParam_ResultIsTainted2()
    {
        AssertOriginalInstrumentedSameResult(() => Path.Combine("c:" + Path.DirectorySeparatorChar, null), () => Path.Combine("c:" + Path.DirectorySeparatorChar, null));
    }

    [TestMethod]
    //[ExpectedException(typeof(System.ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenAPath_WhenCombiningWithOneNullParamAll_ResultIsTainted()
    {
        AssertOriginalInstrumentedSameResult(() => Path.Combine(null, null), () => Path.Combine(null, null));
    }

    [TestMethod]
    //[ExpectedException(typeof(System.ArgumentNullException))]
    [TestCategory(testCategory)]
    public void GivenAPath_WhenCombiningWithOneNullRoute_ResultIsTainted()
    {
        AssertOriginalInstrumentedSameResult(() => Path.Combine(null, taintedValue), () => Path.Combine(null, taintedValue));
    }

    [Fact]
    public void GivenAPath_WhenCombiningWithBadRoute_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-tainted-+:" + Path.DirectorySeparatorChar + ":+-tainted-+:", () => Path.Combine(taintedValue, taintedValue), () => Path.Combine(taintedValue, taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenChangeExtension_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-tainted-+:.dll", () => Path.ChangeExtension(taintedValue, "dll"), () => Path.ChangeExtension(taintedValue, "dll"));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenChangeExtensionFile_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-mydll-+:.info", () => Path.ChangeExtension(taintedValueFile, "info"), () => Path.ChangeExtension(taintedValueFile, "info"));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenChangeExtensionTainted_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-tainted-+:.:+-tainted-+:", () => Path.ChangeExtension(taintedValue, taintedValue), () => Path.ChangeExtension(taintedValue, taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenChangeExtensionTaintedExtensionOnly_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck("nottainted.:+-tainted-+:", () => Path.ChangeExtension(notTaintedValue, taintedValue), () => Path.ChangeExtension(notTaintedValue, taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenChangeExtensionTainted_ResultIsTainted2()
    {
        AssertTaintedWithOriginalCallCheck("nottainted.:+-tainted-+:", () => Path.ChangeExtension(notTaintedValue, taintedValue), () => Path.ChangeExtension(notTaintedValue, taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenChangeExtensionTaintedNullFileName_ResultIsNull()
    {
        AssertOriginalInstrumentedSameResult(() => Path.ChangeExtension(null, taintedValue), () => Path.ChangeExtension(null, taintedValue));
    }

    [Fact]
    public void GivenAPath_WhenChangeExtensionTaintedEmptyFileName_ResultIsEmpty()
    {
        AssertOriginalInstrumentedSameResult(() => Path.ChangeExtension(String.Empty, taintedValue), () => Path.ChangeExtension(String.Empty, taintedValue));
    }

    [Fact]
    public void GivenAPath_WhenChangeExtensionTaintedNullFileExtension_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-tainted-+:", () => Path.ChangeExtension(taintedValue, null), () => Path.ChangeExtension(taintedValue, null));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenChangeExtensionTaintedEmptyFileExtension_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-tainted-+:.", () => Path.ChangeExtension(taintedValue, String.Empty), () => Path.ChangeExtension(taintedValue, String.Empty));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenChangeExtensionNullAll_ResultIsNull()
    {
        AssertOriginalInstrumentedSameResult(() => Path.ChangeExtension(null, null), () => Path.ChangeExtension(null, null));
    }

    [Fact]
    public void GivenAPath_WhenGetDirectoryNameTainted_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-" + Path.DirectorySeparatorChar + "c" + Path.DirectorySeparatorChar + "path-+:", () => Path.GetDirectoryName(taintedPathValue + Path.DirectorySeparatorChar + taintedValue), () => Path.GetDirectoryName(taintedPathValue + Path.DirectorySeparatorChar + taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetDirectoryNameTainted_ResultIsTainted2()
    {
        AssertTaintedWithOriginalCallCheck(":+-" + Path.DirectorySeparatorChar + "c" + Path.DirectorySeparatorChar + "path-+:", () => Path.GetDirectoryName(fulltaintedPathValue), () => Path.GetDirectoryName(fulltaintedPathValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetDirectoryNameTainted_ResultIsTainted3()
    {
        AssertTaintedWithOriginalCallCheck(":+-" + Path.DirectorySeparatorChar + "c" + Path.DirectorySeparatorChar + "path-+:" + Path.DirectorySeparatorChar + "..", () => Path.GetDirectoryName(taintedPathValue + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + taintedValue), () => Path.GetDirectoryName(taintedPathValue + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetExtensionTainted_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(".:+-tainted-+:", () => Path.GetExtension(taintedValue + "." + taintedValue), () => Path.GetExtension(taintedValue + "." + taintedValue));
        AssertNotVulnerable();
    }


    [Fact]
    public void GivenAPath_WhenGetExtensionTainted_ResultIsTainted2()
    {
        AssertTaintedWithOriginalCallCheck(":+-.txt-+:", () => Path.GetExtension(fulltaintedPathValue), () => Path.GetExtension(fulltaintedPathValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetExtensionTainted_ResultIsTainted3()
    {
        AssertTaintedWithOriginalCallCheck(".:+-tainted-+:", () => Path.GetExtension("file." + taintedValue), () => Path.GetExtension("file." + taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetExtensionNotTainted_ResultIsNotTainted()
    {
        AssertOriginalInstrumentedSameResult(() => Path.GetExtension(taintedValue + ".dll"), () => Path.GetExtension(taintedValue + ".dll"));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFileNameTainted_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-tainted-+:.:+-tainted-+:", () => Path.GetFileName(taintedPathValue + Path.DirectorySeparatorChar + taintedValue + "." + taintedValue), () => Path.GetFileName(taintedPathValue + Path.DirectorySeparatorChar + taintedValue + "." + taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFileNameTainted_ResultIsTainted2()
    {
        AssertTaintedWithOriginalCallCheck(":+-tainted-+:", () => Path.GetFileName(notTaintedValue + Path.DirectorySeparatorChar + taintedValue), () => Path.GetFileName(notTaintedValue + Path.DirectorySeparatorChar + taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFileNameTainted_ResultIsTainted3()
    {
        AssertTaintedWithOriginalCallCheck(":+-file.txt-+:", () => Path.GetFileName(fulltaintedPathValue), () => Path.GetFileName(fulltaintedPathValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFileNameNull_NullIsReturned()
    {
        AssertOriginalInstrumentedSameResult(() => Path.GetFileName(null), () => Path.GetFileName(null));
        AssertNotVulnerable();
    }
    [Fact]
    public void GivenAPath_WhenGetFileNameEmpty_EmptyIsReturned()
    {
        AssertOriginalInstrumentedSameResult(() => Path.GetFileName(String.Empty), () => Path.GetFileName(String.Empty));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFileNameNotTainted_ResultIsNotTainted()
    {
        AssertOriginalInstrumentedSameResult(() => Path.GetFileName(taintedPathValue + Path.DirectorySeparatorChar + "hhh.dll"), () => Path.GetFileName(taintedPathValue + Path.DirectorySeparatorChar + "hhh.dll"));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFileNameWithoutExtensionTainted_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-tainted-+:", () => Path.GetFileNameWithoutExtension(taintedPathValue + Path.DirectorySeparatorChar + taintedValue + "." + taintedValue), () => Path.GetFileNameWithoutExtension(taintedPathValue + Path.DirectorySeparatorChar + taintedValue + "." + taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFileNameWithoutExtensionTainted_ResultIsTainted3()
    {
        AssertTaintedWithOriginalCallCheck(":+-file-+:", () => Path.GetFileNameWithoutExtension(fulltaintedPathValue), () => Path.GetFileNameWithoutExtension(fulltaintedPathValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFileNameWithoutExtensionTainted_ResultIsTainted2()
    {
        AssertTaintedWithOriginalCallCheck(":+-tainted-+:", () => Path.GetFileNameWithoutExtension("hhh" + Path.DirectorySeparatorChar + taintedValue), () => Path.GetFileNameWithoutExtension("hhh" + Path.DirectorySeparatorChar + taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFileNameWithoutExtensionNotTainted_ResultIsNotTainted()
    {
        AssertOriginalInstrumentedSameResult(() => Path.GetFileNameWithoutExtension(taintedPathValue + Path.DirectorySeparatorChar + "hhh.dll"), () => Path.GetFileNameWithoutExtension(taintedPathValue + Path.DirectorySeparatorChar + "hhh.dll"));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFullPathTainted_ResultIsTainted()
    {
        var expected = OperatingSystemUtil.IsLinux() ? ":+-/c/path/tainted.tainted-+:" : ":+-C:\\c\\path\\tainted.tainted-+:";
        AssertTaintedWithOriginalCallCheck(expected, () => Path.GetFullPath(taintedPathValue + Path.DirectorySeparatorChar + taintedValue + "." + taintedValue), () => Path.GetFullPath(taintedPathValue + Path.DirectorySeparatorChar + taintedValue + "." + taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFullPathTainted_ResultIsTainted2()
    {
        var expected = OperatingSystemUtil.IsLinux() ? ":+-/c/path/hhh.dll-+:" : ":+-C:\\c\\path\\hhh.dll-+:";
        AssertTaintedWithOriginalCallCheck(expected, () => Path.GetFullPath(taintedPathValue + Path.DirectorySeparatorChar + "www" + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + "hhh.dll"), () => Path.GetFullPath(taintedPathValue + Path.DirectorySeparatorChar + "www" + Path.DirectorySeparatorChar + ".." + Path.DirectorySeparatorChar + "hhh.dll"));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFullPathTainted_ResultIsTainted3()
    {
        var expected = OperatingSystemUtil.IsLinux() ? ":+-/c-+:" : ":+-C:\\c-+:";
        AssertTaintedWithOriginalCallCheck(expected, () => Path.GetFullPath(taintedPathValue + Path.DirectorySeparatorChar + ".."), () => Path.GetFullPath(taintedPathValue + Path.DirectorySeparatorChar + ".."));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetFullPathTainted2_ResultIsTainted()
    {
        var expected = OperatingSystemUtil.IsLinux() ? ":+-/tainted-+:" : ":+-C:\\tainted-+:";
        AssertTaintedWithOriginalCallCheck(expected, () => Path.GetFullPath(Path.DirectorySeparatorChar + taintedValue), () => Path.GetFullPath(Path.DirectorySeparatorChar + taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetGetPathRootNoDrive_ResultIsEmpty()
    {
        AssertOriginalInstrumentedSameResult(() => Path.GetPathRoot(taintedValue), () => Path.GetPathRoot(taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetPathRootTainted_ResultIsTainted()
    {
        AssertTaintedWithOriginalCallCheck(":+-" + Path.DirectorySeparatorChar + "-+:", () => Path.GetPathRoot(taintedPathValue + Path.DirectorySeparatorChar + taintedValue + "." + taintedValue), () => Path.GetPathRoot(taintedPathValue + Path.DirectorySeparatorChar + taintedValue + "." + taintedValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetPathRootTainted_ResultIsTainted2()
    {
        AssertTaintedWithOriginalCallCheck(":+-" + Path.DirectorySeparatorChar + "-+:", () => Path.GetPathRoot(fulltaintedPathValue), () => Path.GetPathRoot(fulltaintedPathValue));
        AssertNotVulnerable();
    }

#if NETCORE31 || NET50 || NET60 || NETCORE21
        [Fact]
        public void GivenAPath_WhenGetPathRootEmpty_ResultIsEmpty()
        {
            AssertOriginalInstrumentedSameResult(() => Path.GetPathRoot(String.Empty), () => Path.GetPathRoot(String.Empty));
        }
#else
    [TestMethod]
    //[ExpectedException(typeof(ArgumentException))]
    [TestCategory(testCategory)]
    public void GivenAPath_WhenGetPathRootEmpty_ResultIsEmpty()
    {
        AssertOriginalInstrumentedSameResult(() => Path.GetPathRoot(String.Empty), () => Path.GetPathRoot(String.Empty));
    }
#endif
    [Fact]
    public void GivenAPath_WhenGetPathRootNotTainted_ResultIsNotTainted()
    {
        AssertOriginalInstrumentedSameResult(() => Path.GetPathRoot("c:" + Path.DirectorySeparatorChar + taintedPathValue), () => Path.GetPathRoot("c:" + Path.DirectorySeparatorChar + taintedPathValue));
        AssertNotVulnerable();
    }

    [Fact]
    public void GivenAPath_WhenGetPathRootNull_ResultIsNotTainted()
    {
        AssertOriginalInstrumentedSameResult(() => Path.GetPathRoot(null), () => Path.GetPathRoot(null));
        AssertNotVulnerable();
    }
}
