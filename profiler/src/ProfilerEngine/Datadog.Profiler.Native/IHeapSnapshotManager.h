// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>
#include <string>
#include <utility>
#include <vector>

#include "IMemoryFootprintProvider.h"

// Bitfield values for DD_INTERNAL_PROFILING_HEAPSNAPSHOT_REFERENCE_TREE_FORMAT
constexpr uint32_t ReferenceTreeFormat_Binary = 1;  // bit 0
constexpr uint32_t ReferenceTreeFormat_Json   = 2;  // bit 1

class IHeapSnapshotManager : public IMemoryFootprintProvider
{
public:
    using FileEntry = std::pair<std::string, std::vector<uint8_t>>;

    virtual ~IHeapSnapshotManager() = default;

    virtual void SetRuntimeSessionParameters(uint64_t keywords, uint32_t verbosity) = 0;
    virtual std::string GetAndClearHeapSnapshotText() = 0;
    virtual std::vector<FileEntry> GetAndClearReferenceTreeContent() = 0;
};