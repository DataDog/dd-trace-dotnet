// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "IMemoryReader.h"

// In-process implementation of IMemoryReader. Reads the current process' own memory with a fault
// guard (SEH on Windows, SIGSEGV/SIGBUS + siglongjmp on Linux) so that following a pointer into an
// unmapped/guard page returns false instead of killing the process. This is the same machinery used
// by ReferenceChainTraverser.
//
// macOS is intentionally not supported (the profiler does not build on macOS today).
class InProcessMemoryReader : public IMemoryReader
{
public:
    InProcessMemoryReader();

    int PointerSize() const override;
    bool ReadMemory(uintptr_t address, uint8_t* buffer, size_t size) override;
};
