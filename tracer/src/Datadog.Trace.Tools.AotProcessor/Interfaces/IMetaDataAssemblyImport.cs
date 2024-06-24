namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface IMetaDataAssemblyImport : IUnknown
{
    public static readonly new Guid Guid = new("EE62470B-E94B-424e-9B7C-2F00C9249F93");

    HResult GetAssemblyProps(
        MdAssembly mda,                 // [IN] The Assembly for which to get the properties.
        IntPtr* ppbPublicKey,           // [OUT] Pointer to the public key.  const void** ppbPublicKey
        int* pcbPublicKey,              // [OUT] Count of bytes in the public key.
        int* pulHashAlgId,              // [OUT] Hash Algorithm.
        char* szName,                   // [OUT] Buffer to fill with assembly's simply name.
        uint cchName,                   // [IN] Size of buffer in wide chars.
        uint* pchName,               // [OUT] Actual # of wide chars in name.
        ASSEMBLYMETADATA* pMetaData,    // [OUT] Assembly MetaData.
        int* pdwAssemblyFlags);      // [OUT] Flags.

    HResult GetAssemblyRefProps(
        MdAssemblyRef mdar,             // [IN] The AssemblyRef for which to get the properties.
        byte* ppbPublicKeyOrToken,      // [OUT] Pointer to the public key or token. const void** ppbPublicKeyOrToken
        int* pcbPublicKeyOrToken,       // [OUT] Count of bytes in the public key or token.
        char* szName,                   // [OUT] Buffer to fill with name.
        uint cchName,                   // [IN] Size of buffer in wide chars.
        uint* pchName,               // [OUT] Actual # of wide chars in name.
        ASSEMBLYMETADATA* pMetaData,    // [OUT] Assembly MetaData.
        byte* ppbHashValue,             // [OUT] Hash blob. const void** ppbHashValue
        int* pcbHashValue,           // [OUT] Count of bytes in the hash blob.
        int* pdwAssemblyRefFlags);   // [OUT] Flags.

    HResult GetFileProps(
        MdFile mdf,                     // [IN] The File for which to get the properties.
        char* szName,                   // [OUT] Buffer to fill with name.
        uint cchName,                   // [IN] Size of buffer in wide chars.
        out uint pchName,               // [OUT] Actual # of wide chars in name.
        byte* ppbHashValue,             // [OUT] Pointer to the Hash Value Blob. const void** ppbHashValue
        out int pcbHashValue,           // [OUT] Count of bytes in the Hash Value Blob.
        out int pdwFileFlags);          // [OUT] Flags.

    HResult GetExportedTypeProps(       // S_OK or error.
        MdExportedType mdct,            // [IN] The ExportedType for which to get the properties.
        char* szName,                   // [OUT] Buffer to fill with name.
        uint cchName,                   // [IN] Size of buffer in wide chars.
        out uint pchName,               // [OUT] Actual # of wide chars in name.
        MdToken* ptkImplementation,     // [OUT] MdFile or MdAssemblyRef or mdExportedType.
        MdTypeDef* ptkTypeDef,          // [OUT] TypeDef token within the file.
        out int pdwExportedTypeFlags);  // [OUT] Flags.

    HResult GetManifestResourceProps(   // S_OK or error.
        MdManifestResource mdmr,        // [IN] The ManifestResource for which to get the properties.
        char* szName,                   // [OUT] Buffer to fill with name.
        uint cchName,                   // [IN] Size of buffer in wide chars.
        out uint pchName,               // [OUT] Actual # of wide chars in name.
        MdToken* ptkImplementation,     // [OUT] MdFile or MdAssemblyRef that provides the ManifestResource.
        out int pdwOffset,              // [OUT] Offset to the beginning of the resource within the file.
        out int pdwResourceFlags);      // [OUT] Flags.

    HResult EnumAssemblyRefs(           // S_OK or error
        HCORENUM* phEnum,               // [IN|OUT] Pointer to the enum.
        MdAssemblyRef* rAssemblyRefs,   // [OUT] Put AssemblyRefs here.
        uint cMax,                      // [IN] Max AssemblyRefs to put.
        out uint pcTokens);              // [OUT] Put # put here.

    HResult EnumFiles(                  // S_OK or error
        HCORENUM* phEnum,               // [IN|OUT] Pointer to the enum.
        MdFile* rFiles,                 // [OUT] Put Files here.
        uint cMax,                      // [IN] Max Files to put.
        out uint pcTokens);              // [OUT] Put # put here.

    HResult EnumExportedTypes(              // S_OK or error
        HCORENUM* phEnum,                   // [IN|OUT] Pointer to the enum.
        MdExportedType* rExportedTypes,     // [OUT] Put ExportedTypes here.
        uint cMax,                          // [IN] Max ExportedTypes to put.
        out uint pcTokens);                  // [OUT] Put # put here.

    HResult EnumManifestResources(                  // S_OK or error
        HCORENUM* phEnum,                           // [IN|OUT] Pointer to the enum.
        MdManifestResource* rManifestResources,     // [OUT] Put ManifestResources here.
        uint cMax,                                  // [IN] Max Resources to put.
        out uint pcTokens);                          // [OUT] Put # put here.

    HResult GetAssemblyFromScope(        // S_OK or error
        out MdAssembly ptkAssembly);     // [OUT] Put token here.

    HResult FindExportedTypeByName(      // S_OK or error
        char* szName,                 // [IN] Name of the ExportedType.
        MdToken mdtExportedType,        // [IN] ExportedType for the enclosing class.
        MdExportedType* ptkExportedType); // [OUT] Put the ExportedType token here.

    HResult FindManifestResourceByName(  // S_OK or error
        char* szName,                 // [IN] Name of the ManifestResource.
        MdManifestResource* ptkManifestResource);  // [OUT] Put the ManifestResource token here.

    void CloseEnum(HCORENUM hEnum);               // Enum to be closed.

    HResult FindAssembliesByName(       // S_OK or error
        char* szAppBase,                // [IN] optional - can be NULL
        char* szPrivateBin,             // [IN] optional - can be NULL
        char* szAssemblyName,           // [IN] required - this is the assembly you are requesting
        IntPtr* ppIUnk,                 // [OUT] put IMetaDataAssemblyImport pointers here IUnknown* ppIUnk[]
        uint cMax,                      // [IN] The max number to put
        out uint pcAssemblies);          // [OUT] The number of assemblies returned.
}
