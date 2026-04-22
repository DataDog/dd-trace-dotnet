#pragma once

#include <atomic>
#include <cstddef>

#include "UnwinderTracer.h"

class UnwindTracersProvider
{
public:
    ~UnwindTracersProvider();

    UnwindTracersProvider(UnwindTracersProvider&& other) noexcept = delete;
    UnwindTracersProvider(const UnwindTracersProvider& other) = delete;
    UnwindTracersProvider& operator=(UnwindTracersProvider&& other) noexcept = delete;
    UnwindTracersProvider& operator=(const UnwindTracersProvider& other) = delete;

    static UnwindTracersProvider& GetInstance();

    struct TracerNode
    {
        UnwinderTracer* tracer;
        std::atomic<TracerNode*> next;
    };

    struct ScopedTracer
    {
        ScopedTracer(UnwindTracersProvider* provider);
        ~ScopedTracer();

        // Ownership of _node is unique: a copied ScopedTracer would release the
        // same node twice through its destructor, corrupting the free-list.
        ScopedTracer(const ScopedTracer&) = delete;
        ScopedTracer& operator=(const ScopedTracer&) = delete;

        ScopedTracer(ScopedTracer&& other) noexcept;
        ScopedTracer& operator=(ScopedTracer&& other) noexcept;

        UnwinderTracer* get();

    private:
        UnwindTracersProvider* _provider = nullptr;
        TracerNode* _node = nullptr;
    };

    ScopedTracer GetTracer();

private:
    UnwindTracersProvider(std::size_t nbTracers);
    TracerNode* AcquireTracer();
    void ReleaseTracer(TracerNode* node);

    friend class ScopedTracer;
    std::atomic<TracerNode*> _headTracer;
};
