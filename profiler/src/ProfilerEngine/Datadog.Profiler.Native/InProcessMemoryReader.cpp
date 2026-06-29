// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "InProcessMemoryReader.h"

#include <cstring>

#ifdef _WINDOWS
// Provides the SEH machinery (__try/__except) and the EXCEPTION_EXECUTE_HANDLER macro.
#include <windows.h>
#else
#include <csetjmp>
#include <csignal>

#include "ProfilerSignalManager.h"

// See ReferenceChainTraverser.cpp for the rationale behind this guard and the macOS caveat.
// The TLS recovery machinery is identical; we keep a separate copy so the two readers are
// independent translation units that each register the same fault handler with the shared
// ProfilerSignalManager (registration is idempotent).
namespace
{
thread_local sigjmp_buf t_memReaderJmpBuf;
thread_local volatile sig_atomic_t t_inGuardedRead = 0;

bool MemoryReadFaultHandler(int /*signal*/, siginfo_t* /*info*/, void* /*context*/)
{
    if (t_inGuardedRead != 0)
    {
        siglongjmp(t_memReaderJmpBuf, 1);
    }
    return false;
}
} // namespace
#endif

InProcessMemoryReader::InProcessMemoryReader()
{
#ifndef _WINDOWS
    auto* segv = ProfilerSignalManager::Get(SIGSEGV);
    if (segv != nullptr)
    {
        segv->RegisterHandler(&MemoryReadFaultHandler);
    }
    auto* bus = ProfilerSignalManager::Get(SIGBUS);
    if (bus != nullptr)
    {
        bus->RegisterHandler(&MemoryReadFaultHandler);
    }
#endif
}

int InProcessMemoryReader::PointerSize() const
{
    return static_cast<int>(sizeof(void*));
}

bool InProcessMemoryReader::ReadMemory(uintptr_t address, uint8_t* buffer, size_t size)
{
    if (address == 0 || buffer == nullptr)
    {
        return false;
    }

    if (size == 0)
    {
        return true;
    }

#ifdef _WINDOWS
    __try
    {
        memcpy(buffer, reinterpret_cast<const void*>(address), size);
        return true;
    }
    __except (EXCEPTION_EXECUTE_HANDLER)
    {
        return false;
    }
#else
    if (sigsetjmp(t_memReaderJmpBuf, 1) == 0)
    {
        t_inGuardedRead = 1;
        memcpy(buffer, reinterpret_cast<const void*>(address), size);
        t_inGuardedRead = 0;
        return true;
    }

    t_inGuardedRead = 0;
    return false;
#endif
}
