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

    std::unordered_map<ClassID, uint32_t> typeToIndex;

    // The string table is encoded as types are discovered so that no per-type string
    // is retained: the same scratch buffer is reused for every name resolution.
    std::vector<uint8_t> stringTable;
    stringTable.reserve(4096);
    uint32_t typeCount = 0;
    std::string scratch;

    // Phase 1: serialize roots + tree body into a temp buffer.
    // Types are discovered lazily during the DFS walk.
    std::vector<uint8_t> body;
    body.reserve(4096);

    for (const auto& [key, rootNode] : tree._roots)
    {
        auto typeIndex = RegisterType(key.typeID, typeToIndex, stringTable, typeCount, scratch, pFrameStore);

        WriteVarint(body, typeIndex);
        body.push_back(static_cast<uint8_t>(rootNode->category));

        WriteVarint(body, rootNode->node.instanceCount);
        WriteVarint(body, rootNode->node.totalSize);

        WriteString(body, rootNode->fieldName);

        WriteVarint(body, rootNode->node.children.size());
        for (const auto& [childTypeID, childNode] : rootNode->node.children)
        {
            WriteNode(*childNode, typeToIndex, stringTable, typeCount, scratch, pFrameStore, body);
        }
    }

    // Phase 2: assemble header + string table + body
    std::vector<uint8_t> out;
    out.reserve(sizeof(Magic) + 16 + stringTable.size() + body.size());

    WriteBytes(out, Magic, sizeof(Magic));
    WriteVarint(out, FormatVersion);
    WriteVarint(out, typeCount);
    WriteVarint(out, tree._roots.size());

    out.insert(out.end(), stringTable.begin(), stringTable.end());
    out.insert(out.end(), body.begin(), body.end());

    auto endTime = OpSysTools::GetHighPrecisionTimestamp();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count();

    Log::Debug("Reference tree binary serialization completed: ", duration, "ms, ",
               out.size(), " bytes, ", typeCount, " types, ",
               tree._roots.size(), " roots");

    return out;
}

uint32_t TypeReferenceTreeBinarySerializer::RegisterType(
    ClassID typeID,
    std::unordered_map<ClassID, uint32_t>& typeToIndex,
    std::vector<uint8_t>& stringTable,
    uint32_t& typeCount,
    std::string& scratch,
    IFrameStore* pFrameStore)
{
    auto [it, inserted] = typeToIndex.try_emplace(typeID, typeCount);
    if (inserted)
    {
        // The std::string overload returns the namespace qualified name, so the type
        // names here match the ones emitted in the class histogram.
        if (pFrameStore->GetTypeName(typeID, scratch))
        {
            WriteString(stringTable, scratch);
        }
        else
        {
            WriteString(stringTable, "?");
        }
        typeCount++;
    }

    return it->second;
}

void TypeReferenceTreeBinarySerializer::WriteNode(
    const TypeTreeNode& node,
    std::unordered_map<ClassID, uint32_t>& typeToIndex,
    std::vector<uint8_t>& stringTable,
    uint32_t& typeCount,
    std::string& scratch,
    IFrameStore* pFrameStore,
    std::vector<uint8_t>& out)
{
    auto typeIndex = RegisterType(node.typeID, typeToIndex, stringTable, typeCount, scratch, pFrameStore);

    WriteVarint(out, typeIndex);
    WriteVarint(out, node.instanceCount);
    WriteVarint(out, node.totalSize);

    WriteVarint(out, node.children.size());
    for (const auto& [childTypeID, childNode] : node.children)
    {
        WriteNode(*childNode, typeToIndex, stringTable, typeCount, scratch, pFrameStore, out);
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
