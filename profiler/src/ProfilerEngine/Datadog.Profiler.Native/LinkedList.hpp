// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cassert>
#include <cstdlib>
#include <memory>
#include <stdexcept>
#include <utility>

#include "shared/src/native-src/dd_memory_resource.hpp"

#ifdef DD_TEST
#define NOEXCEPT noexcept(false)
#else
#define NOEXCEPT noexcept
#endif

template <class T>
class LinkedList
{
public:
    LinkedList(shared::pmr::memory_resource* allocator) noexcept :
        _head{nullptr}, _tail{&_head}, _nbElements{0}, _allocator{allocator}
    {
    }

    ~LinkedList()
    {
        auto* current = std::exchange(_head, nullptr);

        while (current != nullptr)
        {
            auto* next = current->Next;
            // call Node dtor
            std::destroy_at(current);
            _allocator->deallocate(current, sizeof(Node));
            current = next;
        }
    }

    LinkedList(LinkedList const&) = delete;
    LinkedList& operator=(LinkedList const&) = delete;

    LinkedList(LinkedList&& other) NOEXCEPT : LinkedList(shared::pmr::get_default_resource())
    {
        *this = std::move(other);
    }

    LinkedList& operator=(LinkedList&& other) NOEXCEPT
    {
        if (this == &other)
        {
            return *this;
        }

        Swap(other);

        return *this;
    }

    void Swap(LinkedList& other)
    {
        if (this == &other)
        {
            return;
        }

        _head = std::exchange(other._head, _head);
        _tail = std::exchange(other._tail, _tail);

        if (_head == nullptr)
            _tail = &_head;

        if (other._head == nullptr)
            other._tail = &other._head;

#ifdef DD_TEST
        if (_head == nullptr && _tail != &_head)
        {
            throw std::runtime_error("_tail must have the address of _head");
        }

        if (other._head == nullptr && other._tail != &other._head)
        {
            throw std::runtime_error("other._tail must have the address of other._head");
        }
#endif

        std::swap(_nbElements, other._nbElements);
        std::swap(_allocator, other._allocator);
    }

    bool Append(T&& v)
    {
        NodeGuard guard(_allocator);
        guard.Allocate();

        if (guard._Node == nullptr)
        {
            return false;
        }

        ConstructNode(guard._Node, std::move(v));

        *_tail = guard.Release();
        _tail = &((*_tail)->Next);

        _nbElements++;

        return true;
    }

    std::size_t Size() const
    {
        return _nbElements;
    }

    class iterator;

    iterator begin()
    {
        return iterator(_head);
    }

    iterator end()
    {
        return iterator(nullptr);
    }

private:
    struct Node;

public:
    class iterator
    {
    public:
        iterator(Node* node) :
            _Ptr{node}
        {
        }

        T& operator*() const
        {
            return _Ptr->Value;
        }

        iterator operator++(int)
        {
            auto tmp = *this;
            ++*this;
            return tmp;
        }

        iterator& operator++()
        {
            _Ptr = _Ptr->Next;
            return *this;
        }

        bool operator==(iterator const& other) const
        {
            return _Ptr == other._Ptr;
        }

        bool operator!=(iterator const& other) const
        {
            return !(*this == other);
        }

    private:
        Node* _Ptr;
    };

private:
    // Actual LinkedList node
    struct Node
    {
    public:
        T Value;
        Node* Next;

        template <class Ty>
        Node(Ty&& v, Node* next) :
            Value(std::forward<Ty>(v)), Next{next}
        {
        }

        Node(Node const&) = delete;
        Node& operator=(Node const&) = delete;
    };

    // Used to make sure the code is exception-safe when we Append an element.
    // In case, construction throws, we won't leak memory.
    struct NodeGuard
    {
    public:
        constexpr explicit NodeGuard(shared::pmr::memory_resource* mr) :
            _mr{mr}, _Node{nullptr}
        {
        }

        constexpr ~NodeGuard()
        {
            if (_Node != nullptr)
            {
                _mr->deallocate(_Node, sizeof(Node));
            }
        }

        NodeGuard(NodeGuard const&) = delete;
        NodeGuard& operator=(NodeGuard const&) = delete;

        constexpr void Allocate()
        {
            _Node = reinterpret_cast<Node*>(_mr->allocate(sizeof(Node)));
        }

        [[nodiscard]] constexpr Node* Release()
        {
            return std::exchange(_Node, nullptr);
        }

        shared::pmr::memory_resource* _mr;
        Node* _Node;
    };

    constexpr void ConstructNode(void* ptr, T&& value)
    {
        new (ptr) Node(std::move(value), nullptr);
    }

    // On linux allocate has std::pmr::memory_resource::allocate function has the returns_nonnull attribute.
    // This allows the compiler to optimize the code, by removing the null-check.
    // We observed a segmentation fault in our CI with ASAN and UBSAN in the unit tests.
    // We still want to keep the null-check, because when we will use (for some profilers) the
    // ringbuffer-based memory_resource, it could return nullptr (when there is no more room)
    inline void* allocateNode()
    {
        return _allocator->allocate(sizeof(Node));
    }

    Node* _head;
    Node** _tail;
    std::size_t _nbElements;

    shared::pmr::memory_resource* _allocator;
};
