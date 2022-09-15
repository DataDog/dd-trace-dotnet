#pragma once

#include "cor.h"
#include "CorDebug.h"
#include <algorithm>
#include <atomic>
#include <clrdata.h>

#ifndef _WINDOWS

extern "C" void
__attribute__((visibility("default"))) __cdecl
CONTEXT_CaptureContext(
    LPCONTEXT lpContext
    );
#endif

class SelfDataTarget : ICLRDataTarget2
{
public:

    HRESULT STDMETHODCALLTYPE QueryInterface( 
        /* [in] */ REFIID riid,
        /* [iid_is][out] */ void** ppvObject) override;

    ULONG STDMETHODCALLTYPE AddRef( void) override;

    ULONG STDMETHODCALLTYPE Release( void) override;

    virtual ~SelfDataTarget() = default;

    //
    // ICLRDataTarget
    //
    HRESULT STDMETHODCALLTYPE GetMachineType(
        /* [out] */ ULONG32 *machine) override;

    HRESULT STDMETHODCALLTYPE GetPointerSize(
        /* [out] */ ULONG32 *size) override;

    HRESULT STDMETHODCALLTYPE GetImageBase(
        /* [string][in] */ LPCWSTR moduleName,
        /* [out] */ CLRDATA_ADDRESS *baseAddress) override;

    HRESULT STDMETHODCALLTYPE ReadVirtual(
        /* [in] */ CLRDATA_ADDRESS address,
        /* [length_is][size_is][out] */ PBYTE buffer,
        /* [in] */ ULONG32 size,
        /* [optional][out] */ ULONG32 *done) override;

    HRESULT STDMETHODCALLTYPE WriteVirtual(
        /* [in] */ CLRDATA_ADDRESS address,
        /* [size_is][in] */ PBYTE buffer,
        /* [in] */ ULONG32 size,
        /* [optional][out] */ ULONG32 *done) override;

    HRESULT STDMETHODCALLTYPE GetTLSValue(
        /* [in] */ ULONG32 threadID,
        /* [in] */ ULONG32 index,
        /* [out] */ CLRDATA_ADDRESS* value) override;

    HRESULT STDMETHODCALLTYPE SetTLSValue(
        /* [in] */ ULONG32 threadID,
        /* [in] */ ULONG32 index,
        /* [in] */ CLRDATA_ADDRESS value) override;

    HRESULT STDMETHODCALLTYPE GetCurrentThreadID(
        /* [out] */ ULONG32* threadID) override;

    HRESULT STDMETHODCALLTYPE GetThreadContext(
        /* [in] */ ULONG32 threadID,
        /* [in] */ ULONG32 contextFlags,
        /* [in] */ ULONG32 contextSize,
        /* [out, size_is(contextSize)] */ PBYTE context) override;

    HRESULT STDMETHODCALLTYPE SetThreadContext(
        /* [in] */ ULONG32 threadID,
        /* [in] */ ULONG32 contextSize,
        /* [in, size_is(contextSize)] */ PBYTE context) override;

    HRESULT STDMETHODCALLTYPE Request(
        /* [in] */ ULONG32 reqCode,
        /* [in] */ ULONG32 inBufferSize,
        /* [size_is][in] */ BYTE *inBuffer,
        /* [in] */ ULONG32 outBufferSize,
        /* [size_is][out] */ BYTE *outBuffer) override;


        HRESULT STDMETHODCALLTYPE AllocVirtual( 
            /* [in] */ CLRDATA_ADDRESS addr,
            /* [in] */ ULONG32 size,
            /* [in] */ ULONG32 typeFlags,
            /* [in] */ ULONG32 protectFlags,
            /* [out] */ CLRDATA_ADDRESS *virt) override;
        
        HRESULT STDMETHODCALLTYPE FreeVirtual( 
            /* [in] */ CLRDATA_ADDRESS addr,
            /* [in] */ ULONG32 size,
            /* [in] */ ULONG32 typeFlags) override;




        //    virtual HRESULT STDMETHODCALLTYPE AllocVirtual( 
        //    /* [in] */ CLRDATA_ADDRESS addr,
        //    /* [in] */ ULONG32 size,
        //    /* [in] */ ULONG32 typeFlags,
        //    /* [in] */ ULONG32 protectFlags,
        //    /* [out] */ CLRDATA_ADDRESS *virt) = 0;
        //
        //virtual HRESULT STDMETHODCALLTYPE FreeVirtual( 
        //    /* [in] */ CLRDATA_ADDRESS addr,
        //    /* [in] */ ULONG32 size,
        //    /* [in] */ ULONG32 typeFlags) = 0;



        //HRESULT STDMETHODCALLTYPE GetPlatform( 
        //    /* [out] */ CorDebugPlatform *pTargetPlatform) override;
        //
        //HRESULT STDMETHODCALLTYPE ReadVirtual( 
        //    /* [in] */ CORDB_ADDRESS address,
        //    /* [length_is][size_is][out] */ BYTE *pBuffer,
        //    /* [in] */ ULONG32 bytesRequested,
        //    /* [out] */ ULONG32 *pBytesRead) override;
        //
        //HRESULT STDMETHODCALLTYPE GetThreadContext( 
        //    /* [in] */ DWORD dwThreadID,
        //    /* [in] */ ULONG32 contextFlags,
        //    /* [in] */ ULONG32 contextSize,
        //    /* [size_is][out] */ BYTE *pContext) override;

public:
    uint64_t OverrideIp;
    uint64_t OverrideRsp;
    uint64_t OverrideRbp;
    uint64_t OverrideRdi;
    uint64_t OverrideRsi;
    uint64_t OverrideRbx;
    uint64_t OverrideRdx;
    uint64_t OverrideRcx;
    uint64_t OverrideRax;
    uint64_t OverrideR8;
    uint64_t OverrideR9;
    uint64_t OverrideR10;
    uint64_t OverrideR11;
    uint64_t OverrideR12;
    uint64_t OverrideR13;
    uint64_t OverrideR14;
    uint64_t OverrideR15;

private:
    std::atomic<ULONG> _refCount;
};
