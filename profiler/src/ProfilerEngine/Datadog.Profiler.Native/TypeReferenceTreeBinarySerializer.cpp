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
    std::vector<std::string_view> typeTable;
    uint32_t nextIndex = 0;

    // Phase 1: serialize roots + tree body into a temp buffer.
    // Types are discovered lazily during the DFS walk.
    std::vector<uint8_t> body;
    body.reserve(4096);

    for (const auto& [key, rootNode] : tree._roots)
    {
        auto [it, inserted] = typeToIndex.try_emplace(key.typeID, nextIndex);
        if (inserted)
        {
            std::string_view typeName;
            if (pFrameStore->GetTypeName(key.typeID, typeName))
            {
                typeTable.push_back(typeName);
            }
            else
            {
                typeTable.emplace_back("?");
            }
            nextIndex++;
        }

        WriteVarint(body, it->second);
        body.push_back(static_cast<uint8_t>(rootNode->category));

        WriteVarint(body, rootNode->node.instanceCount);
        WriteVarint(body, rootNode->node.totalSize);

        WriteString(body, rootNode->fieldName);

        WriteVarint(body, rootNode->node.children.size());
        for (const auto& [childTypeID, childNode] : rootNode->node.children)
        {
            WriteNode(*childNode, typeToIndex, typeTable, nextIndex, pFrameStore, body);
        }
    }

    // Phase 2: assemble header + string table + body
    std::vector<uint8_t> out;
    out.reserve(sizeof(Magic) + 16 + typeTable.size() * 32 + body.size());

    WriteBytes(out, Magic, sizeof(Magic));
    WriteVarint(out, FormatVersion);
    WriteVarint(out, typeTable.size());
    WriteVarint(out, tree._roots.size());

    for (const auto& name : typeTable)
    {
        WriteString(out, name);
    }

    out.insert(out.end(), body.begin(), body.end());

    auto endTime = OpSysTools::GetHighPrecisionTimestamp();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count();

    Log::Debug("Reference tree binary serialization completed: ", duration, "ms, ",
               out.size(), " bytes, ", typeTable.size(), " types, ",
               tree._roots.size(), " roots");

    return out;
}

void TypeReferenceTreeBinarySerializer::WriteNode(
    const TypeTreeNode& node,
    std::unordered_map<ClassID, uint32_t>& typeToIndex,
    std::vector<std::string_view>& typeTable,
    uint32_t& nextIndex,
    IFrameStore* pFrameStore,
    std::vector<uint8_t>& out)
{
    auto [it, inserted] = typeToIndex.try_emplace(node.typeID, nextIndex);
    if (inserted)
    {
        std::string_view typeName;
        if (pFrameStore->GetTypeName(node.typeID, typeName))
        {
            typeTable.push_back(typeName);
        }
        else
        {
            typeTable.emplace_back("?");
        }
        nextIndex++;
    }

    WriteVarint(out, it->second);
    WriteVarint(out, node.instanceCount);
    WriteVarint(out, node.totalSize);

    WriteVarint(out, node.children.size());
    for (const auto& [childTypeID, childNode] : node.children)
    {
        WriteNode(*childNode, typeToIndex, typeTable, nextIndex, pFrameStore, out);
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
