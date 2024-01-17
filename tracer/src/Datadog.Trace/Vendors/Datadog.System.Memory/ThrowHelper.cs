﻿// Decompiled with JetBrains decompiler
// Type: System.ThrowHelper
// Assembly: System.Memory, Version=4.0.1.2, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51
// MVID: 805945F3-27B0-47AD-B8F6-389D9D8F82C3
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Memory.4.5.5\lib\net461\System.Memory.xml

using System;
using System.Runtime.CompilerServices;
using Datadog.System.Buffers;

namespace Datadog.System
{
    internal static class ThrowHelper
  {
    internal static void ThrowArgumentNullException(ExceptionArgument argument) => throw ThrowHelper.CreateArgumentNullException(argument);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateArgumentNullException(ExceptionArgument argument) => (Exception) new ArgumentNullException(argument.ToString());

    internal static void ThrowArrayTypeMismatchException() => throw ThrowHelper.CreateArrayTypeMismatchException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateArrayTypeMismatchException() => (Exception) new ArrayTypeMismatchException();

    internal static void ThrowArgumentException_InvalidTypeWithPointersNotSupported(Type type) => throw ThrowHelper.CreateArgumentException_InvalidTypeWithPointersNotSupported(type);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateArgumentException_InvalidTypeWithPointersNotSupported(Type type) => (Exception) new ArgumentException(SR.Format(SR.Argument_InvalidTypeWithPointersNotSupported, (object) type));

    internal static void ThrowArgumentException_DestinationTooShort() => throw ThrowHelper.CreateArgumentException_DestinationTooShort();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateArgumentException_DestinationTooShort() => (Exception) new ArgumentException(SR.Argument_DestinationTooShort);

    internal static void ThrowIndexOutOfRangeException() => throw ThrowHelper.CreateIndexOutOfRangeException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateIndexOutOfRangeException() => (Exception) new IndexOutOfRangeException();

    internal static void ThrowArgumentOutOfRangeException() => throw ThrowHelper.CreateArgumentOutOfRangeException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateArgumentOutOfRangeException() => (Exception) new ArgumentOutOfRangeException();

    internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument) => throw ThrowHelper.CreateArgumentOutOfRangeException(argument);

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateArgumentOutOfRangeException(ExceptionArgument argument) => (Exception) new ArgumentOutOfRangeException(argument.ToString());

    internal static void ThrowArgumentOutOfRangeException_PrecisionTooLarge() => throw ThrowHelper.CreateArgumentOutOfRangeException_PrecisionTooLarge();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateArgumentOutOfRangeException_PrecisionTooLarge() => (Exception) new ArgumentOutOfRangeException("precision", SR.Format(SR.Argument_PrecisionTooLarge, (object) (byte) 99));

    internal static void ThrowArgumentOutOfRangeException_SymbolDoesNotFit() => throw ThrowHelper.CreateArgumentOutOfRangeException_SymbolDoesNotFit();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateArgumentOutOfRangeException_SymbolDoesNotFit() => (Exception) new ArgumentOutOfRangeException("symbol", SR.Argument_BadFormatSpecifier);

    internal static void ThrowInvalidOperationException() => throw ThrowHelper.CreateInvalidOperationException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateInvalidOperationException() => (Exception) new InvalidOperationException();

    internal static void ThrowInvalidOperationException_OutstandingReferences() => throw ThrowHelper.CreateInvalidOperationException_OutstandingReferences();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateInvalidOperationException_OutstandingReferences() => (Exception) new InvalidOperationException(SR.OutstandingReferences);

    internal static void ThrowInvalidOperationException_UnexpectedSegmentType() => throw ThrowHelper.CreateInvalidOperationException_UnexpectedSegmentType();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateInvalidOperationException_UnexpectedSegmentType() => (Exception) new InvalidOperationException(SR.UnexpectedSegmentType);

    internal static void ThrowInvalidOperationException_EndPositionNotReached() => throw ThrowHelper.CreateInvalidOperationException_EndPositionNotReached();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateInvalidOperationException_EndPositionNotReached() => (Exception) new InvalidOperationException(SR.EndPositionNotReached);

    internal static void ThrowArgumentOutOfRangeException_PositionOutOfRange() => throw ThrowHelper.CreateArgumentOutOfRangeException_PositionOutOfRange();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateArgumentOutOfRangeException_PositionOutOfRange() => (Exception) new ArgumentOutOfRangeException("position");

    internal static void ThrowArgumentOutOfRangeException_OffsetOutOfRange() => throw ThrowHelper.CreateArgumentOutOfRangeException_OffsetOutOfRange();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateArgumentOutOfRangeException_OffsetOutOfRange() => (Exception) new ArgumentOutOfRangeException("offset");

    internal static void ThrowObjectDisposedException_ArrayMemoryPoolBuffer() => throw ThrowHelper.CreateObjectDisposedException_ArrayMemoryPoolBuffer();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateObjectDisposedException_ArrayMemoryPoolBuffer() => (Exception) new ObjectDisposedException("ArrayMemoryPoolBuffer");

    internal static void ThrowFormatException_BadFormatSpecifier() => throw ThrowHelper.CreateFormatException_BadFormatSpecifier();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateFormatException_BadFormatSpecifier() => (Exception) new FormatException(SR.Argument_BadFormatSpecifier);

    internal static void ThrowArgumentException_OverlapAlignmentMismatch() => throw ThrowHelper.CreateArgumentException_OverlapAlignmentMismatch();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateArgumentException_OverlapAlignmentMismatch() => (Exception) new ArgumentException(SR.Argument_OverlapAlignmentMismatch);

    internal static void ThrowNotSupportedException() => throw ThrowHelper.CreateThrowNotSupportedException();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Exception CreateThrowNotSupportedException() => (Exception) new NotSupportedException();

    public static bool TryFormatThrowFormatException(out int bytesWritten)
    {
      bytesWritten = 0;
      ThrowHelper.ThrowFormatException_BadFormatSpecifier();
      return false;
    }

    public static bool TryParseThrowFormatException<T>(out T value, out int bytesConsumed)
    {
      value = default (T);
      bytesConsumed = 0;
      ThrowHelper.ThrowFormatException_BadFormatSpecifier();
      return false;
    }

    public static void ThrowArgumentValidationException<T>(
      ReadOnlySequenceSegment<T> startSegment,
      int startIndex,
      ReadOnlySequenceSegment<T> endSegment)
    {
      throw ThrowHelper.CreateArgumentValidationException<T>(startSegment, startIndex, endSegment);
    }

    private static Exception CreateArgumentValidationException<T>(
      ReadOnlySequenceSegment<T> startSegment,
      int startIndex,
      ReadOnlySequenceSegment<T> endSegment)
    {
      if (startSegment == null)
        return ThrowHelper.CreateArgumentNullException(ExceptionArgument.startSegment);
      if (endSegment == null)
        return ThrowHelper.CreateArgumentNullException(ExceptionArgument.endSegment);
      if (startSegment != endSegment && startSegment.RunningIndex > endSegment.RunningIndex)
        return ThrowHelper.CreateArgumentOutOfRangeException(ExceptionArgument.endSegment);
      return (uint) startSegment.Memory.Length < (uint) startIndex ? ThrowHelper.CreateArgumentOutOfRangeException(ExceptionArgument.startIndex) : ThrowHelper.CreateArgumentOutOfRangeException(ExceptionArgument.endIndex);
    }

    public static void ThrowArgumentValidationException(Array array, int start) => throw ThrowHelper.CreateArgumentValidationException(array, start);

    private static Exception CreateArgumentValidationException(Array array, int start)
    {
      if (array == null)
        return ThrowHelper.CreateArgumentNullException(ExceptionArgument.array);
      return (uint) start > (uint) array.Length ? ThrowHelper.CreateArgumentOutOfRangeException(ExceptionArgument.start) : ThrowHelper.CreateArgumentOutOfRangeException(ExceptionArgument.length);
    }

    public static void ThrowStartOrEndArgumentValidationException(long start) => throw ThrowHelper.CreateStartOrEndArgumentValidationException(start);

    private static Exception CreateStartOrEndArgumentValidationException(long start) => start < 0L ? ThrowHelper.CreateArgumentOutOfRangeException(ExceptionArgument.start) : ThrowHelper.CreateArgumentOutOfRangeException(ExceptionArgument.length);
  }
}
