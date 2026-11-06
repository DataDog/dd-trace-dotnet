// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "DacInterface.h"

#include "IRuntimeInfo.h"
#include "LiveDataTarget.h"
#include "Log.h"

#include <string>

#ifdef _WINDOWS
#include <windows.h>
#else
#include <dlfcn.h>
#include <cstdio>
#include <cstring>
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

// sospriv.h (generated from sospriv.idl) refers to T_CONTEXT, but its in-file definition is disabled
// (#if 0): coreclr normally supplies it via crosscomp.h (#define T_CONTEXT CONTEXT). We only ever pass
// it as an opaque pointer in an interface slot we never call, so alias it to the platform CONTEXT to
// match coreclr. CONTEXT comes from <windows.h> (Windows) / the PAL (Unix).
#ifndef T_CONTEXT
#define T_CONTEXT CONTEXT
#endif

// The DAC/coreclr-internal headers are isolated to this translation unit (and LiveDataTarget.cpp)
// so they never leak into shared headers.
#include "clrdata.h"
#include "sospriv.h"
#include "xclrdata.h"

namespace
{
using PFN_CLRDataCreateInstance = HRESULT(STDAPICALLTYPE*)(REFIID iid, ICLRDataTarget* target, void** iface);

struct RuntimeModule
{
    uint64_t Base = 0;
    std::string Directory; // includes trailing separator
};

#ifdef _WINDOWS
constexpr const char PathSeparator = '\\';

std::string GetDirectory(const std::string& path)
{
    auto pos = path.find_last_of("\\/");
    if (pos == std::string::npos)
    {
        return std::string{};
    }
    return path.substr(0, pos + 1);
}

bool TryGetRuntimeModule(bool isDotnetFramework, RuntimeModule& result)
{
    HMODULE runtime = nullptr;
    if (isDotnetFramework)
    {
        runtime = GetModuleHandleA("clr.dll");
        if (runtime == nullptr)
        {
            runtime = GetModuleHandleA("clr");
        }
    }
    else
    {
        runtime = GetModuleHandleA("coreclr.dll");
        if (runtime == nullptr)
        {
            runtime = GetModuleHandleA("coreclr");
        }
    }

    if (runtime == nullptr)
    {
        return false;
    }

    char path[MAX_PATH] = {};
    DWORD length = GetModuleFileNameA(runtime, path, static_cast<DWORD>(sizeof(path)));
    if (length == 0 || length >= sizeof(path))
    {
        return false;
    }

    result.Base = reinterpret_cast<uint64_t>(runtime);
    result.Directory = GetDirectory(std::string(path, length));
    return !result.Directory.empty();
}

void* LoadDac(const std::string& fullPath)
{
    return reinterpret_cast<void*>(LoadLibraryA(fullPath.c_str()));
}

void* GetDacExport(void* module, const char* name)
{
    return reinterpret_cast<void*>(GetProcAddress(reinterpret_cast<HMODULE>(module), name));
}

void UnloadDac(void* module)
{
    FreeLibrary(reinterpret_cast<HMODULE>(module));
}
#else
constexpr const char PathSeparator = '/';

std::string GetDirectory(const std::string& path)
{
    auto pos = path.find_last_of('/');
    if (pos == std::string::npos)
    {
        return std::string{};
    }
    return path.substr(0, pos + 1);
}

// Parse /proc/self/maps to find the load base and on-disk path of libcoreclr.so. The base is the
// start of its first (lowest-address) mapping.
bool TryGetRuntimeModule(bool /*isDotnetFramework*/, RuntimeModule& result)
{
    FILE* maps = fopen("/proc/self/maps", "r");
    if (maps == nullptr)
    {
        return false;
    }

    bool found = false;
    char line[4096];
    while (fgets(line, sizeof(line), maps) != nullptr)
    {
        const char* slash = strchr(line, '/');
        if (slash == nullptr)
        {
            continue;
        }
        if (strstr(slash, "libcoreclr.so") == nullptr)
        {
            continue;
        }

        unsigned long long start = 0;
        if (sscanf(line, "%llx", &start) != 1)
        {
            continue;
        }

        std::string path(slash);
        if (!path.empty() && path.back() == '\n')
        {
            path.pop_back();
        }

        result.Base = static_cast<uint64_t>(start);
        result.Directory = GetDirectory(path);
        found = !result.Directory.empty();
        break;
    }

    fclose(maps);
    return found;
}

void* LoadDac(const std::string& fullPath)
{
    return dlopen(fullPath.c_str(), RTLD_NOW | RTLD_LOCAL);
}

void* GetDacExport(void* module, const char* name)
{
    return dlsym(module, name);
}

void UnloadDac(void* module)
{
    dlclose(module);
}
#endif

std::string GetDacFileName(bool isDotnetFramework)
{
#ifdef _WINDOWS
    return isDotnetFramework ? "mscordacwks.dll" : "mscordaccore.dll";
#else
    // .NET Framework does not run on non-Windows, so only the modern-.NET DAC applies here.
    (void)isDotnetFramework;
    return "libmscordaccore.so";
#endif
}
} // namespace

DacInterface::~DacInterface()
{
    Reset();
}

void DacInterface::Reset()
{
    if (_sos != nullptr)
    {
        _sos->Release();
        _sos = nullptr;
    }
    if (_process != nullptr)
    {
        _process->Release();
        _process = nullptr;
    }
    if (_dataTarget != nullptr)
    {
        _dataTarget->Release();
        _dataTarget = nullptr;
    }
    if (_dacModule != nullptr)
    {
        UnloadDac(_dacModule);
        _dacModule = nullptr;
    }
}

bool DacInterface::TryLoad(IRuntimeInfo* pRuntimeInfo)
{
    const bool isDotnetFramework = (pRuntimeInfo != nullptr) && pRuntimeInfo->IsDotnetFramework();

    RuntimeModule runtime;
    if (!TryGetRuntimeModule(isDotnetFramework, runtime))
    {
        Log::Debug("!eeheap (dac): unable to locate the runtime module; DAC backend unavailable.");
        return false;
    }

    std::string dacPath = runtime.Directory + GetDacFileName(isDotnetFramework);

    _dacModule = LoadDac(dacPath);
    if (_dacModule == nullptr)
    {
        Log::Debug("!eeheap (dac): failed to load the DAC at '", dacPath, "'; DAC backend unavailable.");
        return false;
    }

    auto createInstance = reinterpret_cast<PFN_CLRDataCreateInstance>(
        GetDacExport(_dacModule, "CLRDataCreateInstance"));
    if (createInstance == nullptr)
    {
        Log::Debug("!eeheap (dac): CLRDataCreateInstance not found in the DAC; DAC backend unavailable.");
        Reset();
        return false;
    }

    _dataTarget = dac::CreateLiveDataTarget(runtime.Base);
    if (_dataTarget == nullptr)
    {
        Log::Debug("!eeheap (dac): failed to create the live data target; DAC backend unavailable.");
        Reset();
        return false;
    }

    HRESULT hr = createInstance(__uuidof(IXCLRDataProcess), _dataTarget, reinterpret_cast<void**>(&_process));
    if (FAILED(hr) || _process == nullptr)
    {
        Log::Debug("!eeheap (dac): CLRDataCreateInstance failed (hr=0x", std::hex, hr, std::dec,
                   "); the DAC likely does not match the runtime. DAC backend unavailable.");
        Reset();
        return false;
    }

    hr = _process->QueryInterface(__uuidof(ISOSDacInterface), reinterpret_cast<void**>(&_sos));
    if (FAILED(hr) || _sos == nullptr)
    {
        Log::Debug("!eeheap (dac): QueryInterface(ISOSDacInterface) failed (hr=0x", std::hex, hr, std::dec,
                   "); DAC backend unavailable.");
        Reset();
        return false;
    }

    Log::Info("!eeheap (dac): loaded DAC '", dacPath, "'.");
    return true;
}

void DacInterface::Flush()
{
    if (_process != nullptr)
    {
        // Best-effort: invalidate the DAC's cache of the live target. Ignore failures.
        _process->Flush();
    }
}
