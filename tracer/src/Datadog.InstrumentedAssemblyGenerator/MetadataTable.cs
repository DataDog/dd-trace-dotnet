namespace Datadog.InstrumentedAssemblyGenerator
{
    /// All Tables ////
    /// ---------------------------------------------------------------------------
    /// 00 - 00 - Module                  01 - 01 - TypeRef                02 - 02 - TypeDef
    /// 04 - 04 - Field                   06 - 06 - MethodDef              08 - 08 - Param
    /// 09 - 09 - InterfaceImpl           10 - 0A - MemberRef              11 - 0B - Constant
    /// 12 - 0C - CustomAttribute         13 - 0D - FieldMarshal           14 - 0E - DeclSecurity
    /// 15 - 0F - ClassLayout             16 - 10 - FieldLayout            17 - 11 - StandAloneSig
    /// 18 - 12 - EventMap                20 - 14 - Event                  21 - 15 - PropertyMap
    /// 23 - 17 - Property                24 - 18 - MethodSemantics        25 - 19 - MethodImpl
    /// 26 - 1A - ModuleRef               27 - 1B - TypeSpec               28 - 1C - ImplMap
    /// 29 - 1D - FieldRVA                32 - 20 - Assembly               33 - 21 - AssemblyProcessor
    /// 34 - 22 - AssemblyOS              35 - 23 - AssemblyRef            36 - 24 - AssemblyRefProcessor
    /// 37 - 25 - AssemblyRefOS           38 - 26 - File                   39 - 27 - ExportedType
    /// 40 - 28 - ManifestResource        41 - 29 - NestedClass            42 - 2A - GenericParam
    /// 44 - 2C - GenericParamConstraint
    /// ---------------------------------------------------------------------------


    /// <summary>
    /// Supported tables
    /// </summary>
    public enum MetadataTable : byte
    {
        /// <summary>Module table (00h)</summary>
        Module = 0x0,
        /// <summary>TypeRef table (01h)</summary>
        TypeRef = 0x1,
        /// <summary>TypeDef table (02h)</summary>
        TypeDef = 0x2,
        /// <summary>Field table (04h)</summary>
        Field = 0x4,
        /// <summary>Method table (06h)</summary>
        Method = 0x6,
        /// <summary>MemberRef table (0Ah)</summary>
        MemberRef = 0xa,
        /// <summary>StandAloneSig table (11h)</summary>
        StandAloneSig = 0x11,
        /// <summary>Property table (17h)</summary>
        Property = 0x17,
        /// <summary>ModuleRef table (1Ah)</summary>
        ModuleRef = 0x1a,
        /// <summary>TypeSpec table (1Bh)</summary>
        TypeSpec = 0x1b,
        /// <summary>Assembly table (20h)</summary>
        Assembly = 0x20,
        /// <summary>AssemblyRef table (23h)</summary>
        AssemblyRef = 0x23,
        /// <summary>NestedClass table (29h)</summary>
        NestedClass = 0x29,
        /// <summary>GenericParam table (2Ah)</summary>
        GenericParam = 0x2a,
        /// <summary>MethodSpec table (2Bh)</summary>
        MethodSpec = 0x2b,
        /// <summary>ExportedType table (27h)</summary>
        ExportedType = 0x27,
        /// <summary>UserStrings table (70h)</summary>
        UserString = 0x70,
    }
}