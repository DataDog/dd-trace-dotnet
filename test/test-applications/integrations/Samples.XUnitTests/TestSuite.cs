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
    }
}
