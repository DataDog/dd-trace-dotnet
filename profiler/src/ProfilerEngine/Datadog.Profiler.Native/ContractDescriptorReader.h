// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CdacDescriptorTypes.h"

class IMemoryReader;

namespace cdac
{
// Reads and validates the DotNetRuntimeContractDescriptor struct (see contract-descriptor.md):
//
//   struct DotNetRuntimeContractDescriptor {
//       uint64_t   magic;              // "DNCCDAC\0" in target endianness
//       uint32_t   flags;              // bit0 = 1, bit1 = ptrSize (0 => 64-bit, 1 => 32-bit)
//       uint32_t   descriptor_size;
//       const char *descriptor;        // UTF-8 JSON (length = descriptor_size, NOT NUL-terminated)
//       uint32_t   pointer_data_count;
//       uint32_t   pad0;
//       uintptr_t  *pointer_data;
//   };
class ContractDescriptorReader
{
public:
    // Non-throwing: returns false when the memory cannot be read or the magic does not validate.
    // Used for both the root (failure => backend unavailable) and sub-descriptors (slots may point
    // at not-yet-populated / unmapped memory on a live target).
    static bool TryRead(IMemoryReader& reader, uintptr_t address, RawContractDescriptor& raw);
};
} // namespace cdac
