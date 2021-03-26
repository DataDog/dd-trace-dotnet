using System;
using Xunit;

namespace Samples.XUnitTests
{
    public class TestSuite
    {
        [Fact]
        public void SimplePassTest()
        {
        }

        [Fact(Skip = "Simple skip reason")]
        public void SimpleSkipFromAttributeTest()
        {
        }

        [Fact]
        public void SimpleErrorTest()
        {
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
            Assert.Equal(expectedResult, xValue / yValue);
        }
    }
}
