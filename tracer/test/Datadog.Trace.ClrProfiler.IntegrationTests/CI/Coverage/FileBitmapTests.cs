// <copyright file="FileBitmapTests.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

using Datadog.Trace.Ci.Coverage.Util;
using Xunit;

namespace Datadog.Trace.ClrProfiler.IntegrationTests.CI.Coverage;

public class FileBitmapTests
{
    [Fact]
    public void Constructor_WithSize_CreatesEmptyBitmap()
    {
        var lines = 135;
        var size = FileBitmap.GetSize(lines);
        using var bitmap = new FileBitmap(new byte[size]);

        Assert.Equal(size, bitmap.Size);
        for (var i = 0; i < lines; i++)
        {
            Assert.False(bitmap.Get(i + 1));
        }
    }

    [Fact]
    public void Set_SingleBit_SetsBitCorrectly()
    {
        using var bitmap = new FileBitmap(new byte[1]);
        bitmap.Set(1); // Set the first bit

        Assert.True(bitmap.Get(1));
    }

    [Fact]
    public void CountActiveBits_NoBitsSet_ReturnsZero()
    {
        using var bitmap = new FileBitmap(new byte[1]);

        Assert.Equal(0, bitmap.CountActiveBits());
    }

    [Fact]
    public void CountActiveBits_OneBitSet_ReturnsOne()
    {
        using var bitmap = new FileBitmap(new byte[1]);
        bitmap.Set(1); // Set the first bit

        Assert.Equal(1, bitmap.CountActiveBits());
    }

    [Fact]
    public void BitwiseOr_TwoBitmaps_CombinesCorrectly()
    {
        using var bitmapA = new FileBitmap([0b_0000_0001]);
        using var bitmapB = new FileBitmap([0b_0000_0010]);
        using var resultBitmap = bitmapA | bitmapB;

        Assert.Equal(0b_0000_0011, resultBitmap.ToArray()[0]);
    }

    [Fact]
    public void BitwiseAnd_TwoBitmaps_IntersectsCorrectly()
    {
        using var bitmapA = new FileBitmap([0b_0000_0011]);
        using var bitmapB = new FileBitmap([0b_0000_0010]);
        using var resultBitmap = bitmapA & bitmapB;

        Assert.Equal(0b_0000_0010, resultBitmap.ToArray()[0]);
    }

    [Fact]
    public void BitwiseNot_SingleBitmap_InvertsCorrectly()
    {
        using var bitmap = new FileBitmap([0b_1111_1110]);
        using var resultBitmap = ~bitmap;

        Assert.Equal(0b_0000_0001, resultBitmap.ToArray()[0]);
    }

    [Fact]
    public void LargeBitmap_BitwiseOperations_HandleCorrectly()
    {
        // Create two large bitmaps with specific patterns
        var size = 1024; // 1 KB
        using var bitmapA = new FileBitmap(new byte[size]);
        using var bitmapB = new FileBitmap(new byte[size]);

        // Set alternating bits in bitmapA and the inverse in bitmapB
        for (var i = 1; i <= size * 8; i += 2)
        {
            bitmapA.Set(i);
            bitmapB.Set(i + 1);
        }

        using var resultOr = bitmapA | bitmapB;
        using var resultAnd = bitmapA & bitmapB;

        // All bits should be set in the OR operation
        Assert.Equal(size * 8, resultOr.CountActiveBits());

        // No bits should be set in the AND operation
        Assert.Equal(0, resultAnd.CountActiveBits());
    }

    [Fact]
    public void BitwiseNot_ComplexPattern_InvertsCorrectly()
    {
        // Create a bitmap with a complex repeating pattern
        var size = 256; // 256 bytes
        var pattern = new byte[size];
        for (var i = 0; i < size; i++)
        {
            // Pattern with alternating bits, varying by index
            pattern[i] = (byte)((i % 2 == 0) ? 0xAA : 0x55);
        }

        using var bitmap = new FileBitmap(pattern);
        using var invertedBitmap = ~bitmap;

        // Verify that every bit is flipped correctly
        for (var i = 0; i < size * 8; i++)
        {
            var originalBit = bitmap.Get(i + 1);
            var invertedBit = invertedBitmap.Get(i + 1);
            Assert.NotEqual(originalBit, invertedBit);
        }
    }

    [Theory]
    [InlineData(new[] { 1, 8 }, new byte[] { 0b10000001, 0x00, 0x00, 0x00 })]
    [InlineData(new[] { 9, 32 }, new byte[] { 0x00, 0b10000000, 0x00, 0b00000001 })]
    [InlineData(new[] { 1, 8, 9, 32 }, new byte[] { 0b10000001, 0b10000000, 0x00, 0b00000001 })]
    public void ToArray_WithVariousBitSets_ReturnsExpectedByteArray(int[] bitsToSet, byte[] expected)
    {
        // Given a FileBitmap with a specific size
        var bitmap = new FileBitmap(new byte[4]);

        // When setting specific bits
        foreach (var bit in bitsToSet)
        {
            bitmap.Set(bit);
        }

        // Then, converting to a byte array should match the expected result
        var actual = bitmap.ToArray();

        Assert.Equal(expected.Length, actual.Length); // Verify the arrays are of the same length
        for (var i = 0; i < expected.Length; i++)
        {
            Assert.Equal(expected[i], actual[i]); // Verify each byte matches
        }
    }

    [Theory]
    [InlineData(new byte[] { 0b10101010 }, new[] { true, false, true, false, true, false, true, false })]
    [InlineData(new byte[] { 0b11110000 }, new[] { true, true, true, true, false, false, false, false })]
    [InlineData(new byte[] { 0b00000000, 0b11111111 }, new[] { false, false, false, false, false, false, false, false, true, true, true, true, true, true, true, true })]
    public void Enumerator_CorrectlyIteratesOverBits(byte[] bitmapBytes, bool[] expectedBitStates)
    {
        // Initialize a new FileBitmap with the provided bytes
        using var bitmap = new FileBitmap(bitmapBytes);

        // Use the GetEnumerator method of FileBitmap to iterate over the bits
        var enumerator = bitmap.GetEnumerator();

        var index = 0; // Keep track of the current bit index
        while (enumerator.MoveNext())
        {
            // Convert the current byte to its bit representation
            var currentByte = enumerator.Current;
            for (int bitPosition = 0; bitPosition < 8; bitPosition++)
            {
                // Calculate the bit state by shifting the currentByte right by bitPosition and checking the least significant bit
                var bitState = (currentByte & (1 << (7 - bitPosition))) != 0;

                // Ensure we do not exceed the expectedBitStates length
                if (index < expectedBitStates.Length)
                {
                    // Assert that the current bit state matches the expected state
                    Assert.Equal(expectedBitStates[index], bitState);
                }

                index++;
            }
        }

        // Assert that we have iterated over the correct number of bits
        Assert.Equal(expectedBitStates.Length, index);
    }
}
