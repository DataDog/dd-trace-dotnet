#include "UnwindTracersProvider.h"

#define NB_TRACER 50

UnwindTracersProvider::UnwindTracersProvider(std::size_t nbTracers)
    : _headTracer(nullptr)
{
    for (std::size_t i = 0; i < nbTracers; ++i)
    {
        auto* node = new TracerNode();
        node->tracer = new UnwinderTracer();
        node->next.store(_headTracer.load(std::memory_order_relaxed), std::memory_order_relaxed);
        _headTracer.store(node, std::memory_order_relaxed);
    }
}

UnwindTracersProvider::~UnwindTracersProvider()
{
    auto* current = _headTracer.load(std::memory_order_relaxed);
    while (current != nullptr)
    {
        auto* next = current->next.load(std::memory_order_relaxed);
        delete current->tracer;
        delete current;
        current = next;
    }
}

UnwindTracersProvider& UnwindTracersProvider::GetInstance()
{
    static UnwindTracersProvider instance(NB_TRACER);
    return instance;
}

UnwindTracersProvider::ScopedTracer UnwindTracersProvider::GetTracer()
{
    return ScopedTracer(this);
}

UnwindTracersProvider::ScopedTracer::ScopedTracer(UnwindTracersProvider* provider)
    : _provider(provider)
{
    if (_provider != nullptr)
    {
        _node = _provider->AcquireTracer();
    }
}

UnwindTracersProvider::ScopedTracer::ScopedTracer(ScopedTracer&& other) noexcept
    : _provider(other._provider), _node(other._node)
{
    other._provider = nullptr;
    other._node = nullptr;
}

UnwindTracersProvider::ScopedTracer& UnwindTracersProvider::ScopedTracer::operator=(ScopedTracer&& other) noexcept
{
    if (this != &other)
    {
        if (_provider != nullptr && _node != nullptr)
        {
            _provider->ReleaseTracer(_node);
        }
        _provider = other._provider;
        _node = other._node;
        other._provider = nullptr;
        other._node = nullptr;
    }
    return *this;
}

UnwindTracersProvider::ScopedTracer::~ScopedTracer()
{
    if (_provider != nullptr && _node != nullptr)
    {
        _provider->ReleaseTracer(_node);
    }
}

UnwinderTracer* UnwindTracersProvider::ScopedTracer::get()
{
    if (_node == nullptr)
    {
        return nullptr;
    }
    return _node->tracer;
}

UnwindTracersProvider::TracerNode* UnwindTracersProvider::AcquireTracer()
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

void UnwindTracersProvider::ReleaseTracer(TracerNode* node)
{
    TracerNode* head = _headTracer.load(std::memory_order_relaxed);
    node->tracer->Reset();
    do
    {
        node->next.store(head, std::memory_order_relaxed);
    } while (!_headTracer.compare_exchange_weak(head, node,
                                                 std::memory_order_release, std::memory_order_relaxed));
}
