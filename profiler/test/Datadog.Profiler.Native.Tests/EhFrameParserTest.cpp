// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "gtest/gtest.h"

#include "EhFrameParser.h"
#include "StackDeltaTypes.h"

#include <cstring>
#include <link.h>
#include <vector>

// ---------------------------------------------------------------------------
// Helper: build a minimal .eh_frame in memory for testing
// ---------------------------------------------------------------------------
namespace {

class EhFrameBuilder
{
public:
    // Append a CIE. Returns offset of the CIE within the buffer.
    std::size_t AddCie(uint8_t codeAlign, int8_t dataAlign, uint8_t raRegister,
                       const std::vector<uint8_t>& initialInstructions)
    {
        std::size_t cieStart = _buf.size();
        // Placeholder for length
        PushU32(0);
        std::size_t bodyStart = _buf.size();

        PushU32(0); // CIE_id = 0 marks this as a CIE
        Push(1);    // version
        Push(0);    // augmentation string (empty)
        PushUleb128(codeAlign);
        PushSleb128(dataAlign);
        Push(raRegister); // version 1: single byte
        for (auto b : initialInstructions)
            Push(b);

        // Patch length
        uint32_t len = static_cast<uint32_t>(_buf.size() - bodyStart);
        std::memcpy(&_buf[cieStart], &len, 4);

        return cieStart;
    }

    // Append an FDE referencing the CIE at cieOffset.
    // pcBegin/pcRange are absolute 8-byte pointers (DW_EH_PE_absptr).
    void AddFde(std::size_t cieOffset, uint64_t pcBegin, uint64_t pcRange,
                const std::vector<uint8_t>& instructions)
    {
        std::size_t fdeStart = _buf.size();
        PushU32(0); // placeholder for length
        std::size_t bodyStart = _buf.size();

        // CIE pointer: offset from THIS field back to CIE start
        uint32_t ciePtr = static_cast<uint32_t>(bodyStart - cieOffset);
        PushU32(ciePtr);

        // pcBegin and pcRange as absolute 8-byte values
        PushU64(pcBegin);
        PushU64(pcRange);

        for (auto b : instructions)
            Push(b);

        uint32_t len = static_cast<uint32_t>(_buf.size() - bodyStart);
        std::memcpy(&_buf[fdeStart], &len, 4);
    }

    // Add the zero-length terminator
    void Terminate() { PushU32(0); }

    const uint8_t* Data() const { return _buf.data(); }
    std::size_t Size() const { return _buf.size(); }

private:
    void Push(uint8_t b) { _buf.push_back(b); }
    void PushU32(uint32_t v)
    {
        uint8_t bytes[4];
        std::memcpy(bytes, &v, 4);
        _buf.insert(_buf.end(), bytes, bytes + 4);
    }
    void PushU64(uint64_t v)
    {
        uint8_t bytes[8];
        std::memcpy(bytes, &v, 8);
        _buf.insert(_buf.end(), bytes, bytes + 8);
    }
    void PushUleb128(uint64_t v)
    {
        do {
            uint8_t b = v & 0x7F;
            v >>= 7;
            if (v != 0) b |= 0x80;
            _buf.push_back(b);
        } while (v != 0);
    }
    void PushSleb128(int64_t v)
    {
        bool more = true;
        while (more)
        {
            uint8_t b = v & 0x7F;
            v >>= 7;
            if ((v == 0 && (b & 0x40) == 0) || (v == -1 && (b & 0x40) != 0))
                more = false;
            else
                b |= 0x80;
            _buf.push_back(b);
        }
    }

    std::vector<uint8_t> _buf;
};

// DWARF CFA opcodes
constexpr uint8_t DW_CFA_def_cfa = 0x0C;
constexpr uint8_t DW_CFA_def_cfa_offset = 0x0E;
constexpr uint8_t DW_CFA_def_cfa_register = 0x0D;
constexpr uint8_t DW_CFA_advance_loc_1 = 0x41; // advance_loc with operand=1

// ARM64 DWARF register numbers
constexpr uint8_t kDwarfFp = 29;
constexpr uint8_t kDwarfLr = 30;
constexpr uint8_t kDwarfSp = 31;

// DW_CFA_offset encodes as (0x80 | reg), followed by ULEB128 factored offset
uint8_t CfaOffsetOpcode(uint8_t reg) { return 0x80 | reg; }

} // namespace

// ---------------------------------------------------------------------------
// Synthetic .eh_frame: canonical ARM64 frame-pointer function
//
// Simulates:
//   stp x29, x30, [sp, #-16]!   ; SP -= 16, save FP & LR
//   mov x29, sp                  ; FP = SP
//   ... body ...
//   ldp x29, x30, [sp], #16
//   ret
//
// Expected deltas:
//   [0x10000          ]: CFA=SP+0,  RA in LR (leaf-like entry)
//   [0x10000 + 4      ]: CFA=SP+16, FP at CFA-16, LR at CFA-8
//   [0x10000 + 8      ]: kUnwindInfoFramePointer (CFA=FP+16, canonical saves)
//   [0x10000 + 0x100  ]: kUnwindInfoInvalid (end marker)
// ---------------------------------------------------------------------------
TEST(EhFrameParserTest, SyntheticFramePointerFunction)
{
    EhFrameBuilder builder;

    // CIE: code_align=4, data_align=-8, RA=x30
    // Initial instructions: DW_CFA_def_cfa(SP, 0)
    auto cieOff = builder.AddCie(4, -8, kDwarfLr,
        {DW_CFA_def_cfa, kDwarfSp, 0x00});

    // FDE: function at 0x10000, length 0x100
    builder.AddFde(cieOff, 0x10000, 0x100, {
        DW_CFA_advance_loc_1,                // advance 4 bytes (1 * code_align)
        DW_CFA_def_cfa_offset, 0x10,         // CFA = SP + 16
        CfaOffsetOpcode(kDwarfFp), 0x02,     // FP at CFA + 2*(-8) = CFA-16
        CfaOffsetOpcode(kDwarfLr), 0x01,     // LR at CFA + 1*(-8) = CFA-8
        DW_CFA_advance_loc_1,                // advance 4 more bytes
        DW_CFA_def_cfa_register, kDwarfFp,   // CFA = FP + 16 (offset unchanged)
    });

    builder.Terminate();

    std::vector<StackDelta> deltas;
    bool ok = EhFrameParser::ExtractDeltasFromRaw(
        builder.Data(), builder.Size(), 0, false, deltas);

    ASSERT_TRUE(ok);
    // Expect at least 3 meaningful deltas + 1 end marker
    ASSERT_GE(deltas.size(), 3u);

    // Find the delta at function entry (0x10000)
    const StackDelta* entryDelta = nullptr;
    const StackDelta* afterStpDelta = nullptr;
    const StackDelta* afterMovDelta = nullptr;
    const StackDelta* endDelta = nullptr;

    for (const auto& d : deltas)
    {
        if (d.address == 0x10000) entryDelta = &d;
        if (d.address == 0x10004) afterStpDelta = &d;
        if (d.address == 0x10008) afterMovDelta = &d;
        if (d.address == 0x10100) endDelta = &d;
    }

    // --- Function entry: CFA = SP + 0, RA still in LR ---
    ASSERT_NE(entryDelta, nullptr) << "Missing delta at function entry 0x10000";
    EXPECT_FALSE(entryDelta->info.IsCommand());
    EXPECT_EQ(entryDelta->info.baseReg, UnwindReg::Sp);
    EXPECT_EQ(entryDelta->info.param, 0);
    EXPECT_EQ(entryDelta->info.auxBaseReg, UnwindReg::Lr)
        << "At entry, RA should still be in LR";

    // --- After stp: CFA = SP + 16, FP and LR saved ---
    ASSERT_NE(afterStpDelta, nullptr) << "Missing delta after stp at 0x10004";
    EXPECT_FALSE(afterStpDelta->info.IsCommand());
    EXPECT_EQ(afterStpDelta->info.baseReg, UnwindReg::Sp);
    EXPECT_EQ(afterStpDelta->info.param, 16);
    // RA is now saved on stack, not in LR
    EXPECT_NE(afterStpDelta->info.auxBaseReg, UnwindReg::Lr);

    // --- After mov x29, sp: canonical FP frame ---
    ASSERT_NE(afterMovDelta, nullptr) << "Missing delta after mov fp,sp at 0x10008";
    EXPECT_TRUE(afterMovDelta->info.IsCommand());
    EXPECT_EQ(afterMovDelta->info.GetCommand(), UnwindCommand::FramePointer)
        << "CFA=FP+16 with FP@CFA-16, LR@CFA-8 should be recognized as FramePointer";

    // --- End marker ---
    ASSERT_NE(endDelta, nullptr) << "Missing end-of-function marker at 0x10100";
    EXPECT_TRUE(endDelta->info.IsCommand());
    EXPECT_EQ(endDelta->info.GetCommand(), UnwindCommand::Invalid);
}

// ---------------------------------------------------------------------------
// Synthetic .eh_frame: leaf function (SP-only, no FP/LR save)
//
// Simulates:
//   sub sp, sp, #48
//   ... body ...
//   add sp, sp, #48
//   ret
//
// Expected deltas:
//   [0x20000     ]: CFA=SP+0,  RA in LR
//   [0x20000 + 4 ]: CFA=SP+48, RA in LR
//   [0x20000+0x80]: kUnwindInfoInvalid (end marker)
// ---------------------------------------------------------------------------
TEST(EhFrameParserTest, SyntheticLeafFunction)
{
    EhFrameBuilder builder;

    auto cieOff = builder.AddCie(4, -8, kDwarfLr,
        {DW_CFA_def_cfa, kDwarfSp, 0x00});

    builder.AddFde(cieOff, 0x20000, 0x80, {
        DW_CFA_advance_loc_1,         // advance 4 bytes
        DW_CFA_def_cfa_offset, 0x30,  // CFA = SP + 48
        // No register saves -- leaf function
    });

    builder.Terminate();

    std::vector<StackDelta> deltas;
    bool ok = EhFrameParser::ExtractDeltasFromRaw(
        builder.Data(), builder.Size(), 0, false, deltas);

    ASSERT_TRUE(ok);
    ASSERT_GE(deltas.size(), 2u);

    const StackDelta* entryDelta = nullptr;
    const StackDelta* afterSubDelta = nullptr;

    for (const auto& d : deltas)
    {
        if (d.address == 0x20000) entryDelta = &d;
        if (d.address == 0x20004) afterSubDelta = &d;
    }

    // Entry: CFA = SP + 0, RA in LR
    ASSERT_NE(entryDelta, nullptr);
    EXPECT_FALSE(entryDelta->info.IsCommand());
    EXPECT_EQ(entryDelta->info.baseReg, UnwindReg::Sp);
    EXPECT_EQ(entryDelta->info.param, 0);
    EXPECT_EQ(entryDelta->info.auxBaseReg, UnwindReg::Lr);

    // After sub: CFA = SP + 48, RA still in LR
    ASSERT_NE(afterSubDelta, nullptr);
    EXPECT_FALSE(afterSubDelta->info.IsCommand());
    EXPECT_EQ(afterSubDelta->info.baseReg, UnwindReg::Sp);
    EXPECT_EQ(afterSubDelta->info.param, 48);
    EXPECT_EQ(afterSubDelta->info.auxBaseReg, UnwindReg::Lr)
        << "Leaf function: RA should remain in LR";
}

// ---------------------------------------------------------------------------
// Synthetic .eh_frame: non-FP function with saved LR on stack
//
// Simulates a function that saves LR but does NOT set up a frame pointer:
//   str x30, [sp, #-16]!   ; SP -= 16, save LR
//   ... body ...
//   ldr x30, [sp], #16
//   ret
//
// Expected: CFA=SP+16, RA saved at CFA-8 (not in LR, not FramePointer command)
// ---------------------------------------------------------------------------
TEST(EhFrameParserTest, SyntheticSavedLrNoFramePointer)
{
    EhFrameBuilder builder;

    auto cieOff = builder.AddCie(4, -8, kDwarfLr,
        {DW_CFA_def_cfa, kDwarfSp, 0x00});

    builder.AddFde(cieOff, 0x30000, 0x40, {
        DW_CFA_advance_loc_1,
        DW_CFA_def_cfa_offset, 0x10,         // CFA = SP + 16
        CfaOffsetOpcode(kDwarfLr), 0x01,     // LR at CFA + 1*(-8) = CFA-8
        // No FP save, no def_cfa_register(FP) → NOT a FramePointer command
    });

    builder.Terminate();

    std::vector<StackDelta> deltas;
    bool ok = EhFrameParser::ExtractDeltasFromRaw(
        builder.Data(), builder.Size(), 0, false, deltas);

    ASSERT_TRUE(ok);

    const StackDelta* afterStr = nullptr;
    for (const auto& d : deltas)
    {
        if (d.address == 0x30004) afterStr = &d;
    }

    ASSERT_NE(afterStr, nullptr);
    // Must NOT be FramePointer (FP not saved, CFA not FP-based)
    EXPECT_FALSE(afterStr->info.IsCommand())
        << "SP-based frame with only LR saved should not be a command";
    EXPECT_EQ(afterStr->info.baseReg, UnwindReg::Sp);
    EXPECT_EQ(afterStr->info.param, 16);
    // RA is saved on stack, not in LR
    EXPECT_NE(afterStr->info.auxBaseReg, UnwindReg::Lr);
}

// ---------------------------------------------------------------------------
// Live: parse .eh_frame from loaded libraries and verify basic correctness
// ---------------------------------------------------------------------------
TEST(EhFrameParserTest, LiveParsing_ExtractsNonEmptyDeltasFromLoadedLibraries)
{
    struct CallbackData
    {
        int parsedCount = 0;
        int totalModules = 0;
        std::size_t totalDeltas = 0;
    };

    CallbackData data;

    dl_iterate_phdr(
        [](struct dl_phdr_info* info, std::size_t /*size*/, void* userData) -> int {
            auto* d = static_cast<CallbackData*>(userData);
            d->totalModules++;

            std::vector<StackDelta> deltas;
            if (EhFrameParser::ExtractDeltas(info, deltas) && !deltas.empty())
            {
                d->parsedCount++;
                d->totalDeltas += deltas.size();

                for (std::size_t i = 1; i < deltas.size(); ++i)
                {
                    EXPECT_GE(deltas[i].address, deltas[i - 1].address)
                        << "Deltas not sorted at index " << i
                        << " for module '" << (info->dlpi_name ? info->dlpi_name : "<main>") << "'";
                }
            }
            return 0;
        },
        &data);

    EXPECT_GT(data.totalModules, 0);
    EXPECT_GT(data.parsedCount, 0)
        << "Expected to parse .eh_frame from at least one loaded library";
    EXPECT_GT(data.totalDeltas, 0u);
}

TEST(EhFrameParserTest, NullInfoReturnsFalse)
{
    std::vector<StackDelta> deltas;
    EXPECT_FALSE(EhFrameParser::ExtractDeltas(nullptr, deltas));
    EXPECT_TRUE(deltas.empty());
}
