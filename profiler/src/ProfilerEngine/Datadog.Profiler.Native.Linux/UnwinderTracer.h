// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <algorithm>
#include <array>
#include <cstdint>
#include <iosfwd>

#define UNW_LOCAL_ONLY
#include <libunwind.h>

// ---------------------------------------------------------------------------
// Mirror structs for libunwind internals (pinned to DataDog/libunwind v1.8.1-custom-3)
//
// These reproduce the memory layout of struct dwarf_cursor and struct cursor
// from dwarf.h / tdep-aarch64/libunwind_i.h so we can extract diagnostic
// fields without including libunwind internal headers.
// static_asserts at the bottom verify the layout hasn't drifted.
//
// aarch64-only: on other architectures SnapshotCursor returns a zeroed snapshot.
// ---------------------------------------------------------------------------

namespace libunwind_mirror {

static constexpr int kNumEhRegs = 4;          // UNW_TDEP_NUM_EH_REGS
static constexpr int kNumPreservedRegs = 97;  // DWARF_NUM_PRESERVED_REGS
static constexpr int kLocFp = 29;             // UNW_AARCH64_X29
static constexpr int kLocLr = 30;             // UNW_AARCH64_X30
static constexpr int kLocSp = 31;             // UNW_AARCH64_SP

struct dwarf_loc
{
    std::uintptr_t val;
};

struct dwarf_cursor
{
    void* as_arg;
    void* as;
    std::uintptr_t cfa;
    std::uintptr_t ip;
    std::uintptr_t args_size;
    std::uintptr_t eh_args[kNumEhRegs];
    std::uint32_t eh_valid_mask;
    std::uint32_t _pad0;
    dwarf_loc loc[kNumPreservedRegs];
    std::uint32_t bitfields;
    std::uint32_t _pad1;
    unw_proc_info_t pi;
    short hint;
    short prev_rs;
};

// bitfield bit indices within dwarf_cursor::bitfields
static constexpr int kBitNextToSignalFrame = 4;
static constexpr int kBitCfaIsUnreliable = 5;

struct tdep_frame
{
    std::uint64_t virtual_address;
    std::int64_t frame_type     : 2;
    std::int64_t last_frame     : 1;
    std::int64_t cfa_reg_sp     : 1;
    std::int64_t cfa_reg_offset : 30;
    std::int64_t fp_cfa_offset  : 30;
    std::int64_t lr_cfa_offset  : 30;
    std::int64_t sp_cfa_offset  : 30;
};

struct cursor
{
    dwarf_cursor dwarf;
    tdep_frame frame_info;
};

static_assert(sizeof(unw_cursor_t) == 250 * sizeof(unw_word_t),
              "unw_cursor_t size changed -- update mirror structs");
static_assert(sizeof(cursor) <= sizeof(unw_cursor_t),
              "mirror cursor exceeds unw_cursor_t -- layout drift");
static_assert(offsetof(dwarf_cursor, ip) == 24,
              "dwarf_cursor::ip offset changed");
static_assert(offsetof(dwarf_cursor, cfa) == 16,
              "dwarf_cursor::cfa offset changed");
static_assert(offsetof(dwarf_cursor, loc) == 80,
              "dwarf_cursor::loc offset changed");

} // namespace libunwind_mirror


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
};

// ---------------------------------------------------------------------------
// SnapshotCursor -- extract CursorSnapshot from opaque unw_cursor_t
// ---------------------------------------------------------------------------
inline CursorSnapshot SnapshotCursor(const unw_cursor_t& opaque)
{
    using namespace libunwind_mirror;
    const auto* c = reinterpret_cast<const cursor*>(&opaque);
    CursorSnapshot s;
    s.ip                = c->dwarf.ip;
    s.cfa               = c->dwarf.cfa;
    s.locFp             = c->dwarf.loc[kLocFp].val;
    s.locLr             = c->dwarf.loc[kLocLr].val;
    s.locSp             = c->dwarf.loc[kLocSp].val;
    s.nextToSignalFrame = (c->dwarf.bitfields >> kBitNextToSignalFrame) & 1;
    s.cfaIsUnreliable   = (c->dwarf.bitfields >> kBitCfaIsUnreliable) & 1;
    s.frameType         = c->frame_info.frame_type;
    s.cfaRegSp          = c->frame_info.cfa_reg_sp;
    s.cfaRegOffset      = c->frame_info.cfa_reg_offset;
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

    void Record(EventType eventType, std::int32_t result, const unw_cursor_t& cursor)
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
