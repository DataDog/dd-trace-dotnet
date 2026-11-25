// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "ContractDescriptorReader.h"

#include "IMemoryReader.h"

#include <cstring>

namespace cdac
{
namespace
{
// The magic is a uint64 stored in the target's endianness. On a little-endian target its bytes
// spell "DNCCDAC\0"; on big-endian they appear reversed. .NET only supports little-endian targets,
// and we self-inspect, so all multi-byte reads below decode as little-endian (native).
constexpr uint8_t MagicLittleEndian[8] = {0x44, 0x4E, 0x43, 0x43, 0x44, 0x41, 0x43, 0x00}; // "DNCCDAC\0"
constexpr uint8_t MagicBigEndian[8] = {0x00, 0x43, 0x41, 0x44, 0x43, 0x43, 0x4E, 0x44};

bool TryReadUInt32(IMemoryReader& reader, uintptr_t address, uint32_t& value)
{
    return reader.Read(address, value);
}
} // namespace

bool ContractDescriptorReader::TryRead(IMemoryReader& reader, uintptr_t address, RawContractDescriptor& raw)
{
    // Field offsets (see the struct above). 'descriptor' sits at a fixed +0x10; everything after it
    // shifts by (8 - ptrSize) on a 32-bit target, so we compute those positions from the measured
    // pointer size. pointer_data follows the 4-byte count + 4-byte pad0 (a fixed +8 on both widths).
    uint8_t magic[8] = {};
    if (!reader.ReadMemory(address, magic, sizeof(magic)))
    {
        return false;
    }

    bool isLittleEndian;
    if (memcmp(magic, MagicLittleEndian, 8) == 0)
    {
        isLittleEndian = true;
    }
    else if (memcmp(magic, MagicBigEndian, 8) == 0)
    {
        isLittleEndian = false;
    }
    else
    {
        return false;
    }

    uint32_t flags = 0;
    if (!TryReadUInt32(reader, address + 8, flags))
    {
        return false;
    }
    bool is32Bit = (flags & 0x2) != 0;
    int pointerSize = is32Bit ? 4 : 8;

    uint32_t descriptorSize = 0;
    if (!TryReadUInt32(reader, address + 12, descriptorSize))
    {
        return false;
    }

    uintptr_t descriptorPtr = 0;
    if (!reader.ReadPointer(address + 16, descriptorPtr))
    {
        return false;
    }

    uintptr_t afterDescriptorPtr = address + 16 + static_cast<uintptr_t>(pointerSize);
    uint32_t pointerDataCount = 0;
    if (!TryReadUInt32(reader, afterDescriptorPtr, pointerDataCount))
    {
        return false;
    }

    uintptr_t pointerDataPtr = 0;
    if (!reader.ReadPointer(afterDescriptorPtr + 8, pointerDataPtr))
    {
        return false;
    }

    // Defensive upper bounds: a sane descriptor blob is a few tens of KB, and pointer_data holds a
    // few hundred entries. Reject absurd values rather than allocating huge buffers from a bad read.
    constexpr uint32_t MaxDescriptorSize = 16 * 1024 * 1024;
    constexpr uint32_t MaxPointerDataCount = 1024 * 1024;
    if (descriptorSize > MaxDescriptorSize || pointerDataCount > MaxPointerDataCount)
    {
        return false;
    }

    std::string json;
    if (descriptorSize != 0)
    {
        json.resize(descriptorSize);
        if (!reader.ReadMemory(descriptorPtr, reinterpret_cast<uint8_t*>(&json[0]), descriptorSize))
        {
            return false;
        }
    }

    std::vector<uintptr_t> pointerData(pointerDataCount, 0);
    for (uint32_t i = 0; i < pointerDataCount; i++)
    {
        if (!reader.ReadPointer(pointerDataPtr + static_cast<uintptr_t>(i) * static_cast<uintptr_t>(pointerSize), pointerData[i]))
        {
            return false;
        }
    }

    raw.Address = address;
    raw.PointerSize = pointerSize;
    raw.Json = std::move(json);
    raw.PointerData = std::move(pointerData);
    raw.IsLittleEndian = isLittleEndian;
    return true;
}
} // namespace cdac
