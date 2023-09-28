// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.MetadataWriterUtilities
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System;
using Datadog.System.Collections.Immutable;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
  internal static class MetadataWriterUtilities
  {
    public static SignatureTypeCode GetConstantTypeCode(object? value)
    {
      if (value == null)
        return SignatureTypeCode.Boolean | SignatureTypeCode.ByReference;
      if (value.GetType() == typeof (int))
        return SignatureTypeCode.Int32;
      if (value.GetType() == typeof (string))
        return SignatureTypeCode.String;
      if (value.GetType() == typeof (bool))
        return SignatureTypeCode.Boolean;
      if (value.GetType() == typeof (char))
        return SignatureTypeCode.Char;
      if (value.GetType() == typeof (byte))
        return SignatureTypeCode.Byte;
      if (value.GetType() == typeof (long))
        return SignatureTypeCode.Int64;
      if (value.GetType() == typeof (double))
        return SignatureTypeCode.Double;
      if (value.GetType() == typeof (short))
        return SignatureTypeCode.Int16;
      if (value.GetType() == typeof (ushort))
        return SignatureTypeCode.UInt16;
      if (value.GetType() == typeof (uint))
        return SignatureTypeCode.UInt32;
      if (value.GetType() == typeof (sbyte))
        return SignatureTypeCode.SByte;
      if (value.GetType() == typeof (ulong))
        return SignatureTypeCode.UInt64;
      if (value.GetType() == typeof (float))
        return SignatureTypeCode.Single;
      throw new ArgumentException();
    }

    internal static void SerializeRowCounts(BlobBuilder writer, ImmutableArray<int> rowCounts)
    {
      for (int index = 0; index < rowCounts.Length; ++index)
      {
        int rowCount = rowCounts[index];
        if (rowCount > 0)
          writer.WriteInt32(rowCount);
      }
    }
  }
}
