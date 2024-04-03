using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Samples.MSTestTests;

[TestClass]
public class ClassInitializeExceptionTestSuite
{
    public TestContext TestContext { get; set; }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        throw new Exception("Class initialize exception");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestContext.WriteLine("Test Initialize");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        TestContext.WriteLine("Test Cleanup");
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
    }

    [TestMethod]
    public void ClassInitializeExceptionTestMethod()
    {
        TestContext.WriteLine("Hello World");
    }
}
