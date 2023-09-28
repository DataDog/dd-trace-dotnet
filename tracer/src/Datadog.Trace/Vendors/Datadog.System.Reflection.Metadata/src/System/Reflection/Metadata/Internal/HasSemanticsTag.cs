// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.HasSemanticsTag
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Runtime.CompilerServices;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
    internal static class HasSemanticsTag
  {
    internal const int NumberOfBits = 1;
    internal const int LargeRowSize = 32768;
    internal const uint Event = 0;
    internal const uint Property = 1;
    internal const uint TagMask = 1;
    internal const TableMask TablesReferenced = TableMask.Event | TableMask.Property;
    internal const uint TagToTokenTypeByteVector = 5908;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static EntityHandle ConvertToHandle(uint hasSemantic)
    {
      uint num1 = 5908U >> (((int) hasSemantic & 1) << 3) << 24;
      uint num2 = hasSemantic >> 1;
      if (((int) num2 & -16777216) != 0)
        Throw.InvalidCodedIndex();
      return new EntityHandle(num1 | num2);
    }

    internal static uint ConvertEventHandleToTag(EventDefinitionHandle eventDef) => (uint) (eventDef.RowId << 1 | 0);

    internal static uint ConvertPropertyHandleToTag(PropertyDefinitionHandle propertyDef) => (uint) (propertyDef.RowId << 1 | 1);
  }
}
