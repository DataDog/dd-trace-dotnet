// <copyright file="IastUtilsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Iast;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Security.Unit.Tests.IAST
{
    public class IastUtilsTests
    {
        [Fact]
        public void GivenAHashCalcutation_WhenGetHashCodeAndGetHashCodeForArray_ResultsAreTheSame()
        {
            var elem1 = new Range();
            var elem2 = 33;
            var elem3 = "WWW";
            IastUtils.GetHashCode(elem1, elem2, elem3).Should().Be(IastUtils.GetHashCodeForArray(new object[] { elem1, elem2, elem3 }));
        }

        [Fact]
        public void GivenAHashCalcutation_WhenGetHashCodeAndGetHashCodeForArray_ResultsAreTheSame2()
        {
            var elem2 = 33;
            var elem3 = new object[] { elem2 };
            IastUtils.GetHashCode(elem3).Should().Be(IastUtils.GetHashCodeForArray(new object[] { elem3 }));
        }

        [Fact]
        public void GivenAHashCalcutation_WhenGetHashCodeAndGetHashCodeForArray_ResultsAreTheSame3()
        {
            var elem1 = new Range();
            var elem2 = 33;
            var elem3 = new object[] { elem1, elem2 };
            IastUtils.GetHashCode(elem1, elem3).Should().Be(IastUtils.GetHashCodeForArray(new object[] { elem1, elem3 }));
        }

        [Fact]
        public void GivenAHashCalcutation_WhenGetHashCodeAndGetHashCodeForArray_ResultsAreTheSame4()
        {
            var elem3 = new object[1];
            elem3[0] = 33;
            IastUtils.GetHashCode(elem3).Should().Be(IastUtils.GetHashCodeForArray(new object[] { elem3 }));
        }

        [Fact]
        public void GivenAHashCalcutation_WhenGetHashCodeAndGetHashCodeForArray_ResultsAreTheSame5()
        {
            var elem3 = new object[1];
            elem3[0] = elem3;
            IastUtils.GetHashCode(elem3).Should().Be(IastUtils.GetHashCodeForArray(new object[] { elem3 }));
        }
    }
}
