// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <atomic>
#include <cstddef>

#include "UnwindingRecorder.h"

class UnwindingRecorderFactory
{
public:
    UnwindingRecorderFactory(std::size_t nbTracers);
    ~UnwindingRecorderFactory();

    UnwindingRecorderFactory(UnwindingRecorderFactory&& other) noexcept = delete;
    UnwindingRecorderFactory(const UnwindingRecorderFactory& other) = delete;
    UnwindingRecorderFactory& operator=(UnwindingRecorderFactory&& other) noexcept = delete;
    UnwindingRecorderFactory& operator=(const UnwindingRecorderFactory& other) = delete;

    struct TracerNode
    {
        UnwindingRecorder* recorder;
        std::atomic<TracerNode*> next;
    };

    struct ScopedTracer
    {
        ScopedTracer(UnwindingRecorderFactory* factory);
        ~ScopedTracer();

        // Ownership of _node is unique: a copied ScopedTracer would release the
        // same node twice through its destructor, corrupting the free-list.
        ScopedTracer(const ScopedTracer&) = delete;
        ScopedTracer& operator=(const ScopedTracer&) = delete;

        ScopedTracer(ScopedTracer&& other) noexcept;
        ScopedTracer& operator=(ScopedTracer&& other) noexcept;

        UnwindingRecorder* get();

    private:
        UnwindingRecorderFactory* _factory = nullptr;
        TracerNode* _node = nullptr;
    };

    ScopedTracer GetTracer();

private:
    TracerNode* AcquireTracer();
    void ReleaseTracer(TracerNode* node);

    friend class ScopedTracer;
    std::atomic<TracerNode*> _headTracer;
};
