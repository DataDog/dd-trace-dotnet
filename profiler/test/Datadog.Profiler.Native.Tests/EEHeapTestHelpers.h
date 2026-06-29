// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "ClrNativeHeapInfo.h"
#include "IMemoryReader.h"
#include "INativeHeapEnumerator.h"

#include <cstring>
#include <string>
#include <vector>

// A simple region-backed IMemoryReader for the cDAC unit tests. Memory is modeled as a set of
// disjoint regions; a read fully contained in one region succeeds, anything else returns false
// (so an unmapped/out-of-range pointer degrades to "skip", like the real InProcessMemoryReader).
class FakeMemoryReader : public IMemoryReader
{
public:
    explicit FakeMemoryReader(int pointerSize = 8) :
        _pointerSize(pointerSize)
    {
    }

    int PointerSize() const override
    {
        return _pointerSize;
    }

    bool ReadMemory(uintptr_t address, uint8_t* buffer, size_t size) override
    {
        for (const auto& region : _regions)
        {
            if (address >= region.Start && (address + size) <= (region.Start + region.Data.size()))
            {
                if (size != 0)
                {
                    std::memcpy(buffer, region.Data.data() + (address - region.Start), size);
                }
                return true;
            }
        }
        return false;
    }

    void AddRegion(uintptr_t start, std::vector<uint8_t> data)
    {
        _regions.push_back({start, std::move(data)});
    }

    // Writes a pointer-sized value into a fresh region at the given address.
    void AddPointerAt(uintptr_t address, uintptr_t value)
    {
        std::vector<uint8_t> bytes(static_cast<size_t>(_pointerSize), 0);
        for (int i = 0; i < _pointerSize; i++)
        {
            bytes[static_cast<size_t>(i)] = static_cast<uint8_t>((static_cast<uint64_t>(value) >> (8 * i)) & 0xFF);
        }
        AddRegion(address, std::move(bytes));
    }

private:
    struct Region
    {
        uintptr_t Start;
        std::vector<uint8_t> Data;
    };

    int _pointerSize;
    std::vector<Region> _regions;
};

// Builds a valid in-memory DotNetRuntimeContractDescriptor blob (compact "DNCCDAC\0" format) plus
// its JSON and pointer_data regions, and installs them into a FakeMemoryReader. Returns the root
// address to pass to LogicalDescriptor::Build.
inline uintptr_t InstallDescriptor(
    FakeMemoryReader& reader,
    const std::string& json,
    const std::vector<uintptr_t>& pointerData,
    uintptr_t rootAddress = 0x100000,
    uintptr_t jsonAddress = 0x200000,
    uintptr_t pointerDataAddress = 0x300000,
    bool corruptMagic = false)
{
    constexpr int pointerSize = 8;

    auto appendUInt32 = [](std::vector<uint8_t>& out, uint32_t value) {
        for (int i = 0; i < 4; i++)
        {
            out.push_back(static_cast<uint8_t>((value >> (8 * i)) & 0xFF));
        }
    };
    auto appendPointer = [](std::vector<uint8_t>& out, uint64_t value) {
        for (int i = 0; i < pointerSize; i++)
        {
            out.push_back(static_cast<uint8_t>((value >> (8 * i)) & 0xFF));
        }
    };

    std::vector<uint8_t> header;
    // magic: "DNCCDAC\0"
    const uint8_t magic[8] = {0x44, 0x4E, 0x43, 0x43, 0x44, 0x41, 0x43, 0x00};
    for (uint8_t b : magic)
    {
        header.push_back(corruptMagic ? static_cast<uint8_t>(b ^ 0xFF) : b);
    }
    appendUInt32(header, 0x1);                                  // flags: bit1 (0x2) clear -> 64-bit
    appendUInt32(header, static_cast<uint32_t>(json.size()));   // descriptor (JSON) size
    appendPointer(header, jsonAddress);                        // descriptor pointer
    appendUInt32(header, static_cast<uint32_t>(pointerData.size())); // pointer_data count
    appendUInt32(header, 0);                                    // pad
    appendPointer(header, pointerData.empty() ? 0 : pointerDataAddress); // pointer_data pointer

    reader.AddRegion(rootAddress, std::move(header));

    std::vector<uint8_t> jsonBytes(json.begin(), json.end());
    reader.AddRegion(jsonAddress, std::move(jsonBytes));

    if (!pointerData.empty())
    {
        std::vector<uint8_t> pdBytes;
        for (uintptr_t p : pointerData)
        {
            appendPointer(pdBytes, static_cast<uint64_t>(p));
        }
        reader.AddRegion(pointerDataAddress, std::move(pdBytes));
    }

    return rootAddress;
}

// A fully-canned INativeHeapEnumerator for reporter tests.
class FakeNativeHeapEnumerator : public INativeHeapEnumerator
{
public:
    FakeNativeHeapEnumerator(std::vector<ClrNativeHeapInfo> heaps, bool available) :
        _heaps(std::move(heaps)), _available(available)
    {
    }

    std::vector<ClrNativeHeapInfo> EnumerateAll() override
    {
        _enumerateCount++;
        return _heaps;
    }

    bool IsAvailable() const override
    {
        return _available;
    }

    int EnumerateCount() const
    {
        return _enumerateCount;
    }

private:
    std::vector<ClrNativeHeapInfo> _heaps;
    bool _available;
    int _enumerateCount = 0;
};
