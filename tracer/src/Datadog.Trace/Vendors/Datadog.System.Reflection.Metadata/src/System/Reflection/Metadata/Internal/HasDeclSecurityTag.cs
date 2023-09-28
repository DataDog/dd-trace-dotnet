// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.HasDeclSecurityTag
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Runtime.CompilerServices;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
    internal static class HasDeclSecurityTag
  {
    internal const int NumberOfBits = 2;
    internal const int LargeRowSize = 16384;
    internal const uint TypeDef = 0;
    internal const uint MethodDef = 1;
    internal const uint Assembly = 2;
    internal const uint TagMask = 3;
    internal const TableMask TablesReferenced = TableMask.TypeDef | TableMask.MethodDef | TableMask.Assembly;
    internal const uint TagToTokenTypeByteVector = 2098690;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static EntityHandle ConvertToHandle(uint hasDeclSecurity)
    {
      uint num1 = 2098690U >> (((int) hasDeclSecurity & 3) << 3) << 24;
      uint num2 = hasDeclSecurity >> 2;
      if (num1 == 0U || ((int) num2 & -16777216) != 0)
        Throw.InvalidCodedIndex();
      return new EntityHandle(num1 | num2);
    }

    internal static uint ConvertToTag(EntityHandle handle)
    {
      uint type = handle.Type;
      uint rowId = (uint) handle.RowId;
      uint tag;
      switch (type >> 24)
      {
        case 2:
          tag = (uint) ((int) rowId << 2 | 0);
          break;
        case 6:
          tag = (uint) ((int) rowId << 2 | 1);
          break;
        case 32:
          tag = (uint) ((int) rowId << 2 | 2);
          break;
        default:
          tag = 0U;
          break;
      }
      return tag;
    }
  }
}
