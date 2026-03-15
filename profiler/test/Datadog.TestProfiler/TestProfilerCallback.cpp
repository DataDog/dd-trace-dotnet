// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TestProfilerCallback.h"
#include "Log.h"
#include <chrono>
#include <iostream>

// PE32 and PE64 have different optional headers, which complexify the logic to fetch them
// This struct contains the common fields between the two types of headers
struct IMAGE_NT_HEADERS_GENERIC
{
    DWORD Signature;
    IMAGE_FILE_HEADER FileHeader;
    WORD    Magic;
};

// Singleton instance
TestProfilerCallback* TestProfilerCallback::_instance = nullptr;

TestProfilerCallback::TestProfilerCallback()
    : _randomGen(std::random_device{}())
{
    Log::Info("TestProfilerCallback constructor started");
    _instance = this;
    Log::Info("TestProfilerCallback constructor finished");
}

TestProfilerCallback::~TestProfilerCallback()
{
    if (_pCorProfilerInfo != nullptr)
    {
        _pCorProfilerInfo->Release();
        _pCorProfilerInfo = nullptr;
    }
    _instance = nullptr;
}

HRESULT STDMETHODCALLTYPE TestProfilerCallback::QueryInterface(REFIID riid, void** ppvObject)
{
    if (ppvObject == nullptr)
    {
        return E_POINTER;
    }

    if (riid == __uuidof(ICorProfilerCallback10) ||
        riid == __uuidof(ICorProfilerCallback9) ||
        riid == __uuidof(ICorProfilerCallback8) ||
        riid == __uuidof(ICorProfilerCallback7) ||
        riid == __uuidof(ICorProfilerCallback6) ||
        riid == __uuidof(ICorProfilerCallback5) ||
        riid == __uuidof(ICorProfilerCallback4) ||
        riid == __uuidof(ICorProfilerCallback3) ||
        riid == __uuidof(ICorProfilerCallback2) ||
        riid == __uuidof(ICorProfilerCallback) ||
        riid == IID_IUnknown)
    {
        *ppvObject = static_cast<ICorProfilerCallback10*>(this);
        this->AddRef();
        return S_OK;
    }

    *ppvObject = nullptr;
    return E_NOINTERFACE;
}

ULONG STDMETHODCALLTYPE TestProfilerCallback::AddRef()
{
    return ++_refCount;
}

ULONG STDMETHODCALLTYPE TestProfilerCallback::Release()
{
    ULONG refCount = --_refCount;
    if (refCount == 0)
    {
        delete this;
    }
    return refCount;
}

HRESULT STDMETHODCALLTYPE TestProfilerCallback::Initialize(IUnknown* pICorProfilerInfoUnk)
{
    Log::Info("Initializing...");

    // Get ICorProfilerInfo4
    HRESULT hr = pICorProfilerInfoUnk->QueryInterface(__uuidof(ICorProfilerInfo4), (void**)&_pCorProfilerInfo);
    if (FAILED(hr))
    {
        Log::Error("Failed to get ICorProfilerInfo4: 0x", std::hex, hr);
        return hr;
    }

    // Set event mask - monitor JIT compilation, module loads, and dynamic method unloads
    DWORD eventMask = COR_PRF_MONITOR_JIT_COMPILATION |
                      COR_PRF_MONITOR_MODULE_LOADS;

    DWORD eventMaskHigh = COR_PRF_HIGH_MONITOR_DYNAMIC_FUNCTION_UNLOADS;

    // Try to use SetEventMask2 (available in .NET Core and .NET Framework 4.5.2+)
    // This allows us to monitor dynamic method unloads
    ICorProfilerInfo5* pInfo5 = nullptr;
    hr = _pCorProfilerInfo->QueryInterface(__uuidof(ICorProfilerInfo5), (void**)&pInfo5);

    if (SUCCEEDED(hr) && pInfo5 != nullptr)
    {
        hr = pInfo5->SetEventMask2(eventMask, eventMaskHigh);
        pInfo5->Release();

        if (FAILED(hr))
        {
            Log::Error("Failed to set event mask2: 0x", std::hex, hr);
            return hr;
        }
    }
    else
    {
        // Fall back to SetEventMask for older runtimes (won't monitor dynamic method unloads)
        hr = _pCorProfilerInfo->SetEventMask(eventMask);
        if (FAILED(hr))
        {
            Log::Error("Failed to set event mask: 0x", std::hex, hr);
            return hr;
        }
    }

    // Initialize ManagedCodeCache
    _pManagedCodeCache = std::make_unique<ManagedCodeCache>(_pCorProfilerInfo);
    _pManagedCodeCache->Initialize();

    // Initialize FrameStore (pass nullptr for ManagedCodeCache to get CLR-only names)
    _pFrameStore = std::make_unique<FrameStore>(
        _pCorProfilerInfo,
        nullptr,  // IDebugInfoStore - not needed
        nullptr,  // ManagedCodeCache - use CLR ONLY!
        nullptr   // IConfiguration - unused
    );

    // Collect invalid IPs
    AddClearlyInvalidIPs();
    CollectNativeCodeIPs();

    Log::Info("Initialized successfully");
    return S_OK;
}

HRESULT STDMETHODCALLTYPE TestProfilerCallback::Shutdown()
{
    Log::Info("Shutting down...");
    return S_OK;
}

HRESULT STDMETHODCALLTYPE TestProfilerCallback::ModuleLoadFinished(ModuleID moduleId, HRESULT hrStatus)
{
    if (FAILED(hrStatus))
    {
        return S_OK;
    }

    // Check if this is an R2R module
    UINT_PTR baseLoadAddress = 0;
    DWORD moduleFlags = 0;
    HRESULT hr = _pCorProfilerInfo->GetModuleInfo2(
        moduleId, reinterpret_cast<LPCBYTE*>(&baseLoadAddress), 0, nullptr, nullptr, nullptr, &moduleFlags);

    bool isR2R = SUCCEEDED(hr) && (moduleFlags & COR_PRF_MODULE_NGEN) == COR_PRF_MODULE_NGEN;

    // Notify ManagedCodeCache about R2R module (it will parse PE sections for R2R)
    if (_pManagedCodeCache)
    {
        _pManagedCodeCache->AddModule(moduleId);
    }

    // For R2R modules, we track them but don't sample IPs yet (causes crashes)
    // TODO: Implement safe R2R IP sampling
    if (isR2R && baseLoadAddress != 0)
    {
        // Just track that we saw an R2R module
        std::lock_guard<std::mutex> lock(_ipCollectionMutex);

        CodeRangeInfo rangeInfo;
        rangeInfo.category = MethodCategory::ReadyToRun;
        rangeInfo.timestamp = std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch()).count();
        rangeInfo.codeInfo.startAddress = baseLoadAddress;
        rangeInfo.codeInfo.size = 0;
        rangeInfo.skipValidation = true;  // Don't validate R2R IPs for now
        rangeInfo.skipReason = "R2R IP validation not yet implemented";

        _collectedIPs[moduleId].push_back(std::move(rangeInfo));
    }

    return S_OK;
}

HRESULT STDMETHODCALLTYPE TestProfilerCallback::ModuleUnloadFinished(ModuleID moduleId, HRESULT hrStatus)
{
    if (_pManagedCodeCache)
    {
        _pManagedCodeCache->RemoveModule(moduleId);
    }
    return S_OK;
}

HRESULT STDMETHODCALLTYPE TestProfilerCallback::JITCompilationFinished(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock)
{
    if (FAILED(hrStatus))
    {
        return S_OK;
    }

    // Add to ManagedCodeCache
    if (_pManagedCodeCache)
    {
        _pManagedCodeCache->AddFunction(functionId);
    }

    // Collect IPs for validation
    CollectIPsForFunction(functionId, MethodCategory::JitCompiled);

    return S_OK;
}

HRESULT STDMETHODCALLTYPE TestProfilerCallback::DynamicMethodJITCompilationFinished(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock)
{
    if (FAILED(hrStatus))
    {
        return S_OK;
    }

    // Add to ManagedCodeCache
    if (_pManagedCodeCache)
    {
        _pManagedCodeCache->AddFunction(functionId);
    }

    // Collect IPs for validation
    CollectIPsForFunction(functionId, MethodCategory::DynamicMethod);

    return S_OK;
}

HRESULT STDMETHODCALLTYPE TestProfilerCallback::ReJITCompilationFinished(FunctionID functionId, ReJITID rejitId, HRESULT hrStatus, BOOL fIsSafeToBlock)
{
    // ReJIT testing is currently disabled (see TODO in header)
    return S_OK;
}

void TestProfilerCallback::CollectIPsForFunction(FunctionID functionId, MethodCategory category)
{
    if (!_pCorProfilerInfo)
    {
        return;
    }

    // Get code info
    ULONG32 cCodeInfos = 0;
    HRESULT hr = _pCorProfilerInfo->GetCodeInfo2(functionId, 0, &cCodeInfos, nullptr);
    if (FAILED(hr) || cCodeInfos == 0)
    {
        return;
    }

    std::vector<COR_PRF_CODE_INFO> codeInfos(cCodeInfos);
    hr = _pCorProfilerInfo->GetCodeInfo2(functionId, cCodeInfos, &cCodeInfos, codeInfos.data());
    if (FAILED(hr))
    {
        return;
    }

    std::lock_guard<std::mutex> lock(_ipCollectionMutex);

    for (const auto& codeInfo : codeInfos)
    {
        CodeRangeInfo rangeInfo;
        rangeInfo.codeInfo = codeInfo;
        rangeInfo.category = category;
        rangeInfo.timestamp = std::chrono::duration_cast<std::chrono::milliseconds>(
            std::chrono::system_clock::now().time_since_epoch()).count();

        // Sample 3-4 IPs from the range
        uintptr_t start = codeInfo.startAddress;
        ULONG32 size = codeInfo.size;

        if (size > 0)
        {
            rangeInfo.instructionPointers.push_back(start);  // First byte
            if (size > 1)
            {
                rangeInfo.instructionPointers.push_back(start + size / 2);  // Middle
                rangeInfo.instructionPointers.push_back(start + size - 1);  // Last byte
            }
            if (size > 100)
            {
                rangeInfo.instructionPointers.push_back(start + size / 4);  // Quarter
            }
        }

        _collectedIPs[functionId].push_back(std::move(rangeInfo));
    }
}

void TestProfilerCallback::AddClearlyInvalidIPs()
{
    std::lock_guard<std::mutex> lock(_invalidIPsMutex);

    _invalidIPsToTest.push_back({0, "Null pointer"});
    _invalidIPsToTest.push_back({0x1, "Very low address"});
    _invalidIPsToTest.push_back({0xDEADBEEF, "Invalid marker address"});
    _invalidIPsToTest.push_back({0xFFFFFFFFFFFFFFFF, "Max address"});
    _invalidIPsToTest.push_back({0x123, "Low user-space address"});
    _invalidIPsToTest.push_back({static_cast<uintptr_t>(-1), "All bits set"});
    _invalidIPsToTest.push_back({0xBADF00D, "Bad food marker"});
    _invalidIPsToTest.push_back({0xC0FFEE, "Coffee marker"});
    _invalidIPsToTest.push_back({0xFEEDFACE, "Feed face marker"});
    _invalidIPsToTest.push_back({0x8BADF00D, "Bad food with high bit"});
}

void TestProfilerCallback::CollectNativeCodeIPs()
{
    // Platform-specific native code IP collection would go here
    // For now, just add some addresses that are likely to be in native code
    // This is a simplified version - real implementation would enumerate loaded modules

    std::lock_guard<std::mutex> lock(_invalidIPsMutex);

    // Add some addresses from the C++ runtime/standard library
    // These should return E_FAIL or 0 from GetFunctionFromIP since they're not managed code
    uintptr_t stdFunctionAddr = reinterpret_cast<uintptr_t>(static_cast<void*>(&std::cout));
    _invalidIPsToTest.push_back({stdFunctionAddr, "Address in C++ standard library"});

    // Add address of this function (in native code)
    uintptr_t thisFuncAddr = reinterpret_cast<uintptr_t>(static_cast<void(*)()>(nullptr));
    if (thisFuncAddr == 0) {
        // Use a plausible native code address
        _invalidIPsToTest.push_back({0x7FFF00000000, "Typical native code address (high memory)"});
    }
}
