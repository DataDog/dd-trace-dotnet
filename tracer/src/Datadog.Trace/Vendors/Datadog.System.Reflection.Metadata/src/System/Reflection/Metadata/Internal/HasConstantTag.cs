// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.HasConstantTag
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Runtime.CompilerServices;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
    internal static class HasConstantTag
  {
    internal const int NumberOfBits = 2;
    internal const int LargeRowSize = 16384;
    internal const uint Field = 0;
    internal const uint Param = 1;
    internal const uint Property = 2;
    internal const uint TagMask = 3;
    internal const TableMask TablesReferenced = TableMask.Field | TableMask.Param | TableMask.Property;
    internal const uint TagToTokenTypeByteVector = 1509380;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static EntityHandle ConvertToHandle(uint hasConstant)
    {
      uint num1 = 1509380U >> (((int) hasConstant & 3) << 3) << 24;
      uint num2 = hasConstant >> 2;
      if (num1 == 0U || ((int) num2 & -16777216) != 0)
        Throw.InvalidCodedIndex();
      return new EntityHandle(num1 | num2);
    }

    internal static uint ConvertToTag(EntityHandle token)
    {
      HandleKind kind = token.Kind;
      uint rowId = (uint) token.RowId;
      switch (kind)
      {
        case HandleKind.FieldDefinition:
          return (uint) ((int) rowId << 2 | 0);
        case HandleKind.Parameter:
          return (uint) ((int) rowId << 2 | 1);
        case HandleKind.PropertyDefinition:
          return (uint) ((int) rowId << 2 | 2);
        default:
          return 0;
      }
    }
  }
}
