// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include <cstdint>

// Register IDs for CFA and return-address recovery expressions.
// Kept minimal for ARM64; extend for x86_64 if needed later.
enum class UnwindReg : std::uint8_t
{
    Invalid = 0,
    Sp = 1,
    Fp = 2,
    Lr = 3,
};

enum class UnwindFlags : std::uint8_t
{
    None = 0,
    Command = 1 << 0,
    Frame = 1 << 1,
};

inline UnwindFlags operator|(UnwindFlags a, UnwindFlags b)
{
    return static_cast<UnwindFlags>(static_cast<uint8_t>(a) | static_cast<uint8_t>(b));
}

inline bool operator&(UnwindFlags a, UnwindFlags b)
{
    return (static_cast<uint8_t>(a) & static_cast<uint8_t>(b)) != 0;
}

// Command opcodes stored in UnwindInfo::param when flags contains Command.
enum class UnwindCommand : std::int32_t
{
    Invalid = 0,
    Stop = 1,
    Signal = 3,
    FramePointer = 4,
};

// Describes how to unwind one native frame given registers at a PC.
//
// Normal case (flags == None):
//   CFA      = regs[baseReg] + param
//   prev_PC  = *(CFA - 8)            (or regs[auxBaseReg] if auxBaseReg == LR)
//   prev_FP  = *(regs[auxBaseReg] + auxParam)  if auxParam != 0
//   prev_SP  = CFA
//
// Command case (flags & Command):
//   Interpret param as UnwindCommand.
struct UnwindInfo
{
    UnwindFlags flags = UnwindFlags::None;
    UnwindReg baseReg = UnwindReg::Sp;
    UnwindReg auxBaseReg = UnwindReg::Invalid;
    std::uint8_t _reserved = 0;
    std::int32_t param = 0;
    std::int32_t auxParam = 0;

    bool IsCommand() const { return flags & UnwindFlags::Command; }
    UnwindCommand GetCommand() const { return static_cast<UnwindCommand>(param); }
};

static_assert(sizeof(UnwindInfo) == 12, "UnwindInfo should be 12 bytes");

// One interval boundary in a sorted delta table.
// Two consecutive entries define an interval: PC in [deltas[i].address, deltas[i+1].address)
// uses deltas[i].info.
struct StackDelta
{
    std::uint64_t address;
    UnwindInfo info;
};

// Pre-built command UnwindInfo constants
inline constexpr UnwindInfo kUnwindInfoStop = {UnwindFlags::Command, UnwindReg::Invalid, UnwindReg::Invalid, 0,
                                               static_cast<std::int32_t>(UnwindCommand::Stop), 0};

inline constexpr UnwindInfo kUnwindInfoInvalid = {UnwindFlags::Command, UnwindReg::Invalid, UnwindReg::Invalid, 0,
                                                  static_cast<std::int32_t>(UnwindCommand::Invalid), 0};

inline constexpr UnwindInfo kUnwindInfoSignal = {UnwindFlags::Command, UnwindReg::Invalid, UnwindReg::Invalid, 0,
                                                 static_cast<std::int32_t>(UnwindCommand::Signal), 0};

inline constexpr UnwindInfo kUnwindInfoFramePointer = {UnwindFlags::Command, UnwindReg::Invalid, UnwindReg::Invalid, 0,
                                                       static_cast<std::int32_t>(UnwindCommand::FramePointer), 0};
