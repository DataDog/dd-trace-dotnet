﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.BlobHeap
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using System.Text;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
    internal struct BlobHeap
  {

    #nullable disable
    private static byte[][] s_virtualValues;
    internal readonly MemoryBlock Block;
    private VirtualHeap _lazyVirtualHeap;

    internal BlobHeap(MemoryBlock block, MetadataKind metadataKind)
    {
      this._lazyVirtualHeap = (VirtualHeap) null;
      this.Block = block;
      if (BlobHeap.s_virtualValues != null || metadataKind == MetadataKind.Ecma335)
        return;
      BlobHeap.s_virtualValues = new byte[5][]
      {
        null,
        new byte[8]
        {
          (byte) 176,
          (byte) 63,
          (byte) 95,
          (byte) 127,
          (byte) 17,
          (byte) 213,
          (byte) 10,
          (byte) 58
        },
        new byte[160]
        {
          (byte) 0,
          (byte) 36,
          (byte) 0,
          (byte) 0,
          (byte) 4,
          (byte) 128,
          (byte) 0,
          (byte) 0,
          (byte) 148,
          (byte) 0,
          (byte) 0,
          (byte) 0,
          (byte) 6,
          (byte) 2,
          (byte) 0,
          (byte) 0,
          (byte) 0,
          (byte) 36,
          (byte) 0,
          (byte) 0,
          (byte) 82,
          (byte) 83,
          (byte) 65,
          (byte) 49,
          (byte) 0,
          (byte) 4,
          (byte) 0,
          (byte) 0,
          (byte) 1,
          (byte) 0,
          (byte) 1,
          (byte) 0,
          (byte) 7,
          (byte) 209,
          (byte) 250,
          (byte) 87,
          (byte) 196,
          (byte) 174,
          (byte) 217,
          (byte) 240,
          (byte) 163,
          (byte) 46,
          (byte) 132,
          (byte) 170,
          (byte) 15,
          (byte) 174,
          (byte) 253,
          (byte) 13,
          (byte) 233,
          (byte) 232,
          (byte) 253,
          (byte) 106,
          (byte) 236,
          (byte) 143,
          (byte) 135,
          (byte) 251,
          (byte) 3,
          (byte) 118,
          (byte) 108,
          (byte) 131,
          (byte) 76,
          (byte) 153,
          (byte) 146,
          (byte) 30,
          (byte) 178,
          (byte) 59,
          (byte) 231,
          (byte) 154,
          (byte) 217,
          (byte) 213,
          (byte) 220,
          (byte) 193,
          (byte) 221,
          (byte) 154,
          (byte) 210,
          (byte) 54,
          (byte) 19,
          (byte) 33,
          (byte) 2,
          (byte) 144,
          (byte) 11,
          (byte) 114,
          (byte) 60,
          (byte) 249,
          (byte) 128,
          (byte) 149,
          (byte) 127,
          (byte) 196,
          (byte) 225,
          (byte) 119,
          (byte) 16,
          (byte) 143,
          (byte) 198,
          (byte) 7,
          (byte) 119,
          (byte) 79,
          (byte) 41,
          (byte) 232,
          (byte) 50,
          (byte) 14,
          (byte) 146,
          (byte) 234,
          (byte) 5,
          (byte) 236,
          (byte) 228,
          (byte) 232,
          (byte) 33,
          (byte) 192,
          (byte) 165,
          (byte) 239,
          (byte) 232,
          (byte) 241,
          (byte) 100,
          (byte) 92,
          (byte) 76,
          (byte) 12,
          (byte) 147,
          (byte) 193,
          (byte) 171,
          (byte) 153,
          (byte) 40,
          (byte) 93,
          (byte) 98,
          (byte) 44,
          (byte) 170,
          (byte) 101,
          (byte) 44,
          (byte) 29,
          (byte) 250,
          (byte) 214,
          (byte) 61,
          (byte) 116,
          (byte) 93,
          (byte) 111,
          (byte) 45,
          (byte) 229,
          (byte) 241,
          (byte) 126,
          (byte) 94,
          (byte) 175,
          (byte) 15,
          (byte) 196,
          (byte) 150,
          (byte) 61,
          (byte) 38,
          (byte) 28,
          (byte) 138,
          (byte) 18,
          (byte) 67,
          (byte) 101,
          (byte) 24,
          (byte) 32,
          (byte) 109,
          (byte) 192,
          (byte) 147,
          (byte) 52,
          (byte) 77,
          (byte) 90,
          (byte) 210,
          (byte) 147
        },
        new byte[25]
        {
          (byte) 1,
          (byte) 0,
          (byte) 0,
          (byte) 0,
          (byte) 0,
          (byte) 0,
          (byte) 1,
          (byte) 0,
          (byte) 84,
          (byte) 2,
          (byte) 13,
          (byte) 65,
          (byte) 108,
          (byte) 108,
          (byte) 111,
          (byte) 119,
          (byte) 77,
          (byte) 117,
          (byte) 108,
          (byte) 116,
          (byte) 105,
          (byte) 112,
          (byte) 108,
          (byte) 101,
          (byte) 0
        },
        new byte[25]
        {
          (byte) 1,
          (byte) 0,
          (byte) 0,
          (byte) 0,
          (byte) 0,
          (byte) 0,
          (byte) 1,
          (byte) 0,
          (byte) 84,
          (byte) 2,
          (byte) 13,
          (byte) 65,
          (byte) 108,
          (byte) 108,
          (byte) 111,
          (byte) 119,
          (byte) 77,
          (byte) 117,
          (byte) 108,
          (byte) 116,
          (byte) 105,
          (byte) 112,
          (byte) 108,
          (byte) 101,
          (byte) 1
        }
      };
    }


    #nullable enable
    internal byte[] GetBytes(BlobHandle handle)
    {
      if (handle.IsVirtual)
        return BlobHeap.GetVirtualBlobBytes(handle, true);
      int heapOffset = handle.GetHeapOffset();
      int numberOfBytesRead;
      int byteCount = this.Block.PeekCompressedInteger(heapOffset, out numberOfBytesRead);
      return byteCount == int.MaxValue ? Array.Empty<byte>() : this.Block.PeekBytes(heapOffset + numberOfBytesRead, byteCount);
    }

    internal MemoryBlock GetMemoryBlock(BlobHandle handle)
    {
      if (handle.IsVirtual)
        return this.GetVirtualHandleMemoryBlock(handle);
      int offset;
      int size;
      this.Block.PeekHeapValueOffsetAndSize(handle.GetHeapOffset(), out offset, out size);
      return this.Block.GetMemoryBlockAt(offset, size);
    }

    private MemoryBlock GetVirtualHandleMemoryBlock(BlobHandle handle)
    {
      VirtualHeap virtualHeap = VirtualHeap.GetOrCreateVirtualHeap(ref this._lazyVirtualHeap);
      lock (virtualHeap)
      {
        MemoryBlock block;
        if (!virtualHeap.TryGetMemoryBlock(handle.RawValue, out block))
          block = virtualHeap.AddBlob(handle.RawValue, BlobHeap.GetVirtualBlobBytes(handle, false));
        return block;
      }
    }

    internal BlobReader GetBlobReader(BlobHandle handle) => new BlobReader(this.GetMemoryBlock(handle));

    internal BlobHandle GetNextHandle(BlobHandle handle)
    {
      if (handle.IsVirtual)
        return new BlobHandle();
      int offset;
      int size;
      if (!this.Block.PeekHeapValueOffsetAndSize(handle.GetHeapOffset(), out offset, out size))
        return new BlobHandle();
      int heapOffset = offset + size;
      return heapOffset >= this.Block.Length ? new BlobHandle() : BlobHandle.FromOffset(heapOffset);
    }

    internal static byte[] GetVirtualBlobBytes(BlobHandle handle, bool unique)
    {
      BlobHandle.VirtualIndex virtualIndex = handle.GetVirtualIndex();
      byte[] blob = BlobHeap.s_virtualValues[(int) virtualIndex];
      switch (virtualIndex)
      {
        case BlobHandle.VirtualIndex.AttributeUsage_AllowSingle:
        case BlobHandle.VirtualIndex.AttributeUsage_AllowMultiple:
          blob = (byte[]) blob.Clone();
          handle.SubstituteTemplateParameters(blob);
          break;
        default:
          if (unique)
          {
            blob = (byte[]) blob.Clone();
            break;
          }
          break;
      }
      return blob;
    }

    public string GetDocumentName(DocumentNameBlobHandle handle)
    {
      BlobReader blobReader1 = this.GetBlobReader((BlobHandle) handle);
      int num = (int) blobReader1.ReadByte();
      if (num > (int) sbyte.MaxValue)
        throw new BadImageFormatException();
      PooledStringBuilder instance = PooledStringBuilder.GetInstance();
      StringBuilder builder = instance.Builder;
      bool flag = true;
      while (blobReader1.RemainingBytes > 0)
      {
        if (num != 0 && !flag)
          builder.Append((char) num);
        BlobReader blobReader2 = this.GetBlobReader(blobReader1.ReadBlobHandle());
        builder.Append(blobReader2.ReadUTF8(blobReader2.Length));
        flag = false;
      }
      return instance.ToStringAndFree();
    }

    internal bool DocumentNameEquals(DocumentNameBlobHandle handle, string other, bool ignoreCase)
    {
      BlobReader blobReader = this.GetBlobReader((BlobHandle) handle);
      int b = (int) blobReader.ReadByte();
      if (b > (int) sbyte.MaxValue)
        return false;
      int ignoreCaseMask = StringUtils.IgnoreCaseMask(ignoreCase);
      int num = 0;
      bool flag = true;
      while (blobReader.RemainingBytes > 0)
      {
        if (b != 0 && !flag)
        {
          if (num == other.Length || !StringUtils.IsEqualAscii((int) other[num], b, ignoreCaseMask))
            return false;
          ++num;
        }
        MemoryBlock memoryBlock = this.GetMemoryBlock(blobReader.ReadBlobHandle());
        int firstDifferenceIndex;
        switch (memoryBlock.Utf8NullTerminatedFastCompare(0, other, num, out firstDifferenceIndex, char.MinValue, ignoreCase))
        {
          case MemoryBlock.FastComparisonResult.Unequal:
            return false;
          case MemoryBlock.FastComparisonResult.Inconclusive:
            return this.GetDocumentName(handle).Equals(other, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
          default:
            if (firstDifferenceIndex - num == memoryBlock.Length)
            {
              num = firstDifferenceIndex;
              flag = false;
              continue;
            }
            goto case MemoryBlock.FastComparisonResult.Unequal;
        }
      }
      return num == other.Length;
    }
  }
}
