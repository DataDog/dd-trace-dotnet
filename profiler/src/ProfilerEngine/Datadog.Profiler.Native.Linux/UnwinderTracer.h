// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <algorithm>
#include <array>
#include <cstdint>
#include <cstring>
#include <iosfwd>
#include <ucontext.h>

#define UNW_LOCAL_ONLY
#include <libunwind.h>

// ---------------------------------------------------------------------------
// EventType
// ---------------------------------------------------------------------------
enum class EventType : std::uint8_t
{
    Start             = 0,
    InitCursor        = 1,
    NativeFrame       = 2,
    ManagedTransition = 3,
    LibunwindStep     = 4,
    FrameChainStep    = 5,
    Finish            = 6,
};

// ---------------------------------------------------------------------------
// FinishReason -- why Unwind stopped
// ---------------------------------------------------------------------------
enum class FinishReason : std::uint8_t
{
    Success             = 0,
    BufferFull          = 1,
    FailedGetContext     = 2,
    FailedInitLocal2    = 3,
    FailedGetReg        = 4,
    FailedLibunwindStep = 5,
    NoStackBounds       = 6,
    InvalidFp           = 7,
    TooManyNativeFrames = 8,
    InvalidIp           = 9,
    FailedIsManaged     = 10,
};

// ---------------------------------------------------------------------------
// CursorSnapshot -- 10 diagnostic fields from libunwind cursor internals
// ---------------------------------------------------------------------------
struct CursorSnapshot
{
    std::uintptr_t ip;
    std::uintptr_t cfa;
    std::uintptr_t locFp;
    std::uintptr_t locLr;
    std::uintptr_t locSp;
    std::uint32_t  nextToSignalFrame;
    std::uint32_t  cfaIsUnreliable;

    std::int64_t frameType;
    std::int64_t cfaRegSp;
    std::int64_t cfaRegOffset;
    std::int32_t dwarfStepResult;
    std::int32_t stepMethod;
    std::int32_t locInfo;
};

// ---------------------------------------------------------------------------
// TraceEvent
// ---------------------------------------------------------------------------
struct TraceEvent
{
    EventType eventType;
    FinishReason finishReason;
    std::int32_t result;
    std::uintptr_t ip;
    std::uintptr_t fp;
    std::uintptr_t sp;
    CursorSnapshot cursorSnapshot;
    ucontext_t* context;
};

// ---------------------------------------------------------------------------
// SnapshotCursor -- extract CursorSnapshot from opaque unw_cursor_t
// ---------------------------------------------------------------------------
inline CursorSnapshot SnapshotCursor(const unw_cursor_snapshot_t& snapshot)
{
    CursorSnapshot s;
    s.ip                = snapshot.ip;
    s.cfa               = snapshot.cfa;
    s.locFp             = snapshot.loc_fp;
    s.locLr             = snapshot.loc_ip;
    s.locSp             = snapshot.loc_sp;
    s.nextToSignalFrame = snapshot.next_to_signal_frame;
    s.cfaIsUnreliable   = snapshot.cfa_is_unreliable;
    s.frameType         = snapshot.frame_type;
    s.cfaRegSp          = snapshot.cfa_reg_sp;
    s.cfaRegOffset      = snapshot.cfa_reg_offset;
    s.dwarfStepResult   = snapshot.dwarf_step_ret;
    s.stepMethod        = snapshot.step_method;
    s.locInfo           = snapshot.loc_info;
    return s;
}

// ---------------------------------------------------------------------------
// UnwinderTracer
// ---------------------------------------------------------------------------
class UnwinderTracer
{
public:
    void Reset()
    {
        _totalEvents = 0;
    }

    void RecordStart(ucontext_t* context)
    {
        if (_totalEvents < Capacity)
        {
            _entries[_totalEvents].eventType = EventType::Start;
            _entries[_totalEvents].result = 0;
            _entries[_totalEvents].context = context;
        }
        _totalEvents++;
    }

    void Record(EventType eventType, std::int32_t result = 0)
    {
        if (_totalEvents < Capacity)
        {
            _entries[_totalEvents].eventType = eventType;
            _entries[_totalEvents].result = result;
        }
        _totalEvents++;
    }

    void RecordFinish(std::int32_t result, FinishReason reason)
    {
        if (_totalEvents < Capacity)
        {
            _entries[_totalEvents].eventType = EventType::Finish;
            _entries[_totalEvents].result = result;
            _entries[_totalEvents].finishReason = reason;
        }
        _totalEvents++;
    }

    void Record(EventType eventType, std::int32_t result, const unw_cursor_snapshot_t& cursor)
    {
        if (_totalEvents < Capacity)
        {
            _entries[_totalEvents].eventType = eventType;
            _entries[_totalEvents].result = result;
            _entries[_totalEvents].cursorSnapshot = SnapshotCursor(cursor);
        }
        _totalEvents++;
    }

    void Record(EventType eventType, std::uintptr_t ip, std::uintptr_t fp, std::uintptr_t sp)
    {
        if (_totalEvents < Capacity)
        {
            _entries[_totalEvents].eventType = eventType;
            _entries[_totalEvents].ip = ip;
            _entries[_totalEvents].fp = fp;
            _entries[_totalEvents].sp = sp;
        }
        _totalEvents++;
    }

    void Record(EventType eventType, std::uintptr_t ip, std::uintptr_t fp)
    {
        if (_totalEvents < Capacity)
        {
            _entries[_totalEvents].eventType = eventType;
            _entries[_totalEvents].ip = ip;
            _entries[_totalEvents].fp = fp;
        }
        _totalEvents++;
    }

    std::size_t TotalEvents() const { return _totalEvents; }
    std::size_t RecordedEvents() const { return std::min(_totalEvents, Capacity); }
    bool Overflowed() const { return _totalEvents > Capacity; }

    const TraceEvent* begin() const { return _entries.data(); }
    const TraceEvent* end() const { return _entries.data() + RecordedEvents(); }

    void WriteTo(std::ostream& os) const;

private:
    static constexpr std::size_t Capacity = 256;
    std::array<TraceEvent, Capacity> _entries;
    std::size_t _totalEvents = 0;
};
