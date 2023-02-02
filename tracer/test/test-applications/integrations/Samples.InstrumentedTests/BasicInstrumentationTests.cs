// <copyright file="BasicInstrumentationTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Samples.InstrumentedTests.Iast.Vulnerabilities;
using Xunit;

namespace Samples.InstrumentedTests.Iast
{
    public class BasicInstrumentationTests : InstrumentationTestsBase
    {
        [Fact]
        public void GivenATaintedObject_WhenRequest_ObjectIsTainted()
        {
            string tainted = "tainted";
            AddTainted(tainted);
            AssertTainted(tainted);
        }

        [Fact]
        public void GivenANotTaintedObject_WhenRequest_ObjectIsNotTainted()
        {
            AssertNotTainted("nottainted");
        }
    }
}
