using System;
using System.Collections;
using System.Diagnostics;
using NUnit.Framework;

namespace Samples.NUnitTests
{
    public class TestSuite
    {
        [OneTimeSetUp]
        public void Setup()
        {
            var writer = TestContext.Progress;
            writer.WriteLine($"Pid: {Process.GetCurrentProcess().Id}");
            writer.WriteLine("Environment Variables:");
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                writer.WriteLine($"  {entry.Key} = {entry.Value}");
            }
            writer.WriteLine(string.Empty);
        }
        
        [Test]
        public void SimplePassTest()
        {
        }

        [Test]
        [Ignore("Simple skip reason")]
        public void SimpleSkipFromAttributeTest()
        {
        }

        [Test]
        public void SimpleErrorTest()
        {
            int i = 0;
            int z = 0 / i;
        }

        // **********************************************************************************

        [Test]
        [Category("Category01")]
        [Property("Compatibility", "Windows")]
        [Property("Compatibility", "Linux")]
        public void TraitPassTest()
        {
        }

        [Test]
        [Ignore("Simple skip reason")]
        [Category("Category01")]
        [Property("Compatibility", "Windows")]
        [Property("Compatibility", "Linux")]
        public void TraitSkipFromAttributeTest()
        {
        }

        [Test]
        [Category("Category01")]
        [Property("Compatibility", "Windows")]
        [Property("Compatibility", "Linux")]
        public void TraitErrorTest()
        {
            int i = 0;
            int z = 0 / i;
        }

        // **********************************************************************************

        [Theory]
        [TestCase(1, 1, 2)]
        [TestCase(2, 2, 4)]
        [TestCase(3, 3, 6)]
        public void SimpleParameterizedTest(int xValue, int yValue, int expectedResult)
        {
            Assert.AreEqual(expectedResult, xValue + yValue);
        }


        [Theory]
        [Ignore("Simple skip reason")]
        [TestCase(1, 1, 2)]
        [TestCase(2, 2, 4)]
        [TestCase(3, 3, 6)]
        public void SimpleSkipParameterizedTest(int xValue, int yValue, int expectedResult)
        {
            Assert.AreEqual(expectedResult, xValue + yValue);
        }

        [Theory]
        [TestCase(1, 0, 2)]
        [TestCase(2, 0, 4)]
        [TestCase(3, 0, 6)]
        public void SimpleErrorParameterizedTest(int xValue, int yValue, int expectedResult)
        {
            Assert.AreEqual(expectedResult, xValue / yValue);
        }
    }
}
