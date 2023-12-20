using System;
using System.Collections;
using System.Diagnostics;
using NUnit.Framework;

namespace Samples.NUnitTests
{
    public class TestSuite
    {
        public const string SkippedByIntelligentTestRunnerReason = "Skipped by Datadog Intelligent Test Runner";

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
        [Category("ParemeterizedTest")]
        [TestCase(1, 1, 2, Category = "FirstCase")]
        [TestCase(2, 2, 4, Category = "SecondCase")]
        [TestCase(3, 3, 6, Category = "ThirdCase")]
        public void SimpleParameterizedTest(int xValue, int yValue, int expectedResult)
        {
#if NUNIT_4_0
            Assert.That(xValue + yValue, Is.EqualTo(expectedResult));
#else
            Assert.AreEqual(expectedResult, xValue + yValue);
#endif
        }


        [Theory]
        [Ignore("Simple skip reason")]
        [TestCase(1, 1, 2)]
        [TestCase(2, 2, 4)]
        [TestCase(3, 3, 6)]
        public void SimpleSkipParameterizedTest(int xValue, int yValue, int expectedResult)
        {
#if NUNIT_4_0
            Assert.That(xValue + yValue, Is.EqualTo(expectedResult));
#else            
            Assert.AreEqual(expectedResult, xValue + yValue);
#endif
        }

        [Theory]
        [TestCase(1, 0, 2)]
        [TestCase(2, 0, 4)]
        [TestCase(3, 0, 6)]
        public void SimpleErrorParameterizedTest(int xValue, int yValue, int expectedResult)
        {
#if NUNIT_4_0
            Assert.That(xValue / yValue, Is.EqualTo(expectedResult));
#else
            Assert.AreEqual(expectedResult, xValue / yValue);
#endif
        }

        // **********************************************************************************

        [Test]
        public void SimpleAssertPassTest()
        {
            Assert.Pass("The test passed.");
        }

        [Test]
        public void SimpleAssertInconclusive()
        {
            Assert.Inconclusive("The test is inconclusive.");
        }

        [Test]
        [Ignore(SkippedByIntelligentTestRunnerReason)]
        public void SkipByITRSimulation()
        {
        }

        [Test]
        [Property("datadog_itr_unskippable", "")]
        public void UnskippableTest()
        {
        }
    }

    [TestFixture("Test01")]
    [TestFixture("Test02")]
    public class TestFixtureTest
    {
        private string _name;

        public TestFixtureTest(string name)
        {
            _name = name;
        }

        [Test]
        public void Test()
        {
            Assert.Pass("Test is ok");
        }
    }

    public class TestBase<T>
    {
        [Test]
        public void IsNull()
        {
#if NUNIT_4_0
            Assert.That(default(T), Is.Null);
#else
            Assert.IsNull(default(T));
#endif
        }
    }

    public class TestString : TestBase<string>
    { }

    public class TestSetupError : TestBase<object>
    {
        [SetUp]
        public void Setup()
        {
            throw new Exception("SetUp exception.");
        }
        
        [Test]
        public void Test01()
        {
        }
        
        [Test]
        public void Test02()
        {
        }
        
        [Test]
        public void Test03()
        {
        }
        
        [Test]
        public void Test04()
        {
        }
        
        [Test]
        public void Test05()
        {
        }
    }

    public class TestFixtureSetupError : TestFixtureTest
    {
        public TestFixtureSetupError(string name)
            : base(name)
        {}
        
        [OneTimeSetUp]
        public void Setup()
        {
            throw new Exception("SetUp exception.");
        }
    }
    
    public class TestTearDownError : TestBase<object>
    {
        [TearDown]
        public void TearDown()
        {
            throw new Exception("TearDown exception.");
        }
    }

    public class TestTearDown2Error : TestBase<object>
    {
        [OneTimeTearDown]
        public void TearDown()
        {
            throw new Exception("TearDown exception.");
        }
    }

    [Property("datadog_itr_unskippable", "")]
    public class UnSkippableSuite
    {
        [Test]
        public void UnskippableTest()
        {
        }
    }
}
