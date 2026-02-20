// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "TypeReferenceTreeJsonSerializer.h"
#include "Log.h"
#include "OpSysTools.h"
#include <sstream>
#include <algorithm>

std::string TypeReferenceTreeJsonSerializer::Serialize(const TypeReferenceTree& tree, IFrameStore* pFrameStore) {
    auto startTime = OpSysTools::GetHighPrecisionTimestamp();

    if (pFrameStore == nullptr) {
        return "{}";
    }

    // Build type table by walking the entire tree.
    // This assigns each unique ClassID an index for compact JSON output.
    std::unordered_map<ClassID, uint32_t> typeToIndex;
    std::vector<std::string> typeTable;
    uint32_t nextIndex = 0;

    for (const auto& [typeID, rootNode] : tree._roots) {
        CollectTypes(rootNode->node, typeToIndex, typeTable, nextIndex, pFrameStore);
    }

    std::stringstream ss;
    ss << std::fixed;  // No scientific notation

    // Output version
    ss << "{\"v\":7";

    // Output type table
    if (!typeTable.empty()) {
        ss << ",\"tt\":[";
        for (size_t i = 0; i < typeTable.size(); i++) {
            if (i > 0) ss << ",";
            ss << "\"" << EscapeJson(typeTable[i]) << "\"";
        }
        ss << "]";
    }

    // Output roots array with hierarchical tree
    ss << ",\"r\":[";

    bool firstRoot = true;
    for (const auto& [typeID, rootNode] : tree._roots) {
        auto it = typeToIndex.find(typeID);
        if (it == typeToIndex.end()) {
            continue;  // Type not in table (GetTypeName failed)
        }

        if (!firstRoot) ss << ",";
        firstRoot = false;

        uint32_t typeIndex = it->second;

        // Find first root category
        uint8_t categoryIndex = 0;
        for (int cat = 0; cat < 8; cat++) {
            if (rootNode->HasRootCategory(static_cast<RootCategory>(cat))) {
                categoryIndex = cat;
                break;
            }
        }

        const char* categoryCode = GetRootCategoryCode(static_cast<RootCategory>(categoryIndex));

        ss << "{\"t\":" << typeIndex
           << ",\"c\":\"" << categoryCode << "\""
           << ",\"ic\":" << rootNode->node.instanceCount
           << ",\"ts\":" << rootNode->node.totalSize;

        // Output children
        if (!rootNode->node.children.empty()) {
            ss << ",\"ch\":[";
            bool firstChild = true;
            for (const auto& [childTypeID, childNode] : rootNode->node.children) {
                if (!firstChild) ss << ",";
                firstChild = false;
                OutputNode(*childNode, typeToIndex, ss);
            }
            ss << "]";
        }
        ss << "}";
    }

    ss << "]}";

    std::string result = ss.str();

    auto endTime = OpSysTools::GetHighPrecisionTimestamp();
    auto duration = std::chrono::duration_cast<std::chrono::milliseconds>(endTime - startTime).count();

    Log::Debug("Reference tree JSON serialization completed: ", duration, "ms, ",
               result.size(), " bytes, ", typeTable.size(), " types, ",
               tree._roots.size(), " roots");

    return result;
}

void TypeReferenceTreeJsonSerializer::OutputNode(
    const TypeTreeNode& node,
    const std::unordered_map<ClassID, uint32_t>& typeToIndex,
    std::stringstream& ss) {

    auto it = typeToIndex.find(node.typeID);
    if (it == typeToIndex.end()) {
        ss << "{}";  // Unknown type — emit empty node to keep JSON valid
        return;
    }

    uint32_t typeIndex = it->second;

    ss << "{\"t\":" << typeIndex;

    if (node.instanceCount > 0) {
        ss << ",\"ic\":" << node.instanceCount;
    }
    if (node.totalSize > 0) {
        ss << ",\"ts\":" << node.totalSize;
    }

    // Output children (no cycle detection needed — the tree is acyclic by construction)
    if (!node.children.empty()) {
        ss << ",\"ch\":[";
        bool firstChild = true;
        for (const auto& [childTypeID, childNode] : node.children) {
            if (!firstChild) ss << ",";
            firstChild = false;
            OutputNode(*childNode, typeToIndex, ss);
        }
        ss << "]";
    }

    ss << "}";
}

void TypeReferenceTreeJsonSerializer::CollectTypes(
    const TypeTreeNode& node,
    std::unordered_map<ClassID, uint32_t>& typeToIndex,
    std::vector<std::string>& typeTable,
    uint32_t& nextIndex,
    IFrameStore* pFrameStore) {

    // Add this type if not already in the table
    if (typeToIndex.find(node.typeID) == typeToIndex.end()) {
        std::string typeName;
        if (pFrameStore->GetTypeName(node.typeID, typeName)) {
            typeToIndex[node.typeID] = nextIndex++;
            typeTable.push_back(typeName);
        }
    }

    // Recurse into children
    for (const auto& [childTypeID, childNode] : node.children) {
        CollectTypes(*childNode, typeToIndex, typeTable, nextIndex, pFrameStore);
    }
}

const char* TypeReferenceTreeJsonSerializer::GetRootCategoryCode(RootCategory category) {
    switch (category) {
        case RootCategory::Stack: return "S";
        case RootCategory::StaticVariable: return "s";
        case RootCategory::Finalizer: return "F";
        case RootCategory::Handle: return "H";
        case RootCategory::Pinning: return "P";
        case RootCategory::ConditionalWeakTable: return "W";
        case RootCategory::COM: return "R";
        case RootCategory::Unknown: return "?";
        default: return "?";
    }
}

std::string TypeReferenceTreeJsonSerializer::EscapeJson(const std::string& str) {
    std::string escaped;
    escaped.reserve(str.length() + 10);

    for (char c : str) {
        switch (c) {
            case '"': escaped += "\\\""; break;
            case '\\': escaped += "\\\\"; break;
            case '\b': escaped += "\\b"; break;
            case '\f': escaped += "\\f"; break;
            case '\n': escaped += "\\n"; break;
            case '\r': escaped += "\\r"; break;
            case '\t': escaped += "\\t"; break;
            default:
                if (static_cast<unsigned char>(c) < 0x20) {
                    char buf[7];
                    snprintf(buf, sizeof(buf), "\\u%04x", static_cast<unsigned char>(c));
                    escaped += buf;
                } else {
                    escaped += c;
                }
                break;
        }
    }

    return escaped;
}
