// <copyright file="MessagePackBinaryExtensionsTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Collections.Generic;
using Datadog.Trace.Vendors.MessagePack;
using FluentAssertions;
using Xunit;

namespace Datadog.Trace.Tests.ExtensionMethods
{
    public class MessagePackBinaryExtensionsTests
    {
        public static IEnumerable<object[]> GetTestData()
        {
            var rnd = new Random(10);
            foreach (var size in new[] { 1, 5, 50, 100, 500, 1000, 5000, 10000, 50000 })
            {
                var values = new byte[size];
                rnd.NextBytes(values);

                for (var i = 0; i < 10;  i++)
                {
                    var offset = rnd.Next(size);
                    var count = size - offset;
                    yield return new object[] { values, offset, count };
                }
            }
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        public void WriteRawTest(byte[] values, int offset, int count)
        {
            // Declare MessagePack binary results
            var resultExpected = new byte[0];
            var resultActual = new byte[0];

            // Declare source data
            var expectedSource = new byte[count];
            Buffer.BlockCopy(values, offset, expectedSource, 0, count);

            var actualSource = new ReadOnlySpan<byte>(values, offset, count);

            expectedSource.Should().Equal(actualSource.ToArray());

            // Result
            var expectedWritten = MessagePackBinary.WriteRaw(ref resultExpected, 0, expectedSource);
            var actualWritten = MessagePackBinary.WriteRaw(ref resultActual, 0, actualSource);

            expectedWritten.Should().Be(actualWritten);
            resultExpected.Should().Equal(resultActual);
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        public void WriteBytesTest(byte[] values, int offset, int count)
        {
            // Declare MessagePack binary results
            var resultExpected = new byte[0];
            var resultActual = new byte[0];

            // Declare source data
            var expectedSource = new byte[count];
            Buffer.BlockCopy(values, offset, expectedSource, 0, count);

            var actualSource = new ReadOnlySpan<byte>(values, offset, count);

            expectedSource.Should().Equal(actualSource.ToArray());

            // Result
            var expectedWritten = MessagePackBinary.WriteBytes(ref resultExpected, 0, expectedSource);
            var actualWritten = MessagePackBinary.WriteBytes(ref resultActual, 0, actualSource);

            expectedWritten.Should().Be(actualWritten);
            resultExpected.Should().Equal(resultActual);
        }

        [SkippableTheory]
        [MemberData(nameof(GetTestData))]
        public void WriteStringBytesTest(byte[] values, int offset, int count)
        {
            // Declare MessagePack binary results
            var resultExpected = new byte[0];
            var resultActual = new byte[0];

            // Declare source data
            var expectedSource = new byte[count];
            Buffer.BlockCopy(values, offset, expectedSource, 0, count);

            var actualSource = new ReadOnlySpan<byte>(values, offset, count);

            expectedSource.Should().Equal(actualSource.ToArray());

            // Result
            var expectedWritten = MessagePackBinary.WriteStringBytes(ref resultExpected, 0, expectedSource);
            var actualWritten = MessagePackBinary.WriteStringBytes(ref resultActual, 0, actualSource);

            expectedWritten.Should().Be(actualWritten);
            resultExpected.Should().Equal(resultActual);
        }
    }
}
#endif
