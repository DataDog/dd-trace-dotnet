namespace Datadog.Trace.Tools.AotProcessor.Interfaces;

[NativeObject]
internal unsafe interface IMetaDataAssemblyEmit : IUnknown
{
    public static readonly new Guid Guid = new("211EF15B-5317-4438-B196-DEC87B887693");

    HResult DefineAssembly(
        IntPtr pbPublicKey,             // [IN] Public key of the assembly.  IntPtr  pbPublicKey
        int cbPublicKey,                // [IN] Count of bytes in the public key.
        int ulHashAlgId,                // [IN] Hash algorithm used to hash the files.
        char* szName,                   // [IN] Name of the assembly.
        ASSEMBLYMETADATA* pMetaData,    // [IN] Assembly MetaData.
        int dwAssemblyFlags,            // [IN] Flags.
        out MdAssembly pma);            // [OUT] Returned Assembly token.

    HResult DefineAssemblyRef(
        IntPtr pbPublicKeyOrToken,      // [IN] Public key or token of the assembly.
        int cbPublicKeyOrToken,         // [IN] Count of bytes in the public key or token.
        char* szName,                   // [IN] Name of the assembly being referenced.
        ASSEMBLYMETADATA* pMetaData,    // [IN] Assembly MetaData.
        IntPtr  pbHashValue,            // [IN] Hash Blob.
        int cbHashValue,                // [IN] Count of bytes in the Hash Blob.
        int dwAssemblyRefFlags,         // [IN] Flags.
        MdToken* pmdar);          // [OUT] Returned AssemblyRef token.

    HResult DefineFile(
        char* szName,                   // [IN] Name of the file.
        IntPtr pbHashValue,             // [IN] Hash Blob.
        int cbHashValue,                // [IN] Count of bytes in the Hash Blob.
        int dwFileFlags,                // [IN] Flags.
        out MdFile pmdf);               // [OUT] Returned File token.

    HResult DefineExportedType(
        char* szName,                   // [IN] Name of the Com Type.
        MdToken tkImplementation,       // [IN] MdFile or MdAssemblyRef or MdExportedType
        MdTypeDef tkTypeDef,            // [IN] TypeDef token within the file.
        int dwExportedTypeFlags,        // [IN] Flags.
        MdExportedType* pmdct);         // [OUT] Returned ExportedType token.

    HResult DefineManifestResource(     // S_OK or error.
        char* szName,                   // [IN] Name of the resource.
        MdToken tkImplementation,       // [IN] MdFile or MdAssemblyRef that provides the resource.
        int dwOffset,                   // [IN] Offset to the beginning of the resource within the file.
        int dwResourceFlags,            // [IN] Flags.
        MdManifestResource* pmdmr);     // [OUT] Returned ManifestResource token.

    HResult SetAssemblyProps(           // S_OK or error.
        MdAssembly pma,                 // [IN] Assembly token.
        IntPtr pbPublicKey,             // [IN] Public key of the assembly.
        int cbPublicKey,                // [IN] Count of bytes in the public key.
        int ulHashAlgId,                // [IN] Hash algorithm used to hash the files.
        char* szName,                   // [IN] Name of the assembly.
        ASSEMBLYMETADATA* pMetaData,    // [IN] Assembly MetaData.
        int dwAssemblyFlags);           // [IN] Flags.

    HResult SetAssemblyRefProps(        // S_OK or error.
        MdAssemblyRef ar,               // [IN] AssemblyRefToken.
        IntPtr pbPublicKeyOrToken,      // [IN] Public key or token of the assembly.
        int cbPublicKeyOrToken,         // [IN] Count of bytes in the public key or token.
        char* szName,                   // [IN] Name of the assembly being referenced.
        ASSEMBLYMETADATA* pMetaData,    // [IN] Assembly MetaData.
        IntPtr  pbHashValue,            // [IN] Hash Blob.
        int cbHashValue,                // [IN] Count of bytes in the Hash Blob.
        int dwAssemblyRefFlags);        // [IN] Token for Execution Location.

    HResult SetFileProps(               // S_OK or error.
        MdFile file,                    // [IN] File token.
        IntPtr pbHashValue,             // [IN] Hash Blob.
        int cbHashValue,                // [IN] Count of bytes in the Hash Blob.
        int dwFileFlags);               // [IN] Flags.

    HResult SetExportedTypeProps(       // S_OK or error.
        MdExportedType ct,              // [IN] ExportedType token.
        MdToken tkImplementation,       // [IN] MdFile or MdAssemblyRef or MdExportedType.
        MdTypeDef tkTypeDef,            // [IN] TypeDef token within the file.
        int dwExportedTypeFlags);       // [IN] Flags.

    HResult SetManifestResourceProps(   // S_OK or error.
        MdManifestResource mr,          // [IN] ManifestResource token.
        MdToken tkImplementation,       // [IN] MdFile or MdAssemblyRef that provides the resource.
        int dwOffset,                   // [IN] Offset to the beginning of the resource within the file.
        int dwResourceFlags);           // [IN] Flags.
}
