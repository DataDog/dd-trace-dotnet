// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "LogicalDescriptor.h"

#include "ContractDescriptorParser.h"
#include "ContractDescriptorReader.h"
#include "IMemoryReader.h"

namespace cdac
{
bool LogicalDescriptor::Build(IMemoryReader& reader, uintptr_t rootAddress)
{
    _reader = &reader;
    if (!Merge(rootAddress, /*isRoot*/ true))
    {
        return false;
    }
    ResolvePending();
    return true;
}

bool LogicalDescriptor::Refresh()
{
    return ResolvePending();
}

bool LogicalDescriptor::ResolvePending()
{
    if (_reader == nullptr)
    {
        return false;
    }

    bool mergedAny = false;
    bool resolvedThisPass;
    // Re-run until a full pass resolves no slot: merging one sub-descriptor can enqueue further slots
    // (its own sub-descriptors), mirroring the runtime's do/while loop. Progress is tracked by whether
    // a slot was resolved this pass, not by the pending-list size: a pass that erases one slot and
    // enqueues another (e.g. a sub-descriptor cycle) keeps the size constant yet still made progress.
    do
    {
        resolvedThisPass = false;
        for (int i = static_cast<int>(_pendingSlots.size()) - 1; i >= 0; i--)
        {
            SubDescriptorSlot slot = _pendingSlots[static_cast<size_t>(i)];

            // The first indirection already happened (slot == pointer_data[index]); here we read the
            // pointer stored AT the slot to get the actual sub-descriptor address. A null/unreadable
            // slot means "not populated yet" - leave it pending for a later Refresh().
            uintptr_t subDescriptorAddress = 0;
            if (slot.Slot == 0 || !_reader->ReadPointer(static_cast<uintptr_t>(slot.Slot), subDescriptorAddress) || subDescriptorAddress == 0)
            {
                continue;
            }

            _pendingSlots.erase(_pendingSlots.begin() + i);
            // The slot is resolved once its pointer is read, even if the target was already visited
            // (a cycle), so Merge returning false must not stop the loop.
            resolvedThisPass = true;

            if (Merge(subDescriptorAddress, /*isRoot*/ false))
            {
                mergedAny = true;
            }
        }
    } while (resolvedThisPass);

    return mergedAny;
}

bool LogicalDescriptor::Merge(uintptr_t address, bool isRoot)
{
    if (address == 0 || _visited.find(address) != _visited.end())
    {
        return false;
    }

    RawContractDescriptor raw;
    if (!ContractDescriptorReader::TryRead(*_reader, address, raw))
    {
        // Root failure is fatal; sub-descriptor failure is best-effort (skip).
        return false;
    }

    _visited.insert(address);

    PointerSize = raw.PointerSize;
    IsLittleEndian = raw.IsLittleEndian;

    ParsedDescriptor parsed = ContractDescriptorParser::Parse(raw);

    for (const auto& type : parsed.Types)
    {
        MergeType(type.second);
    }

    for (const auto& global : parsed.Globals)
    {
        Globals[global.first] = global.second;
    }

    for (const auto& contract : parsed.Contracts)
    {
        Contracts[contract.first] = contract.second;
    }

    for (const auto& slot : parsed.SubDescriptorSlots)
    {
        _pendingSlots.push_back(slot);
    }

    (void)isRoot;
    return true;
}

void LogicalDescriptor::MergeType(const TypeInfo& incoming)
{
    auto it = Types.find(incoming.Name);
    if (it == Types.end())
    {
        Types[incoming.Name] = incoming;
        return;
    }

    TypeInfo& existing = it->second;
    if (incoming.Size.has_value())
    {
        existing.Size = incoming.Size;
    }
    for (const auto& field : incoming.Fields)
    {
        existing.Fields[field.first] = field.second;
    }
}
} // namespace cdac
