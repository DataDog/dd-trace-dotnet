using System;
using System.Collections;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Samples.MSTestTests
{
    [TestClass]
    public class TestSuite
    {
        [AssemblyInitialize]
        public static void AssemblyInit(TestContext context)
        {
            context.WriteLine($"Pid: {Process.GetCurrentProcess().Id}");
            context.WriteLine("Environment Variables:");
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                context.WriteLine($"  {entry.Key} = {entry.Value}");
            }
            context.WriteLine(string.Empty);
        }
        
        [TestMethod]
        public void SimplePassTest()
        {
        }

        [TestMethod]
        [Ignore("Simple skip reason")]
        public void SimpleSkipFromAttributeTest()
        {
        }

        [TestMethod]
        public void SimpleErrorTest()
        {
            int i = 0;
            int z = 0 / i;
        }

        // **********************************************************************************

        [TestMethod]
        [TestCategory("Category01")]
        [TestProperty("Compatibility", "Windows")]
        [TestProperty("Compatibility", "Linux")]
        public void TraitPassTest()
        {
        }

        [TestMethod]
        [Ignore("Simple skip reason")]
        [TestCategory("Category01")]
        [TestProperty("Compatibility", "Windows")]
        [TestProperty("Compatibility", "Linux")]
        public void TraitSkipFromAttributeTest()
        {
        }

        [TestMethod]
        [TestCategory("Category01")]
        [TestProperty("Compatibility", "Windows")]
        [TestProperty("Compatibility", "Linux")]
        public void TraitErrorTest()
        {
            int i = 0;
            int z = 0 / i;
        }

        // **********************************************************************************

        [DataTestMethod]
        [DataRow(1, 1, 2)]
        [DataRow(2, 2, 4)]
        [DataRow(3, 3, 6)]
        public void SimpleParameterizedTest(int xValue, int yValue, int expectedResult)
        {
            Assert.AreEqual(expectedResult, xValue + yValue);
        }


        [DataTestMethod]
        [Ignore("Simple skip reason")]
        [DataRow(1, 1, 2)]
        [DataRow(2, 2, 4)]
        [DataRow(3, 3, 6)]
        public void SimpleSkipParameterizedTest(int xValue, int yValue, int expectedResult)
        {
            Assert.AreEqual(expectedResult, xValue + yValue);
        }

        [DataTestMethod]
        [DataRow(1, 0, 2)]
        [DataRow(2, 0, 4)]
        [DataRow(3, 0, 6)]
        public void SimpleErrorParameterizedTest(int xValue, int yValue, int expectedResult)
        {
            Assert.AreEqual(expectedResult, xValue / yValue);
        }
    }
}
