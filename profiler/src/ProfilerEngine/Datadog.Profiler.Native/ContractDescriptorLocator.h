// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

namespace cdac
{
// Locates the runtime's exported DotNetRuntimeContractDescriptor symbol (the cDAC bootstrap header).
// Self/in-process only: uses the OS loader (GetProcAddress on Windows, dlsym on Linux). The address
// returned is the address of the descriptor struct itself.
class ContractDescriptorLocator
{
public:
    // Returns true and sets address when the symbol is found in the loaded coreclr module.
    static bool TryLocate(uintptr_t& address);
};
} // namespace cdac
