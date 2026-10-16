// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "cor.h"
#include "corprof.h"
#include "ReferenceChainTypes.h"
#include "shared/src/native-src/string.h"
#include <algorithm>
#include <array>
#include <cstdint>
#include <memory>
#include <string>
#include <unordered_map>

// Maximum depth for tree traversal to prevent pathological cases
// (e.g., million-element linked lists creating million-level trees).
// Beyond this depth, the additional retention information is minimal.
static constexpr uint32_t MaxTreeDepth = 128;

// A node in the type reference tree.
// Each node represents a type AT A SPECIFIC POSITION in a reference chain.
// The same ClassID can appear at multiple positions (different nodes).
// For example: TypeA -> TypeB -> TypeA -> TypeC produces 4 nodes.
struct TypeTreeNode
{
private:
    struct InlineChild
    {
        ClassID typeID = 0;
        std::unique_ptr<TypeTreeNode> node;
    };

    using OverflowChildren = std::unordered_map<ClassID, std::unique_ptr<TypeTreeNode>>;
    static constexpr size_t InlineChildCapacity = 4;

public:
    ClassID typeID;
    uint64_t instanceCount;  // How many instances at this tree position
    uint64_t totalSize;      // Reserved wire-format field; always 0 for reference trees

    TypeTreeNode(ClassID id) : typeID(id), instanceCount(0), totalSize(0)
    {
    }

    void AddInstance()
    {
        instanceCount++;
    }

    // Get or create a child node for the given type.
    TypeTreeNode* GetOrCreateChild(ClassID childTypeID)
    {
        for (size_t i = 0; i < _inlineChildCount; i++)
        {
            if (_inlineChildren[i].typeID == childTypeID)
            {
                return _inlineChildren[i].node.get();
            }
        }

        if (_overflowChildren != nullptr)
        {
            auto it = _overflowChildren->find(childTypeID);
            if (it != _overflowChildren->end())
            {
                return it->second.get();
            }
        }

        if (_inlineChildCount < InlineChildCapacity)
        {
            auto child = std::make_unique<TypeTreeNode>(childTypeID);
            TypeTreeNode* childPtr = child.get();
            InlineChild& entry = _inlineChildren[_inlineChildCount++];
            entry.typeID = childTypeID;
            entry.node = std::move(child);
            return childPtr;
        }

        if (_overflowChildren == nullptr)
        {
            auto overflowChildren = std::make_unique<OverflowChildren>();
            overflowChildren->reserve(InlineChildCapacity);
            _overflowChildren = std::move(overflowChildren);
        }

        auto child = std::make_unique<TypeTreeNode>(childTypeID);
        auto it = _overflowChildren->try_emplace(childTypeID, std::move(child)).first;
        return it->second.get();
    }

    // Get an existing child node (returns nullptr if not found).
    const TypeTreeNode* GetChild(ClassID childTypeID) const
    {
        for (size_t i = 0; i < _inlineChildCount; i++)
        {
            if (_inlineChildren[i].typeID == childTypeID)
            {
                return _inlineChildren[i].node.get();
            }
        }

        if (_overflowChildren == nullptr)
        {
            return nullptr;
        }

        auto it = _overflowChildren->find(childTypeID);
        return it != _overflowChildren->end() ? it->second.get() : nullptr;
    }

    size_t GetChildCount() const
    {
        return _inlineChildCount + (_overflowChildren != nullptr ? _overflowChildren->size() : 0);
    }

    bool HasChildren() const
    {
        return GetChildCount() != 0;
    }

    template <typename TCallback>
    void ForEachChild(TCallback&& callback) const
    {
        for (size_t i = 0; i < _inlineChildCount; i++)
        {
            callback(*_inlineChildren[i].node);
        }

        if (_overflowChildren != nullptr)
        {
            for (const auto& [_, childNode] : *_overflowChildren)
            {
                callback(*childNode);
            }
        }
    }

private:
    std::array<InlineChild, InlineChildCapacity> _inlineChildren;
    uint8_t _inlineChildCount = 0;
    std::unique_ptr<OverflowChildren> _overflowChildren;
};


// Key for roots: (type, category) so the same type can appear as distinct roots per category
struct RootKey
{
    ClassID typeID;
    RootCategory category;

    bool operator==(const RootKey& o) const
    {
        return typeID == o.typeID && category == o.category;
    }
};

struct RootKeyHash
{
    size_t operator()(const RootKey& k) const
    {
        size_t h1 = std::hash<ClassID>{}(k.typeID);
        size_t h2 = std::hash<uint8_t>{}(static_cast<uint8_t>(k.category));
        return h1 ^ (h2 << 16);
    }
};

// A root node in the reference tree.
// Each root is uniquely identified by (type, category) so byte[] as Pinning
// and byte[] as Stack are distinct entries.
struct TypeRootNode
{
    TypeTreeNode node;
    RootCategory category;
    std::string fieldName;  // For static roots: the declaring field name (e.g., "_staticOrders")

    TypeRootNode(ClassID typeID, RootCategory cat) : node(typeID), category(cat)
    {
    }

    void AddInstance(const WCHAR* field = nullptr)
    {
        node.AddInstance();
        if (field != nullptr && *field != L'\0' && fieldName.empty())
        {
            fieldName = shared::ToString(field);
        }
    }
};


// Complete type reference tree.
// Roots are keyed by (ClassID, RootCategory) so the same type can appear
// as distinct roots for different categories (e.g. byte[] as Pinning vs Stack).
class TypeReferenceTree
{
public:
    struct Statistics
    {
        size_t nodeCount = 0;
        size_t leafCount = 0;
        size_t children1To4 = 0;
        size_t children5To8 = 0;
        size_t children9To16 = 0;
        size_t children17OrMore = 0;
        size_t maxChildren = 0;
    };

    std::unordered_map<RootKey, std::unique_ptr<TypeRootNode>, RootKeyHash> _roots;

    // Add or update a root for the given (type, category).
    // Returns a pointer to the root's TypeTreeNode for use during traversal.
    TypeTreeNode* AddRoot(ClassID typeID, RootCategory category, const WCHAR* fieldName = nullptr)
    {
        RootKey key{typeID, category};
        auto [it, inserted] = _roots.try_emplace(key, nullptr);
        if (inserted)
        {
            it->second = std::make_unique<TypeRootNode>(typeID, category);
        }
        it->second->AddInstance(fieldName);
        return &it->second->node;
    }

    bool IsEmpty() const
    {
        return _roots.empty();
    }

    size_t GetNodeCount() const
    {
        return GetStatistics().nodeCount;
    }

    Statistics GetStatistics() const
    {
        Statistics statistics;
        for (const auto& rootEntry : _roots)
        {
            AccumulateStatistics(rootEntry.second->node, statistics);
        }
        return statistics;
    }

    void Clear()
    {
        _roots.clear();
    }

private:
    static void AccumulateStatistics(const TypeTreeNode& node, Statistics& statistics)
    {
        statistics.nodeCount++;

        size_t childCount = node.GetChildCount();
        statistics.maxChildren = (std::max)(statistics.maxChildren, childCount);
        if (childCount == 0)
        {
            statistics.leafCount++;
        }
        else if (childCount <= 4)
        {
            statistics.children1To4++;
        }
        else if (childCount <= 8)
        {
            statistics.children5To8++;
        }
        else if (childCount <= 16)
        {
            statistics.children9To16++;
        }
        else
        {
            statistics.children17OrMore++;
        }

        node.ForEachChild([&statistics](const TypeTreeNode& child) { AccumulateStatistics(child, statistics); });
    }
};
