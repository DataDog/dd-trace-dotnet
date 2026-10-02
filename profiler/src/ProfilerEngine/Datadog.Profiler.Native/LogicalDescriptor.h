// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "CdacDescriptorTypes.h"

#include <map>
#include <set>
#include <string>
#include <vector>

class IMemoryReader;

namespace cdac
{
// The merged ("logical") data descriptor: the result of consuming the root in-memory descriptor and
// recursively merging every sub-descriptor (e.g. the active GC sub-descriptor). Later definitions
// overwrite earlier ones, per data_descriptor.md.
class LogicalDescriptor
{
public:
    std::map<std::string, TypeInfo> Types;
    std::map<std::string, GlobalValue> Globals;
    std::map<std::string, std::string> Contracts;
    int PointerSize = 8;
    bool IsLittleEndian = true;

    // Builds the logical descriptor from the root at rootAddress. Returns false if the root
    // descriptor itself cannot be read/validated (backend unavailable).
    bool Build(IMemoryReader& reader, uintptr_t rootAddress);

    // Re-attempts every pending (still-null) sub-descriptor slot. Returns true if at least one new
    // sub-descriptor was resolved and merged. Meaningful for live targets, where a slot can
    // transition null -> real-address after attach (e.g. the GC's once it initializes).
    bool Refresh();

    int PendingSubDescriptorCount() const { return static_cast<int>(_pendingSlots.size()); }

private:
    bool Merge(uintptr_t address, bool isRoot);
    void MergeType(const TypeInfo& incoming);
    bool ResolvePending();

    IMemoryReader* _reader = nullptr;
    std::set<uintptr_t> _visited;
    std::vector<SubDescriptorSlot> _pendingSlots;
};
} // namespace cdac
