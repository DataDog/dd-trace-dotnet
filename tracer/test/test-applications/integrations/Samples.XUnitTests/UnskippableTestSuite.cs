using System;
using System.Collections;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Samples.XUnitTests
{
    [Trait("datadog_itr_unskippable", null)]
    public class UnSkippableSuite
    {
        [Fact]
        public void UnskippableTest()
        {
        }
    }
}
