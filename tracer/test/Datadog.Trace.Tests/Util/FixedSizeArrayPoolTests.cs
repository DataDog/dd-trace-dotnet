// <copyright file="FixedSizeArrayPoolTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using System;
using Datadog.Trace.Util;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.Util
{
    public class FixedSizeArrayPoolTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Constructor_InvalidArrayItems_Throws(int size)
        {
            Action action = () => new FixedSizeArrayPool<int>(size);

            action.Should()
                  .Throw<ArgumentOutOfRangeException>()
                  .WithParameterName("arrayItems");
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        public void Get_ReturnsArrayOfExpectedLength(int size)
        {
            var pool = new FixedSizeArrayPool<int>(size);

            using var item = pool.Get();

            item.Array.Should().HaveCount(size);
        }

        [Fact]
        public void Get_WhenPoolEmpty_ReturnsDistinctArrays()
        {
            var pool = new FixedSizeArrayPool<int>(2);

            using var item1 = pool.Get();
            using var item2 = pool.Get();

            item1.Array.Should().NotBeSameAs(item2.Array);
        }

        [Fact]
        public void Return_ReusesArrayFromFastPath_AndClearsReferenceTypes()
        {
            var pool = new FixedSizeArrayPool<string>(2);
            string[] array;

            using (var item = pool.Get())
            {
                array = item.Array;
                array[0] = "value";
                array[1] = "other";
            }

            using var item2 = pool.Get();

            item2.Array.Should().BeSameAs(array);
            item2.Array[0].Should().BeNull();
            item2.Array[1].Should().BeNull();
        }

        [Fact]
        public void Return_UsesStackAfterFastPath()
        {
            var pool = new FixedSizeArrayPool<int>(1);

            var item1 = pool.Get();
            var item2 = pool.Get();
            var array1 = item1.Array;
            var array2 = item2.Array;

            item1.Dispose();
            item2.Dispose();

            using var first = pool.Get();
            using var second = pool.Get();

            first.Array.Should().BeSameAs(array1);
            second.Array.Should().BeSameAs(array2);
        }

        [Fact]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            var pool = new FixedSizeArrayPool<int>(1);
            var item = pool.Get();

            item.Dispose();
            item.Dispose();
        }

        [Fact]
        public void Return_WrongLength_Throws()
        {
            var pool = new FixedSizeArrayPool<int>(2);

            Action action = () => ReturnWrongLengthArray(pool);

            action.Should()
                  .Throw<ArgumentOutOfRangeException>()
                  .WithParameterName("value");
        }

        private static void ReturnWrongLengthArray(FixedSizeArrayPool<int> pool)
        {
            var item = new FixedSizeArrayPool<int>.ArrayPoolItem(new int[1], pool);
            item.Dispose();
        }
    }
}
