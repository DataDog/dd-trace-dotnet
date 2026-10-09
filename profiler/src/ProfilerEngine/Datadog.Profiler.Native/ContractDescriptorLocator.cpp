// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ContractDescriptorLocator.h"

#ifdef _WINDOWS
#include <windows.h>
#else
#include <dlfcn.h>
#endif

namespace cdac
{
namespace
{
constexpr const char* DescriptorSymbol = "DotNetRuntimeContractDescriptor";
} // namespace

bool ContractDescriptorLocator::TryLocate(uintptr_t& address)
{
    address = 0;

#ifdef _WINDOWS
    HMODULE coreclr = GetModuleHandleA("coreclr");
    if (coreclr == nullptr)
    {
        coreclr = GetModuleHandleA("coreclr.dll");
    }
    if (coreclr == nullptr)
    {
        return false;
    }

    FARPROC proc = GetProcAddress(coreclr, DescriptorSymbol);
    if (proc == nullptr)
    {
        return false;
    }
    address = reinterpret_cast<uintptr_t>(proc);
    return true;
#else
    // Prefer the already-loaded libcoreclr.so; fall back to a global lookup.
    void* handle = dlopen("libcoreclr.so", RTLD_NOLOAD | RTLD_NOW);
    void* sym = nullptr;
    if (handle != nullptr)
    {
        sym = dlsym(handle, DescriptorSymbol);
        dlclose(handle);
    }
    if (sym == nullptr)
    {
        sym = dlsym(RTLD_DEFAULT, DescriptorSymbol);
    }
    if (sym == nullptr)
    {
        return false;
    }
    address = reinterpret_cast<uintptr_t>(sym);
    return true;
#endif
}
} // namespace cdac
