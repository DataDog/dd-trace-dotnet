#include "JitCodeCache.h"

#include <memory>

JitCodeCache& JitCodeCache::Instance()
{
    static JitCodeCache instance;
    return instance;
}

JitCodeCache::JitCodeCache() :
    _head(nullptr)
{
}

JitCodeCache::~JitCodeCache()
{
    Node* current = _head.load(std::memory_order_acquire);
    while (current != nullptr)
    {
        std::unique_ptr<Node> holder(current);
        current = holder->Next;
    }
}

void JitCodeCache::RegisterMethod(const MethodInfo& info)
{
    auto node = std::make_unique<Node>();
    node->Info = info;
    node->Next = nullptr;

    Node* rawNode = node.get();
    Node* current = _head.load(std::memory_order_acquire);
    while (true)
    {
        rawNode->Next = current;
        if (_head.compare_exchange_weak(current, rawNode, std::memory_order_release, std::memory_order_acquire))
        {
            node.release();
            break;
        }
    }
}

const JitCodeCache::MethodInfo* JitCodeCache::FindMethod(uintptr_t address) const
{
    Node* current = _head.load(std::memory_order_acquire);
    while (current != nullptr)
    {
        const auto& method = current->Info;
        if (address >= method.Start && address < method.End)
        {
            return &method;
        }

        current = current->Next;
    }

    return nullptr;
}

