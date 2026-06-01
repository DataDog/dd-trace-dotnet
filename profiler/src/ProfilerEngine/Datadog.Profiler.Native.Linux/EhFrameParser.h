// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#pragma once

#include "StackDeltaTypes.h"

#include <cstddef>
#include <cstdint>
#include <vector>

struct dl_phdr_info;

// Parses .eh_frame / .eh_frame_hdr ELF sections and produces a sorted
// array of StackDelta entries suitable for signal-safe binary-search
// unwinding.
//
// This replaces the per-step DWARF evaluation that libunwind does inside
// unw_step() with a one-time offline extraction, inspired by the
// OpenTelemetry eBPF profiler's stack delta mechanism.
//
// Thread safety: not thread-safe. Intended for use from a single
// background thread (LibrariesInfoCache worker).
class EhFrameParser
{
public:
    // Extract stack deltas for a single loaded ELF module.
    // info: dl_phdr_info from dl_iterate_phdr
    // outDeltas: appended with StackDelta entries for this module,
    //            sorted by address on return
    // Returns true if at least one delta was extracted.
    static bool ExtractDeltas(const dl_phdr_info* info, std::vector<StackDelta>& outDeltas);

#ifdef DD_TEST
    // Test-only: parse from raw .eh_frame bytes at a given load bias.
    static bool ExtractDeltasFromRaw(
        const std::uint8_t* ehFrameData, std::size_t ehFrameSize,
        std::uintptr_t loadBias, bool is64Bit,
        std::vector<StackDelta>& outDeltas);
#endif

private:
    struct CieInfo
    {
        std::int64_t codeAlignFactor;
        std::int64_t dataAlignFactor;
        std::uint64_t returnAddressRegister;
        const std::uint8_t* initialInstructions;
        std::size_t initialInstructionsLen;
        bool isSignalFrame;
        std::uint8_t fdePointerEncoding;
        std::uint8_t lsdaEncoding;
        bool hasAugmentation;
    };

    struct VmRegs
    {
        UnwindReg cfaReg = UnwindReg::Sp;
        std::int32_t cfaOffset = 0;
        std::int32_t fpOffset = 0;       // offset of saved FP from CFA (0 = not saved)
        bool fpSaved = false;
        std::int32_t raOffset = 0;       // offset of saved RA from CFA (0 = not saved)
        bool raSaved = false;
    };

    static bool ParseCie(const std::uint8_t* data, std::size_t length,
                         bool is64Bit, CieInfo& outCie);

    static bool ParseFde(const std::uint8_t* data, std::size_t length,
                         const CieInfo& cie, std::uintptr_t fdeBase,
                         bool is64Bit, std::uintptr_t ehFrameAddr,
                         std::vector<StackDelta>& outDeltas);

    static bool ExecuteCfi(const std::uint8_t* instructions, std::size_t length,
                           const CieInfo& cie, std::uintptr_t pcBegin, std::uintptr_t pcEnd,
                           VmRegs initialRegs,
                           std::vector<StackDelta>& outDeltas);

    static UnwindInfo VmRegsToUnwindInfo(const VmRegs& regs);

    static std::uint64_t ReadUleb128(const std::uint8_t*& p, const std::uint8_t* end);
    static std::int64_t ReadSleb128(const std::uint8_t*& p, const std::uint8_t* end);
    static std::uintptr_t ReadEncodedPointer(const std::uint8_t*& p, const std::uint8_t* end,
                                             std::uint8_t encoding, std::uintptr_t base);
};
