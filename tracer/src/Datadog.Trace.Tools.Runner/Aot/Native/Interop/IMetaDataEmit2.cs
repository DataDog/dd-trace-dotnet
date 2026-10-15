// <copyright file="IMetaDataEmit2.cs" company="Datadog">
// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2017 Datadog, Inc.
// </copyright>

#if NET6_0_OR_GREATER
#nullable disable

using System;
using System.Runtime.InteropServices;

namespace Datadog.Trace.Tools.Runner.Aot.Native;

[NativeObject]
internal unsafe interface IMetaDataEmit2 : IMetaDataEmit
{
    public static readonly new Guid Guid = new("F5DD9950-F693-42e6-830E-7B833E8146A9");

    HResult DefineMethodSpec(
        MdToken tkParent,               // [IN] MethodDef or MemberRef
        IntPtr pvSigBlob,          // [IN] point to a blob value of COM+ signature PCCOR_SIGNATURE pvSigBlob
        int cbSigBlob,              // [IN] count of bytes in the signature blob
        MdMethodSpec* pmi)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataEmit2.DefineMethodSpec");
        return HResult.E_NOTIMPL;
    }            // [OUT] method instantiation token

    HResult GetDeltaSaveSize(            // S_OK or error.
        CorSaveSize fSave,                  // [IN] cssAccurate or cssQuick.
        out int pdwSaveSize)
    {
        pdwSaveSize = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataEmit2.GetDeltaSaveSize");
        return HResult.E_NOTIMPL;
    }     // [OUT] Put the size here.

    HResult SaveDelta(                   // S_OK or error.
        char* szFile,                 // [IN] The filename to save to.
        int dwSaveFlags)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataEmit2.SaveDelta");
        return HResult.E_NOTIMPL;
    }      // [IN] Flags for the save.

    HResult SaveDeltaToStream(           // S_OK or error.
        IntPtr pIStream,              // [IN] A writable stream to save to. IStream* pIStream
        int dwSaveFlags)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataEmit2.SaveDeltaToStream");
        return HResult.E_NOTIMPL;
    }      // [IN] Flags for the save.

    HResult SaveDeltaToMemory(           // S_OK or error.
        IntPtr pbData,                // [OUT] Location to write data.
        int cbData)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataEmit2.SaveDeltaToMemory");
        return HResult.E_NOTIMPL;
    }           // [IN] Max size of data buffer.

    HResult DefineGenericParam(          // S_OK or error.
        MdToken tk,                    // [IN] TypeDef or MethodDef
        int ulParamSeq,            // [IN] Index of the type parameter
        int dwParamFlags,          // [IN] Flags, for future use (e.g. variance)
        char* szname,                // [IN] Name
        int reserved,              // [IN] For future use (e.g. non-type parameters)
        MdToken* rtkConstraints,      // [IN] Array of type constraints (TypeDef,TypeRef,TypeSpec)
        out MdGenericParam pgp)
    {
        pgp = default;
        NativeStubDiagnostics.NotImplemented("IMetaDataEmit2.DefineGenericParam");
        return HResult.E_NOTIMPL;
    }          // [OUT] Put GenericParam token here

    HResult SetGenericParamProps(   // S_OK or error.
        MdGenericParam gp,          // [IN] GenericParam
        int dwParamFlags,           // [IN] Flags, for future use (e.g. variance)
        char* szName,               // [IN] Optional name
        int reserved,               // [IN] For future use (e.g. non-type parameters)
        MdToken* rtkConstraints)
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataEmit2.SetGenericParamProps");
        return HResult.E_NOTIMPL;
    }   // [IN] Array of type constraints (TypeDef,TypeRef,TypeSpec)

    HResult ResetENCLog()
    {
        NativeStubDiagnostics.NotImplemented("IMetaDataEmit2.ResetENCLog");
        return HResult.E_NOTIMPL;
    }          // S_OK or error.
}
#endif
