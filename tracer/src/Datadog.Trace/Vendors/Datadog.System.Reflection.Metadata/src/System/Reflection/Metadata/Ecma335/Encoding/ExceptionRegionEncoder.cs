﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ExceptionRegionEncoder
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
  public readonly struct ExceptionRegionEncoder
  {
    private const int TableHeaderSize = 4;
    private const int SmallRegionSize = 12;
    private const int FatRegionSize = 24;
    private const int ThreeBytesMaxValue = 16777215;
    internal const int MaxSmallExceptionRegions = 20;
    internal const int MaxExceptionRegions = 699050;

    /// <summary>The underlying builder.</summary>
    public BlobBuilder Builder { get; }

    /// <summary>True if the encoder uses small format.</summary>
    public bool HasSmallFormat { get; }

    internal ExceptionRegionEncoder(BlobBuilder builder, bool hasSmallFormat)
    {
      this.Builder = builder;
      this.HasSmallFormat = hasSmallFormat;
    }

    /// <summary>
    /// Returns true if the number of exception regions first small format.
    /// </summary>
    /// <param name="exceptionRegionCount">Number of exception regions.</param>
    public static bool IsSmallRegionCount(int exceptionRegionCount) => (uint) exceptionRegionCount <= 20U;

    /// <summary>Returns true if the region fits small format.</summary>
    /// <param name="startOffset">Start offset of the region.</param>
    /// <param name="length">Length of the region.</param>
    public static bool IsSmallExceptionRegion(int startOffset, int length) => (uint) startOffset <= (uint) ushort.MaxValue && (uint) length <= (uint) byte.MaxValue;

    internal static bool IsSmallExceptionRegionFromBounds(int startOffset, int endOffset) => ExceptionRegionEncoder.IsSmallExceptionRegion(startOffset, endOffset - startOffset);

    internal static int GetExceptionTableSize(int exceptionRegionCount, bool isSmallFormat) => 4 + exceptionRegionCount * (isSmallFormat ? 12 : 24);

    internal static bool IsExceptionRegionCountInBounds(int exceptionRegionCount) => (uint) exceptionRegionCount <= 699050U;

    internal static bool IsValidCatchTypeHandle(EntityHandle catchType)
    {
      if (catchType.IsNil)
        return false;
      return catchType.Kind == HandleKind.TypeDefinition || catchType.Kind == HandleKind.TypeSpecification || catchType.Kind == HandleKind.TypeReference;
    }

    internal static ExceptionRegionEncoder SerializeTableHeader(
      BlobBuilder builder,
      int exceptionRegionCount,
      bool hasSmallRegions)
    {
      bool flag = hasSmallRegions && ExceptionRegionEncoder.IsSmallRegionCount(exceptionRegionCount);
      int exceptionTableSize = ExceptionRegionEncoder.GetExceptionTableSize(exceptionRegionCount, flag);
      builder.Align(4);
      if (flag)
      {
        builder.WriteByte((byte) 1);
        builder.WriteByte((byte) exceptionTableSize);
        builder.WriteInt16((short) 0);
      }
      else
      {
        builder.WriteByte((byte) 65);
        builder.WriteByte((byte) exceptionTableSize);
        builder.WriteUInt16((ushort) (exceptionTableSize >> 8));
      }
      return new ExceptionRegionEncoder(builder, flag);
    }

    /// <summary>Adds a finally clause.</summary>
    /// <param name="tryOffset">Try block start offset.</param>
    /// <param name="tryLength">Try block length.</param>
    /// <param name="handlerOffset">Handler start offset.</param>
    /// <param name="handlerLength">Handler length.</param>
    /// <returns>Encoder for the next clause.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// <paramref name="tryOffset" />, <paramref name="tryLength" />, <paramref name="handlerOffset" /> or <paramref name="handlerLength" /> is out of range.
    /// </exception>
    /// <exception cref="T:System.InvalidOperationException">Method body was not declared to have exception regions.</exception>
    public ExceptionRegionEncoder AddFinally(
      int tryOffset,
      int tryLength,
      int handlerOffset,
      int handlerLength)
    {
      return this.Add(ExceptionRegionKind.Finally, tryOffset, tryLength, handlerOffset, handlerLength);
    }

    /// <summary>Adds a fault clause.</summary>
    /// <param name="tryOffset">Try block start offset.</param>
    /// <param name="tryLength">Try block length.</param>
    /// <param name="handlerOffset">Handler start offset.</param>
    /// <param name="handlerLength">Handler length.</param>
    /// <returns>Encoder for the next clause.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// <paramref name="tryOffset" />, <paramref name="tryLength" />, <paramref name="handlerOffset" /> or <paramref name="handlerLength" /> is out of range.
    /// </exception>
    /// <exception cref="T:System.InvalidOperationException">Method body was not declared to have exception regions.</exception>
    public ExceptionRegionEncoder AddFault(
      int tryOffset,
      int tryLength,
      int handlerOffset,
      int handlerLength)
    {
      return this.Add(ExceptionRegionKind.Fault, tryOffset, tryLength, handlerOffset, handlerLength);
    }

    /// <summary>Adds a fault clause.</summary>
    /// <param name="tryOffset">Try block start offset.</param>
    /// <param name="tryLength">Try block length.</param>
    /// <param name="handlerOffset">Handler start offset.</param>
    /// <param name="handlerLength">Handler length.</param>
    /// <param name="catchType">
    /// <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /> or <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />.
    /// </param>
    /// <returns>Encoder for the next clause.</returns>
    /// <exception cref="T:System.ArgumentException"><paramref name="catchType" /> is invalid.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// <paramref name="tryOffset" />, <paramref name="tryLength" />, <paramref name="handlerOffset" /> or <paramref name="handlerLength" /> is out of range.
    /// </exception>
    /// <exception cref="T:System.InvalidOperationException">Method body was not declared to have exception regions.</exception>
    public ExceptionRegionEncoder AddCatch(
      int tryOffset,
      int tryLength,
      int handlerOffset,
      int handlerLength,
      EntityHandle catchType)
    {
      return this.Add(ExceptionRegionKind.Catch, tryOffset, tryLength, handlerOffset, handlerLength, catchType);
    }

    /// <summary>Adds a fault clause.</summary>
    /// <param name="tryOffset">Try block start offset.</param>
    /// <param name="tryLength">Try block length.</param>
    /// <param name="handlerOffset">Handler start offset.</param>
    /// <param name="handlerLength">Handler length.</param>
    /// <param name="filterOffset">Offset of the filter block.</param>
    /// <returns>Encoder for the next clause.</returns>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// <paramref name="tryOffset" />, <paramref name="tryLength" />, <paramref name="handlerOffset" /> or <paramref name="handlerLength" /> is out of range.
    /// </exception>
    /// <exception cref="T:System.InvalidOperationException">Method body was not declared to have exception regions.</exception>
    public ExceptionRegionEncoder AddFilter(
      int tryOffset,
      int tryLength,
      int handlerOffset,
      int handlerLength,
      int filterOffset)
    {
      return this.Add(ExceptionRegionKind.Filter, tryOffset, tryLength, handlerOffset, handlerLength, filterOffset: filterOffset);
    }

    /// <summary>Adds an exception clause.</summary>
    /// <param name="kind">Clause kind.</param>
    /// <param name="tryOffset">Try block start offset.</param>
    /// <param name="tryLength">Try block length.</param>
    /// <param name="handlerOffset">Handler start offset.</param>
    /// <param name="handlerLength">Handler length.</param>
    /// <param name="catchType">
    /// <see cref="T:System.Reflection.Metadata.TypeDefinitionHandle" />, <see cref="T:System.Reflection.Metadata.TypeReferenceHandle" /> or <see cref="T:System.Reflection.Metadata.TypeSpecificationHandle" />,
    /// or nil if <paramref name="kind" /> is not <see cref="F:System.Reflection.Metadata.ExceptionRegionKind.Catch" />
    /// </param>
    /// <param name="filterOffset">
    /// Offset of the filter block, or 0 if the <paramref name="kind" /> is not <see cref="F:System.Reflection.Metadata.ExceptionRegionKind.Filter" />.
    /// </param>
    /// <returns>Encoder for the next clause.</returns>
    /// <exception cref="T:System.ArgumentException"><paramref name="catchType" /> is invalid.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException"><paramref name="kind" /> has invalid value.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">
    /// <paramref name="tryOffset" />, <paramref name="tryLength" />, <paramref name="handlerOffset" /> or <paramref name="handlerLength" /> is out of range.
    /// </exception>
    /// <exception cref="T:System.InvalidOperationException">Method body was not declared to have exception regions.</exception>
    public ExceptionRegionEncoder Add(
      ExceptionRegionKind kind,
      int tryOffset,
      int tryLength,
      int handlerOffset,
      int handlerLength,
      EntityHandle catchType = default (EntityHandle),
      int filterOffset = 0)
    {
      if (this.Builder == null)
        Throw.InvalidOperation(SR.MethodHasNoExceptionRegions);
      if (this.HasSmallFormat)
      {
        if ((int) (ushort) tryOffset != tryOffset)
          Throw.ArgumentOutOfRange(nameof (tryOffset));
        if ((int) (byte) tryLength != tryLength)
          Throw.ArgumentOutOfRange(nameof (tryLength));
        if ((int) (ushort) handlerOffset != handlerOffset)
          Throw.ArgumentOutOfRange(nameof (handlerOffset));
        if ((int) (byte) handlerLength != handlerLength)
          Throw.ArgumentOutOfRange(nameof (handlerLength));
      }
      else
      {
        if (tryOffset < 0)
          Throw.ArgumentOutOfRange(nameof (tryOffset));
        if (tryLength < 0)
          Throw.ArgumentOutOfRange(nameof (tryLength));
        if (handlerOffset < 0)
          Throw.ArgumentOutOfRange(nameof (handlerOffset));
        if (handlerLength < 0)
          Throw.ArgumentOutOfRange(nameof (handlerLength));
      }
      int catchTokenOrOffset;
      switch (kind)
      {
        case ExceptionRegionKind.Catch:
          if (!ExceptionRegionEncoder.IsValidCatchTypeHandle(catchType))
            Throw.InvalidArgument_Handle(nameof (catchType));
          catchTokenOrOffset = MetadataTokens.GetToken(catchType);
          break;
        case ExceptionRegionKind.Filter:
          if (filterOffset < 0)
            Throw.ArgumentOutOfRange(nameof (filterOffset));
          catchTokenOrOffset = filterOffset;
          break;
        case ExceptionRegionKind.Finally:
        case ExceptionRegionKind.Fault:
          catchTokenOrOffset = 0;
          break;
        default:
          throw new ArgumentOutOfRangeException(nameof (kind));
      }
      this.AddUnchecked(kind, tryOffset, tryLength, handlerOffset, handlerLength, catchTokenOrOffset);
      return this;
    }

    internal void AddUnchecked(
      ExceptionRegionKind kind,
      int tryOffset,
      int tryLength,
      int handlerOffset,
      int handlerLength,
      int catchTokenOrOffset)
    {
      if (this.HasSmallFormat)
      {
        this.Builder.WriteUInt16((ushort) kind);
        this.Builder.WriteUInt16((ushort) tryOffset);
        this.Builder.WriteByte((byte) tryLength);
        this.Builder.WriteUInt16((ushort) handlerOffset);
        this.Builder.WriteByte((byte) handlerLength);
      }
      else
      {
        this.Builder.WriteInt32((int) kind);
        this.Builder.WriteInt32(tryOffset);
        this.Builder.WriteInt32(tryLength);
        this.Builder.WriteInt32(handlerOffset);
        this.Builder.WriteInt32(handlerLength);
      }
      this.Builder.WriteInt32(catchTokenOrOffset);
    }
  }
}
