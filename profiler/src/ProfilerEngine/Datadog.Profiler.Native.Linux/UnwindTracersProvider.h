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
        ScopedTracer(UnwindTracersProvider& provider);
        ~ScopedTracer();

        UnwinderTracer* get();

    private:
        UnwindTracersProvider& _provider;
        TracerNode* _node;
    };

    ScopedTracer GetTracer();

private:
    UnwindTracersProvider(std::size_t nbTracers);
    TracerNode* AcquireTracer();
    void ReleaseTracer(TracerNode* node);

    friend class ScopedTracer;
    std::atomic<TracerNode*> _headTracer;
};
