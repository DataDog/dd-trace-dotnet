// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TypeReferenceTreeBinarySerializer.h"
#include "Log.h"
#include "OpSysTools.h"

// stands for (D)ata(D)og (R)eference (T)ree
static constexpr uint8_t Magic[4] = {'D', 'D', 'R', 'T'};
static constexpr uint64_t FormatVersion = 1;

std::vector<uint8_t> TypeReferenceTreeBinarySerializer::Serialize(const TypeReferenceTree& tree, IFrameStore* pFrameStore)
{
    auto startTime = OpSysTools::GetHighPrecisionTimestamp();

    if (pFrameStore == nullptr)
    {
        return {};
    }

    // The string table is encoded as types are discovered so that no per-type string
    // is retained.
    StringTable types(pFrameStore);

    // Phase 1: serialize roots + tree body into a temp buffer.
    // Types are discovered lazily during the DFS walk.
    std::vector<uint8_t> body;
    body.reserve(4096);

    for (const auto& [key, rootNode] : tree._roots)
    {
        auto typeIndex = RegisterType(types, key.typeID);

        WriteVarint(body, typeIndex);
        body.push_back(static_cast<uint8_t>(rootNode->category));

        WriteVarint(body, rootNode->node.instanceCount);
        WriteVarint(body, rootNode->node.totalSize);

        WriteString(body, rootNode->fieldName);

        WriteVarint(body, rootNode->node.children.size());
        for (const auto& [childTypeID, childNode] : rootNode->node.children)
        {
            WriteNode(*childNode, types, body);
        }
    }

    // Phase 2: assemble header + string table + body
    std::vector<uint8_t> out;
    out.reserve(sizeof(Magic) + 16 + types.bytes.size() + body.size());

    WriteBytes(out, Magic, sizeof(Magic));
    WriteVarint(out, FormatVersion);
    WriteVarint(out, types.count);
    WriteVarint(out, tree._roots.size());

    out.insert(out.end(), types.bytes.begin(), types.bytes.end());
    out.insert(out.end(), body.begin(), body.end());

    auto endTime = OpSysTools::GetHighPrecisionTimestamp();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count();

    Log::Debug("Reference tree binary serialization completed: ", duration, "ms, ",
               out.size(), " bytes, ", types.count, " types, ",
               tree._roots.size(), " roots");

    return out;
}

TypeReferenceTreeBinarySerializer::StringTable::StringTable(IFrameStore* frameStore) :
    pFrameStore(frameStore)
{
    bytes.reserve(4096);
}

uint32_t TypeReferenceTreeBinarySerializer::RegisterType(StringTable& types, ClassID typeID)
{
    auto [it, inserted] = types.typeToIndex.try_emplace(typeID, types.count);
    if (inserted)
    {
        // The std::string overload returns the namespace qualified name, so the type
        // names here match the ones emitted in the class histogram. It builds that name
        // into a temporary and move-assigns it, so scratch saves declaring a local per
        // call but its buffer is replaced on every resolution.
        if (types.pFrameStore->GetTypeName(typeID, types.scratch))
        {
            WriteString(types.bytes, types.scratch);
        }
        else
        {
            WriteString(types.bytes, "?");
        }
        types.count++;
    }

    return it->second;
}

void TypeReferenceTreeBinarySerializer::WriteNode(const TypeTreeNode& node, StringTable& types, std::vector<uint8_t>& out)
{
    auto typeIndex = RegisterType(types, node.typeID);

    WriteVarint(out, typeIndex);
    WriteVarint(out, node.instanceCount);
    WriteVarint(out, node.totalSize);

    WriteVarint(out, node.children.size());
    for (const auto& [childTypeID, childNode] : node.children)
    {
        WriteNode(*childNode, types, out);
    }
}

void TypeReferenceTreeBinarySerializer::WriteVarint(std::vector<uint8_t>& out, uint64_t value)
{
    do
    {
        uint8_t byte = static_cast<uint8_t>(value & 0x7F);
        value >>= 7;
        if (value != 0)
        {
            byte |= 0x80;
        }
        out.push_back(byte);
    } while (value != 0);
}

void TypeReferenceTreeBinarySerializer::WriteBytes(std::vector<uint8_t>& out, const uint8_t* data, size_t len)
{
    out.insert(out.end(), data, data + len);
}

void TypeReferenceTreeBinarySerializer::WriteString(std::vector<uint8_t>& out, std::string_view str)
{
    WriteVarint(out, str.size());
    out.insert(
        out.end(),
        reinterpret_cast<const uint8_t*>(str.data()),
        reinterpret_cast<const uint8_t*>(str.data()) + str.size()
    );
}
