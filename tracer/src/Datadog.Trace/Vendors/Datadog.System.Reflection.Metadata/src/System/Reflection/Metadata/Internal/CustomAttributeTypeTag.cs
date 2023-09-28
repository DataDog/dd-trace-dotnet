// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.CustomAttributeTypeTag
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Runtime.CompilerServices;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
    internal static class CustomAttributeTypeTag
  {
    internal const int NumberOfBits = 3;
    internal const int LargeRowSize = 8192;
    internal const uint MethodDef = 2;
    internal const uint MemberRef = 3;
    internal const uint TagMask = 7;
    internal const ulong TagToTokenTypeByteVector = 168165376;
    internal const TableMask TablesReferenced = TableMask.MethodDef | TableMask.MemberRef;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static EntityHandle ConvertToHandle(uint customAttributeType)
    {
      uint num1 = (uint) (168165376UL >> (((int) customAttributeType & 7) << 3)) << 24;
      uint num2 = customAttributeType >> 3;
      if (num1 == 0U || ((int) num2 & -16777216) != 0)
        Throw.InvalidCodedIndex();
      return new EntityHandle(num1 | num2);
    }
  }
}
