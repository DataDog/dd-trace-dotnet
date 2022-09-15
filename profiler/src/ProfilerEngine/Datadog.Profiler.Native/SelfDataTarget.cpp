#include "SelfDataTarget.h"
#include "cordebug.h"
#include "HResultConverter.h"

#include <filesystem>
#include <iostream>

#ifndef _WINDOWS
#include <link.h>
#endif


#define MIDL_DEFINE_GUID(type,name,l,w1,w2,b1,b2,b3,b4,b5,b6,b7,b8) \
        EXTERN_C __declspec(selectany) const type name = {l,w1,w2,{b1,b2,b3,b4,b5,b6,b7,b8}}

MIDL_DEFINE_GUID(IID, IID_ICLRDataTarget2,0x6d05fae3,0x189c,0x4630,0xa6,0xdc,0x1c,0x25,0x1e,0x1c,0x01,0xab);

HRESULT SelfDataTarget::QueryInterface(REFIID riid, void** ppvObject)
{
    if (riid == IID_IUnknown)
    {
        std::cout << "SelfDataTarget::QueryInterface: IUnknown" << std::endl;
        *ppvObject = static_cast<IUnknown*>(this);
    }
    else if (riid == IID_ICLRDataTarget2)
    {
        std::cout << "SelfDataTarget::QueryInterface: ICLRDataTarget2" << std::endl;
        *ppvObject = static_cast<ICLRDataTarget2*>(this);
    }
    else
    {
        std::cout << "SelfDataTarget::QueryInterface: Unknown interface" << std::endl;
        return E_NOINTERFACE;
    }

    AddRef();

    return S_OK;
}

ULONG SelfDataTarget::AddRef()
{
    return _refCount.fetch_add(1);
}

ULONG SelfDataTarget::Release()
{
    const auto newCount = _refCount.fetch_sub(1);

    if (newCount == 0)
    {
        delete this;
    }

    return newCount;
}

HRESULT SelfDataTarget::GetMachineType(ULONG32* machine)
{
    std::cout << "SelfDataTarget::GetMachineType" << std::endl;
    *machine = 0x8664; // IMAGE_FILE_MACHINE_AMD64
    return S_OK;
}

HRESULT SelfDataTarget::GetPointerSize(ULONG32* size)
{
    std::cout << "SelfDataTarget::GetPointerSize" << std::endl;
    *size = 8;
    return S_OK;
}

#ifndef _WINDOWS

int GetImageBaseCallback(struct dl_phdr_info *info, size_t size, void *data)
{
    auto result = (std::pair<LPCWSTR, size_t>*)data;

    std::cout << "Module " << info->dlpi_name << " at address " << std::hex << info->dlpi_addr << std::endl;

    auto path = std::filesystem::path(info->dlpi_name);

    auto fileName = path.filename();

    if (fileName == result->first)
    {
        result->second = info->dlpi_addr;
    }

    return 0;
}

#endif

HRESULT SelfDataTarget::GetImageBase(LPCWSTR moduleName, CLRDATA_ADDRESS* baseAddress)
{
    std::cout << "GetImageBase - " << moduleName << std::endl;
    
#ifdef _WINDOWS
    HMODULE module;

    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT, moduleName, &module))
    {
        std::cout << "Locating the module failed" << std::endl;
        return E_FAIL;
    }
    
    *baseAddress = (CLRDATA_ADDRESS)module;

#else
    std::pair<LPCWSTR, size_t> result;

    result.first = moduleName;

    dl_iterate_phdr(&GetImageBaseCallback, &result);

    if (result.second == 0) {
        std::cout << "Locating the module failed" << std::endl;
        return E_FAIL;
    }

    *baseAddress = result.second;
#endif

    std::cout << "Base address: " << std::hex << *baseAddress << std::endl;

    return S_OK;
}

HRESULT SelfDataTarget::ReadVirtual(CLRDATA_ADDRESS address, PBYTE buffer, ULONG32 size, ULONG32* done)
{
    //std::cout << "SelfDataTarget::ReadVirtual" << std::endl;

    memcpy(buffer, (BYTE*)address, size);
    *done = size;

    return S_OK;
}

HRESULT SelfDataTarget::WriteVirtual(CLRDATA_ADDRESS address, PBYTE buffer, ULONG32 size, ULONG32* done)
{
    std::cout << "WriteVirtual" << std::endl;
    return E_NOTIMPL;
}

HRESULT SelfDataTarget::GetTLSValue(ULONG32 threadID, ULONG32 index, CLRDATA_ADDRESS* value)
{
    std::cout << "GetTLSValue" << std::endl;
    return E_NOTIMPL;
}

HRESULT SelfDataTarget::SetTLSValue(ULONG32 threadID, ULONG32 index, CLRDATA_ADDRESS value)
{
    std::cout << "SetTLSValue" << std::endl;
    return E_NOTIMPL;
}

HRESULT SelfDataTarget::GetCurrentThreadID(ULONG32* threadID)
{
    std::cout << "GetCurrentThreadID" << std::endl;
    return E_NOTIMPL;
}

HRESULT SelfDataTarget::GetThreadContext(ULONG32 threadID, ULONG32 contextFlags, ULONG32 contextSize, PBYTE buffer)
{
    //std::cout << "SelfDataTarget::GetThreadContext" << std::endl;
#ifdef _WINDOWS
    const auto thread = OpenThread(THREAD_ALL_ACCESS, FALSE, threadID);

    BOOL success = false;

    CONTEXT context = {0};
    context.ContextFlags = contextFlags;

    if (thread != nullptr)
    {
        success = ::GetThreadContext(thread, &context);
    }

    //std::cout << "ThreadContext " << success << " rip : " << std::hex << context.Rip << std::endl;

    memcpy(&buffer, &context, contextSize);

    CloseHandle(thread);

    return success ? S_OK : E_FAIL;
#else
    if (OverrideIp != 0)
    {
        CONTEXT_CaptureContext((LPCONTEXT)buffer);

        auto context = (LPCONTEXT)buffer;

        /*CONTEXT context = {0};
        context.ContextFlags = contextFlags;*/

        //std::cout << "Storing: " << OverrideIp << " - " << OverrideRsp << " - " << OverrideRbp << std::endl;

        context->Rip = OverrideIp;
        context->Rsp = OverrideRsp;
        context->Rbp = OverrideRbp;
        context->Rdi = OverrideRdi;
        context->Rsi = OverrideRsi;
        context->Rbx = OverrideRbx;
        context->Rdx = OverrideRdx;
        context->Rcx = OverrideRcx;
        context->Rax = OverrideRax;
        context->R8 = OverrideR8;
        context->R9 = OverrideR9;
        context->R10 = OverrideR10;
        context->R11 = OverrideR11;
        context->R12 = OverrideR12;
        context->R13 = OverrideR13;
        context->R14 = OverrideR14;
        context->R15 = OverrideR15;

        /*context.Rip = OverrideIp;
        context.Rsp = OverrideRsp;
        context.Rbp = OverrideRbp;
        context.Rdi = OverrideRdi;
        context.Rsi = OverrideRsi;
        context.Rbx = OverrideRbx;
        context.Rdx = OverrideRdx;
        context.Rcx = OverrideRcx;
        context.Rax = OverrideRax;
        context.R8 = OverrideR8;
        context.R9 = OverrideR9;
        context.R10 = OverrideR10;
        context.R11 = OverrideR11;
        context.R12 = OverrideR12;
        context.R13 = OverrideR13;
        context.R14 = OverrideR14;
        context.R15 = OverrideR15;*/

        //memcpy(&buffer, &context, contextSize);

        //std::cout << "Using overriden registers: " << context.Rip << " - " << context.Rsp << " - " << context.Rbp << std::endl;
        //std::cout << "Using overriden registers: " << context->Rip << " - " << context->Rsp << " - " << context->Rbp << std::endl;
    }
    else
    {
        CONTEXT_CaptureContext((LPCONTEXT)buffer);        
    }

    return S_OK;
#endif
}

HRESULT SelfDataTarget::SetThreadContext(ULONG32 threadID, ULONG32 contextSize, PBYTE context)
{
    std::cout << "SelfDataTarget::SetThreadContext" << std::endl;
    return E_NOTIMPL;
}

HRESULT SelfDataTarget::Request(ULONG32 reqCode, ULONG32 inBufferSize, BYTE* inBuffer, ULONG32 outBufferSize,
                                BYTE* outBuffer)
{
    std::cout << "SelfDataTarget::Request" << std::endl;
    return E_NOTIMPL;
}

HRESULT SelfDataTarget::AllocVirtual(CLRDATA_ADDRESS addr, ULONG32 size, ULONG32 typeFlags, ULONG32 protectFlags,
                                     CLRDATA_ADDRESS* virt)
{
    std::cout << "SelfDataTarget::AllocVirtual" << std::endl;
    return E_NOTIMPL;
}

HRESULT SelfDataTarget::FreeVirtual(CLRDATA_ADDRESS addr, ULONG32 size, ULONG32 typeFlags)
{
    std::cout << "SelfDataTarget::FreeVirtual" << std::endl;
    return E_NOTIMPL;
}

// HRESULT SelfDataTarget::GetPlatform(CorDebugPlatform* pTargetPlatform)
//{
//     std::cout << "SelfDataTarget::GetPlatform" << std::endl;
//
//#ifdef _WINDOWS
//     *pTargetPlatform = CORDB_PLATFORM_WINDOWS_AMD64;
//#else
//     *pTargetPlatform = CORDB_PLATFORM_POSIX_AMD64;
//#endif
//
//     return S_OK;
// }
//
// HRESULT SelfDataTarget::ReadVirtual(CORDB_ADDRESS address, BYTE* pBuffer, ULONG32 bytesRequested, ULONG32* pBytesRead)
//{
//     std::cout << "SelfDataTarget::ReadVirtual" << std::endl;
//
//     memcpy(pBuffer, (BYTE*)address, bytesRequested);
//     *pBytesRead = bytesRequested;
//
//     return S_OK;
// }
//
// HRESULT SelfDataTarget::GetThreadContext(DWORD dwThreadID, ULONG32 contextFlags, ULONG32 contextSize, BYTE* pContext)
//{
//     std::cout << "SelfDataTarget::GetThreadContext" << std::endl;
//
//     const auto thread = OpenThread(THREAD_GET_CONTEXT, FALSE, dwThreadID);
//
//     BOOL success = false;
//
//     if (thread != nullptr)
//     {
//         success = ::GetThreadContext(thread, (LPCONTEXT)pContext);
//     }
//
//     CloseHandle(thread);
//
//     return success ? S_OK : E_FAIL;
// }
