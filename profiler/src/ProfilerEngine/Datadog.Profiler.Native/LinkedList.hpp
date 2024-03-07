// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#ifdef __has_include                 // Check if __has_include is present
#if __has_include(<memory_resource>) // Check for a standard library
#include <memory_resource>
namespace pmr {
using namespace std::pmr;
}
#elif __has_include(<experimental/memory_resource>) // Check for an experimental version
#include <experimental/memory_resource>
namespace pmr {
using namespace std::experimental::pmr;
}
#else // Not found at all
#error "Missing <memory_resource>"
#endif
#endif

#include <cassert>
#include <cstdlib>
#include <memory>
#include <utility>
#include <stdexcept>

#ifdef DD_TEST
#define NOEXCEPT noexcept(false)
#else
#define NOEXCEPT noexcept
#endif

template <class T>
class LinkedList
{
private:
    struct Node;
    using node_type = Node;

public:
    LinkedList(pmr::memory_resource* allocator = pmr::get_default_resource()) noexcept :
        _head{nullptr}, _tail{&_head}, _nbElements{0}, _allocator{allocator}
    {
    }

    ~LinkedList()
    {
        auto* current = std::exchange(_head, nullptr);

        while (current != nullptr)
        {
            auto* next = current->Next;
            std::destroy_at(current);
            _allocator->deallocate(current, sizeof(Node));
            current = next;
        }
    }

    LinkedList(LinkedList const&) = delete;
    LinkedList& operator=(LinkedList const&) = delete;

    LinkedList(LinkedList&& other) NOEXCEPT :
        LinkedList() // should we use the default allocator ? or allow an allocator to the move ctor ?
    {
        *this = std::move(other);
    }

    LinkedList& operator=(LinkedList&& other) NOEXCEPT
    {
        if (this == &other)
        {
            return *this;
        }

        swap(other);

        return *this;
    }

    void swap(LinkedList& other)
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

    bool append(T&& v)
    {
        auto* ptr = _allocator->allocate(sizeof(Node));

        if (ptr == nullptr)
        {
            return false;
        }

        *_tail = new (ptr) Node(std::move(v), nullptr);
        _tail = &((*_tail)->Next);

        _nbElements++;

        return true;
    }

    std::size_t size() const
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

    Node* _head;
    Node** _tail;
    std::size_t _nbElements;

    pmr::memory_resource* _allocator;
};