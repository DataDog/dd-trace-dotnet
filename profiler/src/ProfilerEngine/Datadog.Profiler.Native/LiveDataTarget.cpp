// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LiveDataTarget.h"

#include "InProcessMemoryReader.h"

#ifdef _WINDOWS
#include <windows.h>
#endif

// On non-Windows, define TARGET_UNIX/HOST_UNIX before the DAC headers so their layouts/conditionals
// match the Linux runtime, consistent with how coreclr compiles them.
#if !defined(_WINDOWS)
#ifndef TARGET_UNIX
#define TARGET_UNIX
#endif
#ifndef HOST_UNIX
#define HOST_UNIX
#endif
#endif

// clrdata.h (and the basic COM/PAL types it needs) is intentionally included ONLY in this .cpp so
// the DAC headers never leak into shared headers (this is the first DAC consumer in the profiler).
#include "clrdata.h"

#include <atomic>
#include <cstring>
#include <new>

// Image machine-type constants (winnt.h on Windows; defined defensively for the PAL build).
#ifndef IMAGE_FILE_MACHINE_I386
#define IMAGE_FILE_MACHINE_I386 0x014c
#endif
#ifndef IMAGE_FILE_MACHINE_AMD64
#define IMAGE_FILE_MACHINE_AMD64 0x8664
#endif
#ifndef IMAGE_FILE_MACHINE_ARM64
#define IMAGE_FILE_MACHINE_ARM64 0xAA64
#endif
#ifndef IMAGE_FILE_MACHINE_ARMNT
#define IMAGE_FILE_MACHINE_ARMNT 0x01c4
#endif

namespace dac
{
namespace
{
ULONG32 GetCurrentMachineType()
{
#if defined(_M_X64) || defined(__x86_64__)
    return IMAGE_FILE_MACHINE_AMD64;
#elif defined(_M_IX86) || defined(__i386__)
    return IMAGE_FILE_MACHINE_I386;
#elif defined(_M_ARM64) || defined(__aarch64__)
    return IMAGE_FILE_MACHINE_ARM64;
#elif defined(_M_ARM) || defined(__arm__)
    return IMAGE_FILE_MACHINE_ARMNT;
#else
    return IMAGE_FILE_MACHINE_AMD64;
#endif
}

// ICLRDataTarget implementation reading the profiler's own process memory. Only the members the DAC
// needs for native-heap enumeration are implemented; the rest return E_NOTIMPL.
class LiveDataTarget : public ICLRDataTarget
{
public:
    explicit LiveDataTarget(uint64_t runtimeModuleBase) :
        _refCount(1), _runtimeModuleBase(runtimeModuleBase)
    {
    }

    virtual ~LiveDataTarget() = default;

    // IUnknown
    HRESULT STDMETHODCALLTYPE QueryInterface(REFIID riid, void** ppvObject) override
    {
        if (ppvObject == nullptr)
        {
            return E_POINTER;
        }

        if (riid == __uuidof(IUnknown) || riid == __uuidof(ICLRDataTarget))
        {
            *ppvObject = static_cast<ICLRDataTarget*>(this);
            AddRef();
            return S_OK;
        }

        *ppvObject = nullptr;
        return E_NOINTERFACE;
    }

    ULONG STDMETHODCALLTYPE AddRef() override
    {
        return static_cast<ULONG>(++_refCount);
    }

    ULONG STDMETHODCALLTYPE Release() override
    {
        long count = --_refCount;
        if (count == 0)
        {
            delete this;
        }
        return static_cast<ULONG>(count);
    }

    // ICLRDataTarget
    HRESULT STDMETHODCALLTYPE GetMachineType(ULONG32* machineType) override
    {
        if (machineType == nullptr)
        {
            return E_INVALIDARG;
        }
        *machineType = GetCurrentMachineType();
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetPointerSize(ULONG32* pointerSize) override
    {
        if (pointerSize == nullptr)
        {
            return E_INVALIDARG;
        }
        *pointerSize = static_cast<ULONG32>(sizeof(void*));
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE GetImageBase(LPCWSTR /*imagePath*/, CLRDATA_ADDRESS* baseAddress) override
    {
        // The DAC only asks for the runtime module it is matched against, whose base we already
        // resolved when locating the DAC. Return it regardless of the requested name.
        if (baseAddress == nullptr)
        {
            return E_INVALIDARG;
        }
        if (_runtimeModuleBase == 0)
        {
            return E_FAIL;
        }
        *baseAddress = static_cast<CLRDATA_ADDRESS>(_runtimeModuleBase);
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE ReadVirtual(
        CLRDATA_ADDRESS address,
        BYTE* buffer,
        ULONG32 bytesRequested,
        ULONG32* bytesRead) override
    {
        if (buffer == nullptr)
        {
            return E_INVALIDARG;
        }

        if (!_reader.ReadMemory(static_cast<uintptr_t>(address), buffer, bytesRequested))
        {
            if (bytesRead != nullptr)
            {
                *bytesRead = 0;
            }
            return E_FAIL;
        }

        if (bytesRead != nullptr)
        {
            *bytesRead = bytesRequested;
        }
        return S_OK;
    }

    HRESULT STDMETHODCALLTYPE WriteVirtual(CLRDATA_ADDRESS, BYTE*, ULONG32, ULONG32*) override
    {
        return E_NOTIMPL;
    }

    HRESULT STDMETHODCALLTYPE GetTLSValue(ULONG32, ULONG32, CLRDATA_ADDRESS*) override
    {
        return E_NOTIMPL;
    }

    HRESULT STDMETHODCALLTYPE SetTLSValue(ULONG32, ULONG32, CLRDATA_ADDRESS) override
    {
        return E_NOTIMPL;
    }

    HRESULT STDMETHODCALLTYPE GetCurrentThreadID(ULONG32*) override
    {
        return E_NOTIMPL;
    }

    HRESULT STDMETHODCALLTYPE GetThreadContext(ULONG32, ULONG32, ULONG32, BYTE*) override
    {
        return E_NOTIMPL;
    }

    HRESULT STDMETHODCALLTYPE SetThreadContext(ULONG32, ULONG32, BYTE*) override
    {
        return E_NOTIMPL;
    }

    HRESULT STDMETHODCALLTYPE Request(ULONG32, ULONG32, BYTE*, ULONG32, BYTE*) override
    {
        return E_NOTIMPL;
    }

private:
    std::atomic<long> _refCount;
    uint64_t _runtimeModuleBase;
    InProcessMemoryReader _reader;
};
} // namespace

ICLRDataTarget* CreateLiveDataTarget(uint64_t runtimeModuleBase)
{
    return new (std::nothrow) LiveDataTarget(runtimeModuleBase);
}
} // namespace dac
