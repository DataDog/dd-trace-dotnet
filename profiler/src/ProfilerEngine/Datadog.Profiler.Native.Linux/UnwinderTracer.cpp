#include "UnwinderTracer.h"

void UnwinderTracer::Reset(pid_t threadId, uintptr_t contextPointer)
{
    _threadId = threadId;
    _contextPointer = contextPointer;
    _initFlags = 0;
    _count = 0;
    _overflow = false;
}

void UnwinderTracer::SetInitFlags(std::uint32_t flags)
{
    _initFlags = flags;
}

void UnwinderTracer::Append(HybridTraceEvent event, uintptr_t value, uintptr_t aux, std::int32_t result)
{
    auto index = _count.load(std::memory_order_relaxed);
    if (index >= static_cast<std::uint32_t>(MaxEntries))
    {
        _overflow.store(true, std::memory_order_relaxed);
        return;
    }

    _entries[static_cast<std::size_t>(index)] = HybridTraceEntry{event, value, aux, result};
    _count.store(static_cast<std::uint32_t>(index + 1U), std::memory_order_release);
}

std::uint32_t UnwinderTracer::Count() const
{
    return _count.load(std::memory_order_acquire);
}

bool UnwinderTracer::HasOverflow() const
{
    return _overflow.load(std::memory_order_acquire);
} 

UnwinderTracer::HybridTraceEntry UnwinderTracer::EntryAt(std::size_t index) const
{
    return _entries[index];
}

pid_t UnwinderTracer::GetThreadId() const
{
    return _threadId;
}

uintptr_t UnwinderTracer::GetContextPointer() const 
{
    return _contextPointer;
}

std::uint32_t UnwinderTracer::GetInitFlags() const
{
    return _initFlags;
}

void UnwinderTracer::ResetAfterFlush()
{
    _count = 0;
    _overflow = false;
}
