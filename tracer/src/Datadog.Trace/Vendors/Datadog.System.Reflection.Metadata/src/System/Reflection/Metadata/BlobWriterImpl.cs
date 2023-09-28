// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.BlobWriterImpl
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml


#nullable enable
using System;
using System.Reflection;

namespace Datadog.System.Reflection.Metadata
{
  internal static class BlobWriterImpl
  {
    internal const int SingleByteCompressedIntegerMaxValue = 127;
    internal const int TwoByteCompressedIntegerMaxValue = 16383;
    internal const int MaxCompressedIntegerValue = 536870911;
    internal const int MinSignedCompressedIntegerValue = -268435456;
    internal const int MaxSignedCompressedIntegerValue = 268435455;

    internal static int GetCompressedIntegerSize(int value)
    {
      if (value <= (int) sbyte.MaxValue)
        return 1;
      return value <= 16383 ? 2 : 4;
    }

    internal static void WriteCompressedInteger(ref BlobWriter writer, uint value)
    {
      if (value <= (uint) sbyte.MaxValue)
        writer.WriteByte((byte) value);
      else if (value <= 16383U)
        writer.WriteUInt16BE((ushort) (32768U | value));
      else if (value <= 536870911U)
        writer.WriteUInt32BE(3221225472U | value);
      else
        Throw.ValueArgumentOutOfRange();
    }

    internal static void WriteCompressedInteger(BlobBuilder writer, uint value)
    {
      if (value <= (uint) sbyte.MaxValue)
        writer.WriteByte((byte) value);
      else if (value <= 16383U)
        writer.WriteUInt16BE((ushort) (32768U | value));
      else if (value <= 536870911U)
        writer.WriteUInt32BE(3221225472U | value);
      else
        Throw.ValueArgumentOutOfRange();
    }

    internal static void WriteCompressedSignedInteger(ref BlobWriter writer, int value)
    {
      int num1 = value >> 31;
      if ((value & -64) == (num1 & -64))
      {
        int num2 = (value & 63) << 1 | num1 & 1;
        writer.WriteByte((byte) num2);
      }
      else if ((value & -8192) == (num1 & -8192))
      {
        int num3 = (value & 8191) << 1 | num1 & 1;
        writer.WriteUInt16BE((ushort) (32768 | num3));
      }
      else if ((value & -268435456) == (num1 & -268435456))
      {
        int num4 = (value & 268435455) << 1 | num1 & 1;
        writer.WriteUInt32BE((uint) (-1073741824 | num4));
      }
      else
        Throw.ValueArgumentOutOfRange();
    }

    internal static void WriteCompressedSignedInteger(BlobBuilder writer, int value)
    {
      int num1 = value >> 31;
      if ((value & -64) == (num1 & -64))
      {
        int num2 = (value & 63) << 1 | num1 & 1;
        writer.WriteByte((byte) num2);
      }
      else if ((value & -8192) == (num1 & -8192))
      {
        int num3 = (value & 8191) << 1 | num1 & 1;
        writer.WriteUInt16BE((ushort) (32768 | num3));
      }
      else if ((value & -268435456) == (num1 & -268435456))
      {
        int num4 = (value & 268435455) << 1 | num1 & 1;
        writer.WriteUInt32BE((uint) (-1073741824 | num4));
      }
      else
        Throw.ValueArgumentOutOfRange();
    }

    internal static void WriteConstant(ref BlobWriter writer, object? value)
    {
      if (value == null)
      {
        writer.WriteUInt32(0U);
      }
      else
      {
        Type type = value.GetType();
        if (type.GetTypeInfo().IsEnum)
          type = Enum.GetUnderlyingType(type);
        if (type == typeof (bool))
          writer.WriteBoolean((bool) value);
        else if (type == typeof (int))
          writer.WriteInt32((int) value);
        else if (type == typeof (string))
          writer.WriteUTF16((string) value);
        else if (type == typeof (byte))
          writer.WriteByte((byte) value);
        else if (type == typeof (char))
          writer.WriteUInt16((ushort) (char) value);
        else if (type == typeof (double))
          writer.WriteDouble((double) value);
        else if (type == typeof (short))
          writer.WriteInt16((short) value);
        else if (type == typeof (long))
          writer.WriteInt64((long) value);
        else if (type == typeof (sbyte))
          writer.WriteSByte((sbyte) value);
        else if (type == typeof (float))
          writer.WriteSingle((float) value);
        else if (type == typeof (ushort))
          writer.WriteUInt16((ushort) value);
        else if (type == typeof (uint))
        {
          writer.WriteUInt32((uint) value);
        }
        else
        {
          if (!(type == typeof (ulong)))
            throw new ArgumentException(SR.Format(SR.InvalidConstantValueOfType, (object) type));
          writer.WriteUInt64((ulong) value);
        }
      }
    }

    internal static void WriteConstant(BlobBuilder writer, object? value)
    {
      if (value == null)
      {
        writer.WriteUInt32(0U);
      }
      else
      {
        Type type = value.GetType();
        if (type.GetTypeInfo().IsEnum)
          type = Enum.GetUnderlyingType(type);
        if (type == typeof (bool))
          writer.WriteBoolean((bool) value);
        else if (type == typeof (int))
          writer.WriteInt32((int) value);
        else if (type == typeof (string))
          writer.WriteUTF16((string) value);
        else if (type == typeof (byte))
          writer.WriteByte((byte) value);
        else if (type == typeof (char))
          writer.WriteUInt16((ushort) (char) value);
        else if (type == typeof (double))
          writer.WriteDouble((double) value);
        else if (type == typeof (short))
          writer.WriteInt16((short) value);
        else if (type == typeof (long))
          writer.WriteInt64((long) value);
        else if (type == typeof (sbyte))
          writer.WriteSByte((sbyte) value);
        else if (type == typeof (float))
          writer.WriteSingle((float) value);
        else if (type == typeof (ushort))
          writer.WriteUInt16((ushort) value);
        else if (type == typeof (uint))
        {
          writer.WriteUInt32((uint) value);
        }
        else
        {
          if (!(type == typeof (ulong)))
            throw new ArgumentException(SR.Format(SR.InvalidConstantValueOfType, (object) type));
          writer.WriteUInt64((ulong) value);
        }
      }
    }
  }
}
