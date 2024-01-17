﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Internal.MemoryBlock
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using Datadog.System.Reflection.Metadata;
using Datadog.System.Reflection.Metadata.Ecma335;


#nullable enable
namespace Datadog.System.Reflection.Internal
{
    [DebuggerDisplay("{GetDebuggerDisplay(),nq}")]
  internal readonly struct MemoryBlock
  {
    internal readonly unsafe byte* Pointer;
    internal readonly int Length;

    internal unsafe MemoryBlock(byte* buffer, int length)
    {
      this.Pointer = buffer;
      this.Length = length;
    }

    internal static unsafe MemoryBlock CreateChecked(byte* buffer, int length)
    {
      if (length < 0)
        throw new ArgumentOutOfRangeException(nameof (length));
      if ((IntPtr) buffer == IntPtr.Zero && length != 0)
        Throw.ArgumentNull(nameof (buffer));
      return new MemoryBlock(buffer, length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckBounds(int offset, int byteCount)
    {
      if ((ulong) (uint) offset + (ulong) (uint) byteCount <= (ulong) this.Length)
        return;
      Throw.OutOfBounds();
    }

    internal unsafe byte[]? ToArray() => (IntPtr) this.Pointer != IntPtr.Zero ? this.PeekBytes(0, this.Length) : (byte[]) null;


    #nullable disable
    private unsafe string GetDebuggerDisplay() => (IntPtr) this.Pointer == IntPtr.Zero ? "<null>" : this.GetDebuggerDisplay(out int _);


    #nullable enable
    internal string GetDebuggerDisplay(out int displayedBytes)
    {
      displayedBytes = Math.Min(this.Length, 64);
      string debuggerDisplay = BitConverter.ToString(this.PeekBytes(0, displayedBytes));
      if (displayedBytes < this.Length)
        debuggerDisplay += "-...";
      return debuggerDisplay;
    }

    internal unsafe string GetDebuggerDisplay(int offset)
    {
      if ((IntPtr) this.Pointer == IntPtr.Zero)
        return "<null>";
      int displayedBytes;
      string debuggerDisplay = this.GetDebuggerDisplay(out displayedBytes);
      return offset >= displayedBytes ? (displayedBytes != this.Length ? debuggerDisplay + "*..." : debuggerDisplay + "*") : debuggerDisplay.Insert(offset * 3, "*");
    }

    internal unsafe MemoryBlock GetMemoryBlockAt(int offset, int length)
    {
      this.CheckBounds(offset, length);
      return new MemoryBlock(this.Pointer + offset, length);
    }

    internal unsafe byte PeekByte(int offset)
    {
      this.CheckBounds(offset, 1);
      return this.Pointer[offset];
    }

    internal int PeekInt32(int offset)
    {
      uint num = this.PeekUInt32(offset);
      if ((long) (int) num != (long) num)
        Throw.ValueOverflow();
      return (int) num;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe uint PeekUInt32(int offset)
    {
      this.CheckBounds(offset, 4);
      byte* numPtr = this.Pointer + offset;
      return (uint) ((int) *numPtr | (int) numPtr[1] << 8 | (int) numPtr[2] << 16 | (int) numPtr[3] << 24);
    }

    /// <summary>
    /// Decodes a compressed integer value starting at offset.
    /// See Metadata Specification section II.23.2: Blobs and signatures.
    /// </summary>
    /// <param name="offset">Offset to the start of the compressed data.</param>
    /// <param name="numberOfBytesRead">Bytes actually read.</param>
    /// <returns>
    /// Value between 0 and 0x1fffffff, or <see cref="F:System.Reflection.Metadata.BlobReader.InvalidCompressedInteger" /> if the value encoding is invalid.
    /// </returns>
    internal unsafe int PeekCompressedInteger(int offset, out int numberOfBytesRead)
    {
      this.CheckBounds(offset, 0);
      byte* numPtr = this.Pointer + offset;
      long num1 = (long) (this.Length - offset);
      if (num1 == 0L)
      {
        numberOfBytesRead = 0;
        return int.MaxValue;
      }
      byte num2 = *numPtr;
      if (((int) num2 & 128) == 0)
      {
        numberOfBytesRead = 1;
        return (int) num2;
      }
      if (((int) num2 & 64) == 0)
      {
        if (num1 >= 2L)
        {
          numberOfBytesRead = 2;
          return ((int) num2 & 63) << 8 | (int) numPtr[1];
        }
      }
      else if (((int) num2 & 32) == 0 && num1 >= 4L)
      {
        numberOfBytesRead = 4;
        return ((int) num2 & 31) << 24 | (int) numPtr[1] << 16 | (int) numPtr[2] << 8 | (int) numPtr[3];
      }
      numberOfBytesRead = 0;
      return int.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal unsafe ushort PeekUInt16(int offset)
    {
      this.CheckBounds(offset, 2);
      byte* numPtr = this.Pointer + offset;
      return (ushort) ((uint) *numPtr | (uint) numPtr[1] << 8);
    }

    internal uint PeekTaggedReference(int offset, bool smallRefSize) => this.PeekReferenceUnchecked(offset, smallRefSize);

    internal uint PeekReferenceUnchecked(int offset, bool smallRefSize) => !smallRefSize ? this.PeekUInt32(offset) : (uint) this.PeekUInt16(offset);

    internal int PeekReference(int offset, bool smallRefSize)
    {
      if (smallRefSize)
        return (int) this.PeekUInt16(offset);
      uint rowId = this.PeekUInt32(offset);
      if (!TokenTypeIds.IsValidRowId(rowId))
        Throw.ReferenceOverflow();
      return (int) rowId;
    }

    internal int PeekHeapReference(int offset, bool smallRefSize)
    {
      if (smallRefSize)
        return (int) this.PeekUInt16(offset);
      uint offset1 = this.PeekUInt32(offset);
      if (!HeapHandleType.IsValidHeapOffset(offset1))
        Throw.ReferenceOverflow();
      return (int) offset1;
    }

    internal unsafe Guid PeekGuid(int offset)
    {
      this.CheckBounds(offset, sizeof (Guid));
      byte* numPtr = this.Pointer + offset;
      return BitConverter.IsLittleEndian ? *(Guid*) numPtr : new Guid((int) *numPtr | (int) numPtr[1] << 8 | (int) numPtr[2] << 16 | (int) numPtr[3] << 24, (short) ((int) numPtr[4] | (int) numPtr[5] << 8), (short) ((int) numPtr[6] | (int) numPtr[7] << 8), numPtr[8], numPtr[9], numPtr[10], numPtr[11], numPtr[12], numPtr[13], numPtr[14], numPtr[15]);
    }

    internal unsafe string PeekUtf16(int offset, int byteCount)
    {
      this.CheckBounds(offset, byteCount);
      byte* bytes = this.Pointer + offset;
      return BitConverter.IsLittleEndian ? new string((char*) bytes, 0, byteCount / 2) : Encoding.Unicode.GetString(bytes, byteCount);
    }

    internal unsafe string PeekUtf8(int offset, int byteCount)
    {
      this.CheckBounds(offset, byteCount);
      return Encoding.UTF8.GetString(this.Pointer + offset, byteCount);
    }

    /// <summary>
    /// Read UTF8 at the given offset up to the given terminator, null terminator, or end-of-block.
    /// </summary>
    /// <param name="offset">Offset in to the block where the UTF8 bytes start.</param>
    /// <param name="prefix">UTF8 encoded prefix to prepend to the bytes at the offset before decoding.</param>
    /// <param name="utf8Decoder">The UTF8 decoder to use that allows user to adjust fallback and/or reuse existing strings without allocating a new one.</param>
    /// <param name="numberOfBytesRead">The number of bytes read, which includes the terminator if we did not hit the end of the block.</param>
    /// <param name="terminator">A character in the ASCII range that marks the end of the string.
    /// If a value other than '\0' is passed we still stop at the null terminator if encountered first.</param>
    /// <returns>The decoded string.</returns>
    internal unsafe string PeekUtf8NullTerminated(
      int offset,
      byte[]? prefix,
      MetadataStringDecoder utf8Decoder,
      out int numberOfBytesRead,
      char terminator = '\0')
    {
      this.CheckBounds(offset, 0);
      int terminatedLength = this.GetUtf8NullTerminatedLength(offset, out numberOfBytesRead, terminator);
      return EncodingHelper.DecodeUtf8(this.Pointer + offset, terminatedLength, prefix, utf8Decoder);
    }

    /// <summary>
    /// Get number of bytes from offset to given terminator, null terminator, or end-of-block (whichever comes first).
    /// Returned length does not include the terminator, but numberOfBytesRead out parameter does.
    /// </summary>
    /// <param name="offset">Offset in to the block where the UTF8 bytes start.</param>
    /// <param name="terminator">A character in the ASCII range that marks the end of the string.
    /// If a value other than '\0' is passed we still stop at the null terminator if encountered first.</param>
    /// <param name="numberOfBytesRead">The number of bytes read, which includes the terminator if we did not hit the end of the block.</param>
    /// <returns>Length (byte count) not including terminator.</returns>
    internal unsafe int GetUtf8NullTerminatedLength(
      int offset,
      out int numberOfBytesRead,
      char terminator = '\0')
    {
      this.CheckBounds(offset, 0);
      byte* numPtr1 = this.Pointer + offset;
      byte* numPtr2 = this.Pointer + this.Length;
      byte* numPtr3;
      for (numPtr3 = numPtr1; numPtr3 < numPtr2; ++numPtr3)
      {
        byte num = *numPtr3;
        if (num == (byte) 0 || (int) num == (int) terminator)
          break;
      }
      int terminatedLength = (int) (numPtr3 - numPtr1);
      numberOfBytesRead = terminatedLength;
      if (numPtr3 < numPtr2)
        ++numberOfBytesRead;
      return terminatedLength;
    }

    internal unsafe int Utf8NullTerminatedOffsetOfAsciiChar(int startOffset, char asciiChar)
    {
      this.CheckBounds(startOffset, 0);
      for (int index = startOffset; index < this.Length; ++index)
      {
        byte num = this.Pointer[index];
        if (num != (byte) 0)
        {
          if ((int) num == (int) asciiChar)
            return index;
        }
        else
          break;
      }
      return -1;
    }

    internal bool Utf8NullTerminatedEquals(
      int offset,
      string text,
      MetadataStringDecoder utf8Decoder,
      char terminator,
      bool ignoreCase)
    {
      int num;
      MemoryBlock.FastComparisonResult comparisonResult = this.Utf8NullTerminatedFastCompare(offset, text, 0, out num, terminator, ignoreCase);
      return comparisonResult == MemoryBlock.FastComparisonResult.Inconclusive ? this.PeekUtf8NullTerminated(offset, (byte[]) null, utf8Decoder, out num, terminator).Equals(text, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) : comparisonResult == MemoryBlock.FastComparisonResult.Equal;
    }

    internal bool Utf8NullTerminatedStartsWith(
      int offset,
      string text,
      MetadataStringDecoder utf8Decoder,
      char terminator,
      bool ignoreCase)
    {
      int num;
      switch (this.Utf8NullTerminatedFastCompare(offset, text, 0, out num, terminator, ignoreCase))
      {
        case MemoryBlock.FastComparisonResult.Equal:
        case MemoryBlock.FastComparisonResult.BytesStartWithText:
          return true;
        case MemoryBlock.FastComparisonResult.TextStartsWithBytes:
        case MemoryBlock.FastComparisonResult.Unequal:
          return false;
        default:
          return this.PeekUtf8NullTerminated(offset, (byte[]) null, utf8Decoder, out num, terminator).StartsWith(text, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
      }
    }

    internal unsafe MemoryBlock.FastComparisonResult Utf8NullTerminatedFastCompare(
      int offset,
      string text,
      int textStart,
      out int firstDifferenceIndex,
      char terminator,
      bool ignoreCase)
    {
      this.CheckBounds(offset, 0);
      byte* numPtr1 = this.Pointer + offset;
      byte* numPtr2 = this.Pointer + this.Length;
      byte* numPtr3 = numPtr1;
      int ignoreCaseMask = StringUtils.IgnoreCaseMask(ignoreCase);
      int index;
      for (index = textStart; index < text.Length && numPtr3 != numPtr2; ++numPtr3)
      {
        byte b = *numPtr3;
        if (b != (byte) 0 && (int) b != (int) terminator)
        {
          char a = text[index];
          if (((int) b & 128) == 0 && StringUtils.IsEqualAscii((int) a, (int) b, ignoreCaseMask))
          {
            ++index;
          }
          else
          {
            firstDifferenceIndex = index;
            return a <= '\u007F' ? MemoryBlock.FastComparisonResult.Unequal : MemoryBlock.FastComparisonResult.Inconclusive;
          }
        }
        else
          break;
      }
      firstDifferenceIndex = index;
      bool flag1 = index == text.Length;
      bool flag2 = numPtr3 == numPtr2 || *numPtr3 == (byte) 0 || (int) *numPtr3 == (int) terminator;
      if (flag1 & flag2)
        return MemoryBlock.FastComparisonResult.Equal;
      return !flag1 ? MemoryBlock.FastComparisonResult.TextStartsWithBytes : MemoryBlock.FastComparisonResult.BytesStartWithText;
    }

    internal unsafe bool Utf8NullTerminatedStringStartsWithAsciiPrefix(
      int offset,
      string asciiPrefix)
    {
      this.CheckBounds(offset, 0);
      if (asciiPrefix.Length > this.Length - offset)
        return false;
      byte* numPtr = this.Pointer + offset;
      for (int index = 0; index < asciiPrefix.Length; ++index)
      {
        if ((int) asciiPrefix[index] != (int) *numPtr)
          return false;
        ++numPtr;
      }
      return true;
    }

    internal unsafe int CompareUtf8NullTerminatedStringWithAsciiString(
      int offset,
      string asciiString)
    {
      this.CheckBounds(offset, 0);
      byte* numPtr = this.Pointer + offset;
      int num = this.Length - offset;
      for (int index = 0; index < asciiString.Length; ++index)
      {
        if (index > num)
          return -1;
        if ((int) *numPtr != (int) asciiString[index])
          return (int) *numPtr - (int) asciiString[index];
        ++numPtr;
      }
      return *numPtr != (byte) 0 ? 1 : 0;
    }

    internal unsafe byte[] PeekBytes(int offset, int byteCount)
    {
      this.CheckBounds(offset, byteCount);
      return BlobUtilities.ReadBytes(this.Pointer + offset, byteCount);
    }

    internal int IndexOf(byte b, int start)
    {
      this.CheckBounds(start, 0);
      return this.IndexOfUnchecked(b, start);
    }

    internal unsafe int IndexOfUnchecked(byte b, int start)
    {
      byte* numPtr1 = this.Pointer + start;
      for (byte* numPtr2 = this.Pointer + this.Length; numPtr1 < numPtr2; ++numPtr1)
      {
        if ((int) *numPtr1 == (int) b)
          return (int) (numPtr1 - this.Pointer);
      }
      return -1;
    }

    internal int BinarySearch(string[] asciiKeys, int offset)
    {
      int num1 = 0;
      int num2 = asciiKeys.Length - 1;
      while (num1 <= num2)
      {
        int index = num1 + (num2 - num1 >> 1);
        string asciiKey = asciiKeys[index];
        int num3 = this.CompareUtf8NullTerminatedStringWithAsciiString(offset, asciiKey);
        if (num3 == 0)
          return index;
        if (num3 < 0)
          num2 = index - 1;
        else
          num1 = index + 1;
      }
      return ~num1;
    }

    /// <summary>
    /// In a table that specifies children via a list field (e.g. TypeDef.FieldList, TypeDef.MethodList),
    /// searches for the parent given a reference to a child.
    /// </summary>
    /// <returns>Returns row number [0..RowCount).</returns>
    internal int BinarySearchForSlot(
      int rowCount,
      int rowSize,
      int referenceListOffset,
      uint referenceValue,
      bool isReferenceSmall)
    {
      int num1 = 0;
      int num2 = rowCount - 1;
      uint num3 = this.PeekReferenceUnchecked(num1 * rowSize + referenceListOffset, isReferenceSmall);
      uint num4 = this.PeekReferenceUnchecked(num2 * rowSize + referenceListOffset, isReferenceSmall);
      if (num2 == 1)
        return referenceValue >= num4 ? num2 : num1;
      while (num2 - num1 > 1)
      {
        if (referenceValue <= num3)
          return (int) referenceValue != (int) num3 ? num1 - 1 : num1;
        if (referenceValue >= num4)
          return (int) referenceValue != (int) num4 ? num2 + 1 : num2;
        int num5 = (num1 + num2) / 2;
        uint num6 = this.PeekReferenceUnchecked(num5 * rowSize + referenceListOffset, isReferenceSmall);
        if (referenceValue > num6)
        {
          num1 = num5;
          num3 = num6;
        }
        else
        {
          if (referenceValue >= num6)
            return num5;
          num2 = num5;
          num4 = num6;
        }
      }
      return num1;
    }

    /// <summary>
    /// In a table ordered by a column containing entity references searches for a row with the specified reference.
    /// </summary>
    /// <returns>Returns row number [0..RowCount) or -1 if not found.</returns>
    internal int BinarySearchReference(
      int rowCount,
      int rowSize,
      int referenceOffset,
      uint referenceValue,
      bool isReferenceSmall)
    {
      int num1 = 0;
      int num2 = rowCount - 1;
      while (num1 <= num2)
      {
        int num3 = (num1 + num2) / 2;
        uint num4 = this.PeekReferenceUnchecked(num3 * rowSize + referenceOffset, isReferenceSmall);
        if (referenceValue > num4)
        {
          num1 = num3 + 1;
        }
        else
        {
          if (referenceValue >= num4)
            return num3;
          num2 = num3 - 1;
        }
      }
      return -1;
    }

    internal int BinarySearchReference(
      int[] ptrTable,
      int rowSize,
      int referenceOffset,
      uint referenceValue,
      bool isReferenceSmall)
    {
      int num1 = 0;
      int num2 = ptrTable.Length - 1;
      while (num1 <= num2)
      {
        int index = (num1 + num2) / 2;
        uint num3 = this.PeekReferenceUnchecked((ptrTable[index] - 1) * rowSize + referenceOffset, isReferenceSmall);
        if (referenceValue > num3)
        {
          num1 = index + 1;
        }
        else
        {
          if (referenceValue >= num3)
            return index;
          num2 = index - 1;
        }
      }
      return -1;
    }

    /// <summary>
    /// Calculates a range of rows that have specified value in the specified column in a table that is sorted by that column.
    /// </summary>
    internal void BinarySearchReferenceRange(
      int rowCount,
      int rowSize,
      int referenceOffset,
      uint referenceValue,
      bool isReferenceSmall,
      out int startRowNumber,
      out int endRowNumber)
    {
      int num = this.BinarySearchReference(rowCount, rowSize, referenceOffset, referenceValue, isReferenceSmall);
      if (num == -1)
      {
        startRowNumber = -1;
        endRowNumber = -1;
      }
      else
      {
        startRowNumber = num;
        while (startRowNumber > 0 && (int) this.PeekReferenceUnchecked((startRowNumber - 1) * rowSize + referenceOffset, isReferenceSmall) == (int) referenceValue)
          --startRowNumber;
        endRowNumber = num;
        while (endRowNumber + 1 < rowCount && (int) this.PeekReferenceUnchecked((endRowNumber + 1) * rowSize + referenceOffset, isReferenceSmall) == (int) referenceValue)
          ++endRowNumber;
      }
    }

    /// <summary>
    /// Calculates a range of rows that have specified value in the specified column in a table that is sorted by that column.
    /// </summary>
    internal void BinarySearchReferenceRange(
      int[] ptrTable,
      int rowSize,
      int referenceOffset,
      uint referenceValue,
      bool isReferenceSmall,
      out int startRowNumber,
      out int endRowNumber)
    {
      int num = this.BinarySearchReference(ptrTable, rowSize, referenceOffset, referenceValue, isReferenceSmall);
      if (num == -1)
      {
        startRowNumber = -1;
        endRowNumber = -1;
      }
      else
      {
        startRowNumber = num;
        while (startRowNumber > 0 && (int) this.PeekReferenceUnchecked((ptrTable[startRowNumber - 1] - 1) * rowSize + referenceOffset, isReferenceSmall) == (int) referenceValue)
          --startRowNumber;
        endRowNumber = num;
        while (endRowNumber + 1 < ptrTable.Length && (int) this.PeekReferenceUnchecked((ptrTable[endRowNumber + 1] - 1) * rowSize + referenceOffset, isReferenceSmall) == (int) referenceValue)
          ++endRowNumber;
      }
    }

    internal int LinearSearchReference(
      int rowSize,
      int referenceOffset,
      uint referenceValue,
      bool isReferenceSmall)
    {
      int offset = referenceOffset;
      for (int length = this.Length; offset < length; offset += rowSize)
      {
        if ((int) this.PeekReferenceUnchecked(offset, isReferenceSmall) == (int) referenceValue)
          return offset / rowSize;
      }
      return -1;
    }

    internal bool IsOrderedByReferenceAscending(
      int rowSize,
      int referenceOffset,
      bool isReferenceSmall)
    {
      int offset = referenceOffset;
      int length = this.Length;
      uint num1 = 0;
      for (; offset < length; offset += rowSize)
      {
        uint num2 = this.PeekReferenceUnchecked(offset, isReferenceSmall);
        if (num2 < num1)
          return false;
        num1 = num2;
      }
      return true;
    }

    internal int[] BuildPtrTable(
      int numberOfRows,
      int rowSize,
      int referenceOffset,
      bool isReferenceSmall)
    {
      int[] array = new int[numberOfRows];
      uint[] unsortedReferences = new uint[numberOfRows];
      for (int index = 0; index < array.Length; ++index)
        array[index] = index + 1;
      this.ReadColumn(unsortedReferences, rowSize, referenceOffset, isReferenceSmall);
      Array.Sort<int>(array, (Comparison<int>) ((a, b) => unsortedReferences[a - 1].CompareTo(unsortedReferences[b - 1])));
      return array;
    }


    #nullable disable
    private void ReadColumn(
      uint[] result,
      int rowSize,
      int referenceOffset,
      bool isReferenceSmall)
    {
      int offset = referenceOffset;
      int length = this.Length;
      int index = 0;
      while (offset < length)
      {
        result[index] = this.PeekReferenceUnchecked(offset, isReferenceSmall);
        offset += rowSize;
        ++index;
      }
    }


    #nullable enable
    internal bool PeekHeapValueOffsetAndSize(int index, out int offset, out int size)
    {
      int numberOfBytesRead;
      int num = this.PeekCompressedInteger(index, out numberOfBytesRead);
      if (num == int.MaxValue)
      {
        offset = 0;
        size = 0;
        return false;
      }
      offset = index + numberOfBytesRead;
      size = num;
      return true;
    }

    internal enum FastComparisonResult
    {
      Equal,
      BytesStartWithText,
      TextStartsWithBytes,
      Unequal,
      Inconclusive,
    }
  }
}
