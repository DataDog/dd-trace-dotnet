// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "UnwindingRecorderFactory.h"

UnwindingRecorderFactory::UnwindingRecorderFactory(std::size_t nbTracers)
    : _headTracer(nullptr)
{
    for (std::size_t i = 0; i < nbTracers; ++i)
    {
        auto* node = new TracerNode();
        node->recorder = new UnwindingRecorder();
        node->next.store(_headTracer.load(std::memory_order_relaxed), std::memory_order_relaxed);
        _headTracer.store(node, std::memory_order_relaxed);
    }
}

UnwindingRecorderFactory::~UnwindingRecorderFactory()
{
    auto* current = _headTracer.load(std::memory_order_relaxed);
    while (current != nullptr)
    {
        auto* next = current->next.load(std::memory_order_relaxed);
        delete current->recorder;
        delete current;
        current = next;
    }
}

UnwindingRecorderFactory::ScopedTracer UnwindingRecorderFactory::GetTracer()
{
    return ScopedTracer(this);
}

UnwindingRecorderFactory::ScopedTracer::ScopedTracer(UnwindingRecorderFactory* factory)
    : _factory(factory)
{
    if (_factory != nullptr)
    {
        _node = _factory->AcquireTracer();
    }
}

UnwindingRecorderFactory::ScopedTracer::ScopedTracer(ScopedTracer&& other) noexcept
    : _factory(other._factory), _node(other._node)
{
    other._factory = nullptr;
    other._node = nullptr;
}

UnwindingRecorderFactory::ScopedTracer& UnwindingRecorderFactory::ScopedTracer::operator=(ScopedTracer&& other) noexcept
{
    if (this != &other)
    {
        if (_factory != nullptr && _node != nullptr)
        {
            _factory->ReleaseTracer(_node);
        }
        _factory = other._factory;
        _node = other._node;
        other._factory = nullptr;
        other._node = nullptr;
    }
    return *this;
}

UnwindingRecorderFactory::ScopedTracer::~ScopedTracer()
{
    if (_factory != nullptr && _node != nullptr)
    {
        _factory->ReleaseTracer(_node);
    }
}

UnwindingRecorder* UnwindingRecorderFactory::ScopedTracer::get()
{
    if (_node == nullptr)
    {
        return nullptr;
    }
    return _node->recorder;
}

UnwindingRecorderFactory::TracerNode* UnwindingRecorderFactory::AcquireTracer()
{
    TracerNode* head = _headTracer.load(std::memory_order_acquire);
    while (head != nullptr)
    {
        if (_headTracer.compare_exchange_weak(head, head->next.load(std::memory_order_relaxed),
                                               std::memory_order_acq_rel, std::memory_order_relaxed))
        {
            head->next.store(nullptr, std::memory_order_relaxed);
            return head;
        }
    }
    return nullptr;
}

void UnwindingRecorderFactory::ReleaseTracer(TracerNode* node)
{
    TracerNode* head = _headTracer.load(std::memory_order_relaxed);
    node->recorder->Reset();
    do
    {
        node->next.store(head, std::memory_order_relaxed);
    } while (!_headTracer.compare_exchange_weak(head, node,
                                                 std::memory_order_release, std::memory_order_relaxed));
}
