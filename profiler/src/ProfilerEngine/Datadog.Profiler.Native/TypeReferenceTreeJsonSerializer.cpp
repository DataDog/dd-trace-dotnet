// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TypeReferenceTreeJsonSerializer.h"
#include "Log.h"
#include "OpSysTools.h"
#include <cstdio>

std::string TypeReferenceTreeJsonSerializer::Serialize(const TypeReferenceTree& tree, IFrameStore* pFrameStore)
{
    auto startTime = OpSysTools::GetHighPrecisionTimestamp();

    if (pFrameStore == nullptr)
    {
        return "{}";
    }

    // B4: Single-pass -- types are collected lazily during OutputNode.
    // The type table is emitted after the tree (roots first, then type table).
    // Entries are escaped as types are discovered so that no per-type string is retained.
    TypeTable types(pFrameStore);

    // B1: Use std::string buffer instead of std::stringstream.
    std::string out;
    out.reserve(4096);

    // Phase 1: emit roots (types are collected lazily via OutputNode)
    std::string rootsJson;
    rootsJson.reserve(2048);
    rootsJson += '[';

    bool firstRoot = true;
    for (const auto& [key, rootNode] : tree._roots)
    {
        // Lazily register root type
        auto typeIndex = RegisterType(types, key.typeID);

        if (!firstRoot)
        {
            rootsJson += ',';
        }
        firstRoot = false;

        rootsJson += "{\"t\":";
        AppendUInt32(rootsJson, typeIndex);

        const char* categoryCode = GetRootCategoryCode(rootNode->category);
        rootsJson += ",\"c\":\"";
        rootsJson += categoryCode;
        rootsJson += '"';

        rootsJson += ",\"ic\":";
        AppendUInt64(rootsJson, rootNode->node.instanceCount);

        rootsJson += ",\"ts\":";
        AppendUInt64(rootsJson, rootNode->node.totalSize);

        if (!rootNode->fieldName.empty())
        {
            rootsJson += ",\"fn\":\"";
            AppendEscapedJson(rootsJson, rootNode->fieldName);
            rootsJson += '"';
        }

        if (!rootNode->node.children.empty())
        {
            rootsJson += ",\"ch\":[";
            bool firstChild = true;
            for (const auto& [childTypeID, childNode] : rootNode->node.children)
            {
                if (!firstChild)
                {
                    rootsJson += ',';
                }
                firstChild = false;
                OutputNode(*childNode, types, rootsJson);
            }
            rootsJson += ']';
        }
        rootsJson += '}';
    }

    rootsJson += ']';

    // Phase 2: assemble final JSON -- type table first (now fully populated), then roots
    out += "{\"v\":1";

    if (types.count > 0)
    {
        out += ",\"tt\":[";
        out += types.json;
        out += ']';
    }

    out += ",\"r\":";
    out += rootsJson;
    out += '}';

    auto endTime = OpSysTools::GetHighPrecisionTimestamp();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count();

    Log::Debug("Reference tree JSON serialization completed: ", duration, "ms, ",
               out.size(), " bytes, ", types.count, " types, ",
               tree._roots.size(), " roots");

    return out;
}

TypeReferenceTreeJsonSerializer::TypeTable::TypeTable(IFrameStore* frameStore) :
    pFrameStore(frameStore)
{
    json.reserve(2048);
}

uint32_t TypeReferenceTreeJsonSerializer::RegisterType(TypeTable& types, ClassID typeID)
{
    auto [it, inserted] = types.typeToIndex.try_emplace(typeID, types.count);
    if (inserted)
    {
        if (types.count > 0)
        {
            types.json += ',';
        }
        types.json += '"';

        // The std::string overload returns the namespace qualified name, so the type
        // names here match the ones emitted in the class histogram. It builds that name
        // into a temporary and move-assigns it, so scratch saves declaring a local per
        // call but its buffer is replaced on every resolution.
        if (types.pFrameStore->GetTypeName(typeID, types.scratch))
        {
            AppendEscapedJson(types.json, types.scratch);
        }
        else
        {
            types.json += '?';
        }

        types.json += '"';
        types.count++;
    }

    return it->second;
}

void TypeReferenceTreeJsonSerializer::OutputNode(const TypeTreeNode& node, TypeTable& types, std::string& out)
{
    // B3+B4: Lazily register type on first encounter (single lookup via try_emplace)
    auto typeIndex = RegisterType(types, node.typeID);

    out += "{\"t\":";
    AppendUInt32(out, typeIndex);

    if (node.instanceCount > 0)
    {
        out += ",\"ic\":";
        AppendUInt64(out, node.instanceCount);
    }
    if (node.totalSize > 0)
    {
        out += ",\"ts\":";
        AppendUInt64(out, node.totalSize);
    }

    if (!node.children.empty())
    {
        out += ",\"ch\":[";
        bool firstChild = true;
        for (const auto& [childTypeID, childNode] : node.children)
        {
            if (!firstChild)
            {
                out += ',';
            }
            firstChild = false;
            OutputNode(*childNode, types, out);
        }
        out += ']';
    }

    out += '}';
}

// Single-letter root category codes in JSON ("c" field). K=stack, S=static, O=other (see RootCategory).
const char* TypeReferenceTreeJsonSerializer::GetRootCategoryCode(RootCategory category)
{
    switch (category)
    {
        case RootCategory::Stack: return "K";
        case RootCategory::StaticVariable: return "S";
        case RootCategory::Finalizer: return "F";
        case RootCategory::Handle: return "H";
        case RootCategory::Pinning: return "P";
        case RootCategory::ConditionalWeakTable: return "W";
        case RootCategory::COM: return "R";
        case RootCategory::Other: return "O";
        case RootCategory::Unknown: return "?";
        default: return "?";
    }
}

void TypeReferenceTreeJsonSerializer::AppendUInt64(std::string& out, uint64_t v)
{
    char buf[24];
    int n = snprintf(buf, sizeof(buf), "%llu", static_cast<unsigned long long>(v));
    out.append(buf, n);
}

void TypeReferenceTreeJsonSerializer::AppendUInt32(std::string& out, uint32_t v)
{
    char buf[12];
    int n = snprintf(buf, sizeof(buf), "%u", v);
    out.append(buf, n);
}

// B2: Fast path — scan for chars needing escape first. If none found,
// append the entire string in one operation (zero per-char overhead).
void TypeReferenceTreeJsonSerializer::AppendEscapedJson(std::string& out, std::string_view str)
{
    // Fast path: check if any character needs escaping
    bool needsEscape = false;
    for (char c : str)
    {
        if (c == '"' || c == '\\' || static_cast<unsigned char>(c) < 0x20)
        {
            needsEscape = true;
            break;
        }
    }

    if (!needsEscape)
    {
        out.append(str.data(), str.size());
        return;
    }

    // Slow path: escape character by character
    out.reserve(out.size() + str.size() + 16);
    for (char c : str)
    {
        switch (c)
        {
            case '"': out += "\\\""; break;
            case '\\': out += "\\\\"; break;
            case '\b': out += "\\b"; break;
            case '\f': out += "\\f"; break;
            case '\n': out += "\\n"; break;
            case '\r': out += "\\r"; break;
            case '\t': out += "\\t"; break;
            default:
                if (static_cast<unsigned char>(c) < 0x20)
                {
                    char buf[7];
                    snprintf(buf, sizeof(buf), "\\u%04x", static_cast<unsigned char>(c));
                    out.append(buf, 6);
                }
                else
                {
                    out += c;
                }
                break;
        }
    }
}
