﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.MethodBodyBlock
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Collections.Immutable;
using Datadog.System.Reflection.Internal;


#nullable enable
namespace Datadog.System.Reflection.Metadata
{
  public sealed class MethodBodyBlock
  {
    private readonly MemoryBlock _il;
    private readonly int _size;
    private readonly ushort _maxStack;
    private readonly bool _localVariablesInitialized;
    private readonly StandaloneSignatureHandle _localSignature;

    #nullable disable
    private readonly ImmutableArray<ExceptionRegion> _exceptionRegions;
    private const byte ILTinyFormat = 2;
    private const byte ILFatFormat = 3;
    private const byte ILFormatMask = 3;
    private const int ILTinyFormatSizeShift = 2;
    private const byte ILMoreSects = 8;
    private const byte ILInitLocals = 16;
    private const byte ILFatFormatHeaderSize = 3;
    private const int ILFatFormatHeaderSizeShift = 4;
    private const byte SectEHTable = 1;
    private const byte SectFatFormat = 64;

    private MethodBodyBlock(
      bool localVariablesInitialized,
      ushort maxStack,
      StandaloneSignatureHandle localSignatureHandle,
      MemoryBlock il,
      ImmutableArray<ExceptionRegion> exceptionRegions,
      int size)
    {
      this._localVariablesInitialized = localVariablesInitialized;
      this._maxStack = maxStack;
      this._localSignature = localSignatureHandle;
      this._il = il;
      this._exceptionRegions = exceptionRegions;
      this._size = size;
    }

    /// <summary>
    /// Size of the method body - includes the header, IL and exception regions.
    /// </summary>
    public int Size => this._size;

    public int MaxStack => (int) this._maxStack;

    public bool LocalVariablesInitialized => this._localVariablesInitialized;

    public StandaloneSignatureHandle LocalSignature => this._localSignature;


    #nullable enable
    public ImmutableArray<ExceptionRegion> ExceptionRegions => this._exceptionRegions;

    public byte[]? GetILBytes() => this._il.ToArray();

    public ImmutableArray<byte> GetILContent()
    {
      byte[] ilBytes = this.GetILBytes();
      return ImmutableByteArrayInterop.DangerousCreateFromUnderlyingArray(ref ilBytes);
    }

    public BlobReader GetILReader() => new BlobReader(this._il);

    public static MethodBodyBlock Create(BlobReader reader)
    {
      int offset = reader.Offset;
      byte p1_1 = reader.ReadByte();
      if (((int) p1_1 & 3) == 2)
      {
        int length = (int) p1_1 >> 2;
        return new MethodBodyBlock(false, (ushort) 8, new StandaloneSignatureHandle(), reader.GetMemoryBlockAt(0, length), ImmutableArray<ExceptionRegion>.Empty, 1 + length);
      }
      if (((int) p1_1 & 3) != 3)
        throw new BadImageFormatException(SR.Format(SR.InvalidMethodHeader1, (object) p1_1));
      byte p2 = reader.ReadByte();
      if ((int) p2 >> 4 != 3)
        throw new BadImageFormatException(SR.Format(SR.InvalidMethodHeader2, (object) p1_1, (object) p2));
      bool localVariablesInitialized = ((int) p1_1 & 16) == 16;
      bool flag1 = ((int) p1_1 & 8) == 8;
      ushort maxStack = reader.ReadUInt16();
      int length1 = reader.ReadInt32();
      int p1_2 = reader.ReadInt32();
      StandaloneSignatureHandle localSignatureHandle;
      if (p1_2 == 0)
        localSignatureHandle = new StandaloneSignatureHandle();
      else
        localSignatureHandle = ((long) p1_2 & 2130706432L) == 285212672L ? StandaloneSignatureHandle.FromRowId(p1_2 & 16777215) : throw new BadImageFormatException(SR.Format(SR.InvalidLocalSignatureToken, (object) (uint) p1_2));
      MemoryBlock memoryBlockAt = reader.GetMemoryBlockAt(0, length1);
      reader.Offset += length1;
      ImmutableArray<ExceptionRegion> exceptionRegions;
      if (flag1)
      {
        reader.Align((byte) 4);
        byte p1_3 = reader.ReadByte();
        if (((int) p1_3 & 1) != 1)
          throw new BadImageFormatException(SR.Format(SR.InvalidSehHeader, (object) p1_3));
        bool flag2 = ((int) p1_3 & 64) == 64;
        int num1 = (int) reader.ReadByte();
        if (flag2)
        {
          int num2 = num1 + ((int) reader.ReadUInt16() << 8);
          exceptionRegions = MethodBodyBlock.ReadFatExceptionHandlers(ref reader, num2 / 24);
        }
        else
        {
          reader.Offset += 2;
          exceptionRegions = MethodBodyBlock.ReadSmallExceptionHandlers(ref reader, num1 / 12);
        }
      }
      else
        exceptionRegions = ImmutableArray<ExceptionRegion>.Empty;
      return new MethodBodyBlock(localVariablesInitialized, maxStack, localSignatureHandle, memoryBlockAt, exceptionRegions, reader.Offset - offset);
    }


    #nullable disable
    private static ImmutableArray<ExceptionRegion> ReadSmallExceptionHandlers(
      ref BlobReader memReader,
      int count)
    {
      ExceptionRegion[] exceptionRegionArray = new ExceptionRegion[count];
      for (int index = 0; index < exceptionRegionArray.Length; ++index)
      {
        ExceptionRegionKind kind = (ExceptionRegionKind) memReader.ReadUInt16();
        ushort tryOffset = memReader.ReadUInt16();
        byte tryLength = memReader.ReadByte();
        ushort handlerOffset = memReader.ReadUInt16();
        byte handlerLength = memReader.ReadByte();
        int classTokenOrFilterOffset = memReader.ReadInt32();
        exceptionRegionArray[index] = new ExceptionRegion(kind, (int) tryOffset, (int) tryLength, (int) handlerOffset, (int) handlerLength, classTokenOrFilterOffset);
      }
      return ImmutableArray.Create<ExceptionRegion>(exceptionRegionArray);
    }

    private static ImmutableArray<ExceptionRegion> ReadFatExceptionHandlers(
      ref BlobReader memReader,
      int count)
    {
      ExceptionRegion[] exceptionRegionArray = new ExceptionRegion[count];
      for (int index = 0; index < exceptionRegionArray.Length; ++index)
      {
        ExceptionRegionKind kind = (ExceptionRegionKind) memReader.ReadUInt32();
        int tryOffset = memReader.ReadInt32();
        int tryLength = memReader.ReadInt32();
        int handlerOffset = memReader.ReadInt32();
        int handlerLength = memReader.ReadInt32();
        int classTokenOrFilterOffset = memReader.ReadInt32();
        exceptionRegionArray[index] = new ExceptionRegion(kind, tryOffset, tryLength, handlerOffset, handlerLength, classTokenOrFilterOffset);
      }
      return ImmutableArray.Create<ExceptionRegion>(exceptionRegionArray);
    }
  }
}
