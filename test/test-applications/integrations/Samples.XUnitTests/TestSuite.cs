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
    }
}
