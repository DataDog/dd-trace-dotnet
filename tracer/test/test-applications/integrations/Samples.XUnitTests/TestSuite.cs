using System;
using System.Collections;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Samples.XUnitTests
{
    public class TestSuite
    {
        public const string SkippedByIntelligentTestRunnerReason = "Skipped by Datadog Intelligent Test Runner";
        private ITestOutputHelper _output;

        public TestSuite(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SimplePassTest()
        {
            _output.WriteLine("Test:SimplePassTest");
        }

        [Fact(Skip = "Simple skip reason")]
        public void SimpleSkipFromAttributeTest()
        {
        }

        [Fact]
        public void SimpleErrorTest()
        {
            _output.WriteLine("Test:SimpleErrorTest");
            int i = 0;
            int z = 0 / i;
        }

        // **********************************************************************************

        [Fact]
        [Trait("Category", "Category01")]
        [Trait("Compatibility", "Windows")]
        [Trait("Compatibility", "Linux")]
        public void TraitPassTest()
        {
            _output.WriteLine("Test:TraitPassTest");
        }

        [Fact(Skip = "Simple skip reason")]
        [Trait("Category", "Category01")]
        [Trait("Compatibility", "Windows")]
        [Trait("Compatibility", "Linux")]
        public void TraitSkipFromAttributeTest()
        {
        }

        [Fact]
        [Trait("Category", "Category01")]
        [Trait("Compatibility", "Windows")]
        [Trait("Compatibility", "Linux")]
        public void TraitErrorTest()
        {
            _output.WriteLine("Test:TraitErrorTest");
            int i = 0;
            int z = 0 / i;
        }

        // **********************************************************************************

        [Theory]
        [InlineData(1, 1, 2)]
        [InlineData(2, 2, 4)]
        [InlineData(3, 3, 6)]
        public void SimpleParameterizedTest(int xValue, int yValue, int expectedResult)
        {
            _output.WriteLine("Test:SimpleParameterizedTest");
            Assert.Equal(expectedResult, xValue + yValue);
        }


        [Theory(Skip = "Simple skip reason")]
        [InlineData(1, 1, 2)]
        [InlineData(2, 2, 4)]
        [InlineData(3, 3, 6)]
        public void SimpleSkipParameterizedTest(int xValue, int yValue, int expectedResult)
        {
            Assert.Equal(expectedResult, xValue + yValue);
        }

        [Theory]
        [InlineData(1, 0, 2)]
        [InlineData(2, 0, 4)]
        [InlineData(3, 0, 6)]
        public void SimpleErrorParameterizedTest(int xValue, int yValue, int expectedResult)
        {
            _output.WriteLine("Test:SimpleErrorParameterizedTest");
            Assert.Equal(expectedResult, xValue / yValue);
        }

        [Fact(Skip = SkippedByIntelligentTestRunnerReason)]
        public void SkipByITRSimulation()
        {
        }

        [Fact]
        [Trait("datadog_itr_unskippable", null)]
        public void UnskippableTest()
        {
        }
    }

    [Trait("datadog_itr_unskippable", null)]
    public class UnSkippableSuite
    {
        [Fact]
        public void UnskippableTest()
        {
        }
    }
}
