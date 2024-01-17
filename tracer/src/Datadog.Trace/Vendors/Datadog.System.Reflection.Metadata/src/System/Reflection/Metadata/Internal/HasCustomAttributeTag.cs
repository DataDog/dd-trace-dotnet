﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.HasCustomAttributeTag
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

using System.Runtime.CompilerServices;


#nullable enable
namespace Datadog.System.Reflection.Metadata.Ecma335
{
    internal static class HasCustomAttributeTag
  {
    internal const int NumberOfBits = 5;
    internal const int LargeRowSize = 2048;
    internal const uint MethodDef = 0;
    internal const uint Field = 1;
    internal const uint TypeRef = 2;
    internal const uint TypeDef = 3;
    internal const uint Param = 4;
    internal const uint InterfaceImpl = 5;
    internal const uint MemberRef = 6;
    internal const uint Module = 7;
    internal const uint DeclSecurity = 8;
    internal const uint Property = 9;
    internal const uint Event = 10;
    internal const uint StandAloneSig = 11;
    internal const uint ModuleRef = 12;
    internal const uint TypeSpec = 13;
    internal const uint Assembly = 14;
    internal const uint AssemblyRef = 15;
    internal const uint File = 16;
    internal const uint ExportedType = 17;
    internal const uint ManifestResource = 18;
    internal const uint GenericParam = 19;
    internal const uint GenericParamConstraint = 20;
    internal const uint MethodSpec = 21;
    internal const uint TagMask = 31;
    internal const uint InvalidTokenType = 4294967295;
    internal static uint[] TagToTokenTypeArray = new uint[32]
    {
      100663296U,
      67108864U,
      16777216U,
      33554432U,
      134217728U,
      150994944U,
      167772160U,
      0U,
      234881024U,
      385875968U,
      335544320U,
      285212672U,
      436207616U,
      452984832U,
      536870912U,
      587202560U,
      637534208U,
      654311424U,
      671088640U,
      704643072U,
      738197504U,
      721420288U,
      uint.MaxValue,
      uint.MaxValue,
      uint.MaxValue,
      uint.MaxValue,
      uint.MaxValue,
      uint.MaxValue,
      uint.MaxValue,
      uint.MaxValue,
      uint.MaxValue,
      uint.MaxValue
    };
    internal const TableMask TablesReferenced = TableMask.Module | TableMask.TypeRef | TableMask.TypeDef | TableMask.Field | TableMask.MethodDef | TableMask.Param | TableMask.InterfaceImpl | TableMask.MemberRef | TableMask.DeclSecurity | TableMask.StandAloneSig | TableMask.Event | TableMask.Property | TableMask.ModuleRef | TableMask.TypeSpec | TableMask.Assembly | TableMask.AssemblyRef | TableMask.File | TableMask.ExportedType | TableMask.ManifestResource | TableMask.GenericParam | TableMask.MethodSpec | TableMask.GenericParamConstraint;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static EntityHandle ConvertToHandle(uint hasCustomAttribute)
    {
      uint tagToTokenType = HasCustomAttributeTag.TagToTokenTypeArray[(int) hasCustomAttribute & 31];
      uint num = hasCustomAttribute >> 5;
      if (tagToTokenType == uint.MaxValue || ((int) num & -16777216) != 0)
        Throw.InvalidCodedIndex();
      return new EntityHandle(tagToTokenType | num);
    }

    internal static uint ConvertToTag(EntityHandle handle)
    {
      uint type = handle.Type;
      uint rowId = (uint) handle.RowId;
      uint tag;
      switch (type >> 24)
      {
        case 0:
          tag = (uint) ((int) rowId << 5 | 7);
          break;
        case 1:
          tag = (uint) ((int) rowId << 5 | 2);
          break;
        case 2:
          tag = (uint) ((int) rowId << 5 | 3);
          break;
        case 4:
          tag = (uint) ((int) rowId << 5 | 1);
          break;
        case 6:
          tag = (uint) ((int) rowId << 5 | 0);
          break;
        case 8:
          tag = (uint) ((int) rowId << 5 | 4);
          break;
        case 9:
          tag = (uint) ((int) rowId << 5 | 5);
          break;
        case 10:
          tag = (uint) ((int) rowId << 5 | 6);
          break;
        case 14:
          tag = (uint) ((int) rowId << 5 | 8);
          break;
        case 17:
          tag = (uint) ((int) rowId << 5 | 11);
          break;
        case 20:
          tag = (uint) ((int) rowId << 5 | 10);
          break;
        case 23:
          tag = (uint) ((int) rowId << 5 | 9);
          break;
        case 26:
          tag = (uint) ((int) rowId << 5 | 12);
          break;
        case 27:
          tag = (uint) ((int) rowId << 5 | 13);
          break;
        case 32:
          tag = (uint) ((int) rowId << 5 | 14);
          break;
        case 35:
          tag = (uint) ((int) rowId << 5 | 15);
          break;
        case 38:
          tag = (uint) ((int) rowId << 5 | 16);
          break;
        case 39:
          tag = (uint) ((int) rowId << 5 | 17);
          break;
        case 40:
          tag = (uint) ((int) rowId << 5 | 18);
          break;
        case 42:
          tag = (uint) ((int) rowId << 5 | 19);
          break;
        case 43:
          tag = (uint) ((int) rowId << 5 | 21);
          break;
        case 44:
          tag = (uint) ((int) rowId << 5 | 20);
          break;
        default:
          tag = 0U;
          break;
      }
      return tag;
    }
  }
}
