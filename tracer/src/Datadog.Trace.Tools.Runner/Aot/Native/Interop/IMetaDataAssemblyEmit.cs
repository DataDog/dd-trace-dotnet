// <copyright file="IMetaDataAssemblyEmit.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

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
        out MdAssembly pma)
    {
        pma = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataAssemblyEmit.DefineAssembly");
        return HResult.E_NOTIMPL;
    }            // [OUT] Returned Assembly token.

    HResult DefineAssemblyRef(
        IntPtr pbPublicKeyOrToken,      // [IN] Public key or token of the assembly.
        int cbPublicKeyOrToken,         // [IN] Count of bytes in the public key or token.
        char* szName,                   // [IN] Name of the assembly being referenced.
        ASSEMBLYMETADATA* pMetaData,    // [IN] Assembly MetaData.
        IntPtr  pbHashValue,            // [IN] Hash Blob.
        int cbHashValue,                // [IN] Count of bytes in the Hash Blob.
        int dwAssemblyRefFlags,         // [IN] Flags.
        MdToken* pmdar)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataAssemblyEmit.DefineAssemblyRef");
        return HResult.E_NOTIMPL;
    }          // [OUT] Returned AssemblyRef token.

    HResult DefineFile(
        char* szName,                   // [IN] Name of the file.
        IntPtr pbHashValue,             // [IN] Hash Blob.
        int cbHashValue,                // [IN] Count of bytes in the Hash Blob.
        int dwFileFlags,                // [IN] Flags.
        out MdFile pmdf)
    {
        pmdf = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataAssemblyEmit.DefineFile");
        return HResult.E_NOTIMPL;
    }               // [OUT] Returned File token.

    HResult DefineExportedType(
        char* szName,                   // [IN] Name of the Com Type.
        MdToken tkImplementation,       // [IN] MdFile or MdAssemblyRef or MdExportedType
        MdTypeDef tkTypeDef,            // [IN] TypeDef token within the file.
        int dwExportedTypeFlags,        // [IN] Flags.
        MdExportedType* pmdct)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataAssemblyEmit.DefineExportedType");
        return HResult.E_NOTIMPL;
    }         // [OUT] Returned ExportedType token.

    HResult DefineManifestResource(     // S_OK or error.
        char* szName,                   // [IN] Name of the resource.
        MdToken tkImplementation,       // [IN] MdFile or MdAssemblyRef that provides the resource.
        int dwOffset,                   // [IN] Offset to the beginning of the resource within the file.
        int dwResourceFlags,            // [IN] Flags.
        MdManifestResource* pmdmr)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataAssemblyEmit.DefineManifestResource");
        return HResult.E_NOTIMPL;
    }     // [OUT] Returned ManifestResource token.

    HResult SetAssemblyProps(           // S_OK or error.
        MdAssembly pma,                 // [IN] Assembly token.
        IntPtr pbPublicKey,             // [IN] Public key of the assembly.
        int cbPublicKey,                // [IN] Count of bytes in the public key.
        int ulHashAlgId,                // [IN] Hash algorithm used to hash the files.
        char* szName,                   // [IN] Name of the assembly.
        ASSEMBLYMETADATA* pMetaData,    // [IN] Assembly MetaData.
        int dwAssemblyFlags)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataAssemblyEmit.SetAssemblyProps");
        return HResult.E_NOTIMPL;
    }           // [IN] Flags.

    HResult SetAssemblyRefProps(        // S_OK or error.
        MdAssemblyRef ar,               // [IN] AssemblyRefToken.
        IntPtr pbPublicKeyOrToken,      // [IN] Public key or token of the assembly.
        int cbPublicKeyOrToken,         // [IN] Count of bytes in the public key or token.
        char* szName,                   // [IN] Name of the assembly being referenced.
        ASSEMBLYMETADATA* pMetaData,    // [IN] Assembly MetaData.
        IntPtr  pbHashValue,            // [IN] Hash Blob.
        int cbHashValue,                // [IN] Count of bytes in the Hash Blob.
        int dwAssemblyRefFlags)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataAssemblyEmit.SetAssemblyRefProps");
        return HResult.E_NOTIMPL;
    }        // [IN] Token for Execution Location.

    HResult SetFileProps(               // S_OK or error.
        MdFile file,                    // [IN] File token.
        IntPtr pbHashValue,             // [IN] Hash Blob.
        int cbHashValue,                // [IN] Count of bytes in the Hash Blob.
        int dwFileFlags)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataAssemblyEmit.SetFileProps");
        return HResult.E_NOTIMPL;
    }               // [IN] Flags.

    HResult SetExportedTypeProps(       // S_OK or error.
        MdExportedType ct,              // [IN] ExportedType token.
        MdToken tkImplementation,       // [IN] MdFile or MdAssemblyRef or MdExportedType.
        MdTypeDef tkTypeDef,            // [IN] TypeDef token within the file.
        int dwExportedTypeFlags)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataAssemblyEmit.SetExportedTypeProps");
        return HResult.E_NOTIMPL;
    }       // [IN] Flags.

    HResult SetManifestResourceProps(   // S_OK or error.
        MdManifestResource mr,          // [IN] ManifestResource token.
        MdToken tkImplementation,       // [IN] MdFile or MdAssemblyRef that provides the resource.
        int dwOffset,                   // [IN] Offset to the beginning of the resource within the file.
        int dwResourceFlags)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataAssemblyEmit.SetManifestResourceProps");
        return HResult.E_NOTIMPL;
    }           // [IN] Flags.
}
#endif
