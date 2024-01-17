﻿// Decompiled with JetBrains decompiler
// Type: System.Reflection.Metadata.Ecma335.TokenTypeIds
// Assembly: System.Reflection.Metadata, Version=7.0.0.2, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a
// MVID: 2EB35F4B-CF50-496F-AFB8-CC6F6F79CB72
// Assembly location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.dll
// XML documentation location: C:\Users\dudi.keleti\source\repos\ConsoleApp4\packages\System.Reflection.Metadata.7.0.2\lib\net462\System.Reflection.Metadata.xml

namespace Datadog.System.Reflection.Metadata.Ecma335
{
    internal static class TokenTypeIds
    {
        internal const uint VirtualTokenMask = 1U << 31;
        internal const uint String = 0x70000000;
        internal const uint Module = 0;
        internal const uint TypeRef = 16777216;
        internal const uint TypeDef = 33554432;
        internal const uint FieldDef = 67108864;
        internal const uint MethodDef = 100663296;
        internal const uint ParamDef = 134217728;
        internal const uint InterfaceImpl = 150994944;
        internal const uint MemberRef = 167772160;
        internal const uint Constant = 184549376;
        internal const uint CustomAttribute = 201326592;
        internal const uint DeclSecurity = 234881024;
        internal const uint Signature = 285212672;
        internal const uint EventMap = 301989888;
        internal const uint Event = 335544320;
        internal const uint PropertyMap = 352321536;
        internal const uint Property = 385875968;
        internal const uint MethodSemantics = 402653184;
        internal const uint MethodImpl = 419430400;
        internal const uint ModuleRef = 436207616;
        internal const uint TypeSpec = 452984832;
        internal const uint Assembly = 536870912;
        internal const uint AssemblyRef = 587202560;
        internal const uint File = 637534208;
        internal const uint ExportedType = 654311424;
        internal const uint ManifestResource = 671088640;
        internal const uint NestedClass = 687865856;
        internal const uint GenericParam = 704643072;
        internal const uint MethodSpec = 721420288;
        internal const uint GenericParamConstraint = 738197504;
        internal const uint Document = 805306368;
        internal const uint MethodDebugInformation = 822083584;
        internal const uint LocalScope = 838860800;
        internal const uint LocalVariable = 855638016;
        internal const uint LocalConstant = 872415232;
        internal const uint ImportScope = 889192448;
        internal const uint AsyncMethod = 905969664;
        internal const uint CustomDebugInformation = 922746880;
        internal const uint UserString = 1879048192;
        internal const int RowIdBitCount = 24;
        internal const uint RIDMask = 16777215;
        internal const uint TypeMask = 2130706432;
        /// <summary>
        /// Use the highest bit to mark tokens that are virtual (synthesized).
        /// We create virtual tokens to represent projected WinMD entities.
        /// </summary>
        internal const uint VirtualBit = 2147483648;

        /// <summary>
        /// Returns true if the token value can escape the metadata reader.
        /// We don't allow virtual tokens and heap tokens other than UserString to escape
        /// since the token type ids are internal to the reader and not specified by ECMA spec.
        /// 
        /// Spec (Partition III, 1.9 Metadata tokens):
        /// Many CIL instructions are followed by a "metadata token". This is a 4-byte value, that specifies a row in a
        /// metadata table, or a starting byte offset in the User String heap.
        /// 
        /// For example, a value of 0x02 specifies the TypeDef table; a value of 0x70 specifies the User
        /// String heap.The value corresponds to the number assigned to that metadata table (see Partition II for the full
        /// list of tables) or to 0x70 for the User String heap.The least-significant 3 bytes specify the target row within that
        /// metadata table, or starting byte offset within the User String heap.
        /// </summary>
        internal static bool IsEntityOrUserStringToken(uint vToken) => (vToken & 2130706432U) <= 1879048192U;

        internal static bool IsEntityToken(uint vToken) => (vToken & 2130706432U) < 1879048192U;

        internal static bool IsValidRowId(uint rowId) => ((int)rowId & -16777216) == 0;

        internal static bool IsValidRowId(int rowId) => ((long)rowId & 4278190080L) == 0L;
    }
}
