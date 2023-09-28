// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.ResolutionScopeTag
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Runtime.CompilerServices;

namespace Datadog.System.Reflection.Metadata.Ecma335
{
    internal static class ResolutionScopeTag
  {
    internal const int NumberOfBits = 2;
    internal const int LargeRowSize = 16384;
    internal const uint Module = 0;
    internal const uint ModuleRef = 1;
    internal const uint AssemblyRef = 2;
    internal const uint TypeRef = 3;
    internal const uint TagMask = 3;
    internal const uint TagToTokenTypeByteVector = 19077632;
    internal const TableMask TablesReferenced = TableMask.Module | TableMask.TypeRef | TableMask.ModuleRef | TableMask.AssemblyRef;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static EntityHandle ConvertToHandle(uint resolutionScope)
    {
      uint num1 = 19077632U >> (((int) resolutionScope & 3) << 3) << 24;
      uint num2 = resolutionScope >> 2;
      if (((int) num2 & -16777216) != 0)
        Throw.InvalidCodedIndex();
      return new EntityHandle(num1 | num2);
    }
  }
}
