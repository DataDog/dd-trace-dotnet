// Unless explicitly stated otherwise all files in this repository are licensed under the Apache 2 License.
// This product includes software developed at Datadog (https://www.datadoghq.com/). Copyright 2022 Datadog, Inc.

#include "EhFrameParser.h"

#include <algorithm>
#include <cstring>
#include <elf.h>
#include <link.h>

// DWARF CFA opcodes (high 2 bits = primary opcode, low 6 bits = operand)
static constexpr uint8_t DW_CFA_advance_loc_mask = 0x40;
static constexpr uint8_t DW_CFA_offset_mask = 0x80;
static constexpr uint8_t DW_CFA_restore_mask = 0xC0;
static constexpr uint8_t DW_CFA_primary_mask = 0xC0;

static constexpr uint8_t DW_CFA_nop = 0x00;
static constexpr uint8_t DW_CFA_set_loc = 0x01;
static constexpr uint8_t DW_CFA_advance_loc1 = 0x02;
static constexpr uint8_t DW_CFA_advance_loc2 = 0x03;
static constexpr uint8_t DW_CFA_advance_loc4 = 0x04;
static constexpr uint8_t DW_CFA_offset_extended = 0x05;
static constexpr uint8_t DW_CFA_restore_extended = 0x06;
static constexpr uint8_t DW_CFA_undefined = 0x07;
static constexpr uint8_t DW_CFA_same_value = 0x08;
static constexpr uint8_t DW_CFA_register = 0x09;
static constexpr uint8_t DW_CFA_remember_state = 0x0A;
static constexpr uint8_t DW_CFA_restore_state = 0x0B;
static constexpr uint8_t DW_CFA_def_cfa = 0x0C;
static constexpr uint8_t DW_CFA_def_cfa_register = 0x0D;
static constexpr uint8_t DW_CFA_def_cfa_offset = 0x0E;
static constexpr uint8_t DW_CFA_def_cfa_expression = 0x0F;
static constexpr uint8_t DW_CFA_expression = 0x10;
static constexpr uint8_t DW_CFA_offset_extended_sf = 0x11;
static constexpr uint8_t DW_CFA_def_cfa_sf = 0x12;
static constexpr uint8_t DW_CFA_def_cfa_offset_sf = 0x13;
static constexpr uint8_t DW_CFA_val_offset = 0x14;
static constexpr uint8_t DW_CFA_val_offset_sf = 0x15;
static constexpr uint8_t DW_CFA_val_expression = 0x16;
static constexpr uint8_t DW_CFA_GNU_args_size = 0x2E;
static constexpr uint8_t DW_CFA_GNU_negative_offset_extended = 0x2F;

// ARM64 DWARF register numbers
static constexpr uint64_t kDwarfRegFp = 29;  // x29
static constexpr uint64_t kDwarfRegLr = 30;  // x30
static constexpr uint64_t kDwarfRegSp = 31;

// .eh_frame pointer encoding formats
static constexpr uint8_t DW_EH_PE_absptr = 0x00;
static constexpr uint8_t DW_EH_PE_uleb128 = 0x01;
static constexpr uint8_t DW_EH_PE_udata2 = 0x02;
static constexpr uint8_t DW_EH_PE_udata4 = 0x03;
static constexpr uint8_t DW_EH_PE_udata8 = 0x04;
static constexpr uint8_t DW_EH_PE_sleb128 = 0x09;
static constexpr uint8_t DW_EH_PE_sdata2 = 0x0A;
static constexpr uint8_t DW_EH_PE_sdata4 = 0x0B;
static constexpr uint8_t DW_EH_PE_sdata8 = 0x0C;

static constexpr uint8_t DW_EH_PE_pcrel = 0x10;
static constexpr uint8_t DW_EH_PE_textrel = 0x20;
static constexpr uint8_t DW_EH_PE_datarel = 0x30;
static constexpr uint8_t DW_EH_PE_funcrel = 0x40;
static constexpr uint8_t DW_EH_PE_aligned = 0x50;

static constexpr uint8_t DW_EH_PE_indirect = 0x80;
static constexpr uint8_t DW_EH_PE_omit = 0xFF;

static constexpr uint8_t DW_EH_PE_FORMAT_MASK = 0x0F;
static constexpr uint8_t DW_EH_PE_APPL_MASK = 0x70;

template <typename T>
static T ReadValue(const uint8_t*& p, const uint8_t* end)
{
    if (p + sizeof(T) > end)
    {
        p = end;
        return 0;
    }
    T val;
    std::memcpy(&val, p, sizeof(T));
    p += sizeof(T);
    return val;
}

uint64_t EhFrameParser::ReadUleb128(const uint8_t*& p, const uint8_t* end)
{
    uint64_t result = 0;
    unsigned shift = 0;
    while (p < end)
    {
        uint8_t byte = *p++;
        result |= static_cast<uint64_t>(byte & 0x7F) << shift;
        if ((byte & 0x80) == 0)
            break;
        shift += 7;
        if (shift >= 64)
            break;
    }
    return result;
}

int64_t EhFrameParser::ReadSleb128(const uint8_t*& p, const uint8_t* end)
{
    int64_t result = 0;
    unsigned shift = 0;
    uint8_t byte = 0;
    while (p < end)
    {
        byte = *p++;
        result |= static_cast<int64_t>(byte & 0x7F) << shift;
        shift += 7;
        if ((byte & 0x80) == 0)
            break;
        if (shift >= 64)
            break;
    }
    if (shift < 64 && (byte & 0x40))
        result |= -(static_cast<int64_t>(1) << shift);
    return result;
}

uintptr_t EhFrameParser::ReadEncodedPointer(const uint8_t*& p, const uint8_t* end,
                                             uint8_t encoding, uintptr_t base)
{
    if (encoding == DW_EH_PE_omit)
        return 0;

    uintptr_t result = 0;
    const uint8_t* startPos = p;

    switch (encoding & DW_EH_PE_FORMAT_MASK)
    {
        case DW_EH_PE_absptr:
            result = ReadValue<uintptr_t>(p, end);
            break;
        case DW_EH_PE_uleb128:
            result = ReadUleb128(p, end);
            break;
        case DW_EH_PE_udata2:
            result = ReadValue<uint16_t>(p, end);
            break;
        case DW_EH_PE_udata4:
            result = ReadValue<uint32_t>(p, end);
            break;
        case DW_EH_PE_udata8:
            result = ReadValue<uint64_t>(p, end);
            break;
        case DW_EH_PE_sleb128:
            result = static_cast<uintptr_t>(ReadSleb128(p, end));
            break;
        case DW_EH_PE_sdata2:
            result = static_cast<uintptr_t>(ReadValue<int16_t>(p, end));
            break;
        case DW_EH_PE_sdata4:
            result = static_cast<uintptr_t>(ReadValue<int32_t>(p, end));
            break;
        case DW_EH_PE_sdata8:
            result = static_cast<uintptr_t>(ReadValue<int64_t>(p, end));
            break;
        default:
            return 0;
    }

    if (result == 0)
        return 0;

    switch (encoding & DW_EH_PE_APPL_MASK)
    {
        case 0:
            break;
        case DW_EH_PE_pcrel:
            result += reinterpret_cast<uintptr_t>(startPos);
            break;
        case DW_EH_PE_datarel:
            result += base;
            break;
        default:
            return 0;
    }

    return result;
}

bool EhFrameParser::ParseCie(const uint8_t* data, size_t length,
                              bool /*is64Bit*/, CieInfo& outCie)
{
    const uint8_t* p = data;
    const uint8_t* end = data + length;

    outCie = {};
    outCie.codeAlignFactor = 1;
    outCie.dataAlignFactor = 1;
    outCie.fdePointerEncoding = DW_EH_PE_absptr;
    outCie.lsdaEncoding = DW_EH_PE_omit;

    // Version
    if (p >= end)
        return false;
    uint8_t version = *p++;
    if (version != 1 && version != 3)
        return false;

    // Augmentation string
    const char* augStr = reinterpret_cast<const char*>(p);
    size_t augLen = strnlen(augStr, end - p);
    if (p + augLen >= end)
        return false;
    p += augLen + 1; // skip null terminator

    outCie.codeAlignFactor = static_cast<int64_t>(ReadUleb128(p, end));
    outCie.dataAlignFactor = ReadSleb128(p, end);

    if (version == 1)
    {
        if (p >= end) return false;
        outCie.returnAddressRegister = *p++;
    }
    else
    {
        outCie.returnAddressRegister = ReadUleb128(p, end);
    }

    // Parse augmentation data if present
    if (augLen > 0 && augStr[0] == 'z')
    {
        outCie.hasAugmentation = true;
        uint64_t augDataLen = ReadUleb128(p, end);
        const uint8_t* augEnd = p + augDataLen;
        if (augEnd > end)
            return false;

        for (size_t i = 1; i < augLen + 1 && augStr[i] != '\0'; ++i)
        {
            switch (augStr[i])
            {
                case 'R':
                    if (p < augEnd)
                        outCie.fdePointerEncoding = *p++;
                    break;
                case 'P':
                {
                    if (p < augEnd)
                    {
                        uint8_t enc = *p++;
                        // Skip the personality pointer
                        ReadEncodedPointer(p, augEnd, enc, 0);
                    }
                    break;
                }
                case 'L':
                    if (p < augEnd)
                        outCie.lsdaEncoding = *p++;
                    break;
                case 'S':
                    outCie.isSignalFrame = true;
                    break;
                default:
                    break;
            }
        }
        p = augEnd;
    }

    outCie.initialInstructions = p;
    outCie.initialInstructionsLen = static_cast<size_t>(end - p);

    return true;
}

bool EhFrameParser::ParseFde(const uint8_t* data, size_t length,
                              const CieInfo& cie, uintptr_t /*fdeBase*/,
                              bool /*is64Bit*/, uintptr_t ehFrameAddr,
                              std::vector<StackDelta>& outDeltas)
{
    const uint8_t* p = data;
    const uint8_t* end = data + length;

    // PC begin and range - for .eh_frame, addresses are pcrel by default
    uint8_t ptrEnc = cie.fdePointerEncoding;
    uintptr_t pcBegin = ReadEncodedPointer(p, end, ptrEnc, ehFrameAddr);
    // Range is always the same format but without the application modifier (pcrel etc.)
    uint8_t rangeEnc = ptrEnc & DW_EH_PE_FORMAT_MASK;
    uintptr_t pcRange = ReadEncodedPointer(p, end, rangeEnc, 0);

    if (pcBegin == 0 || pcRange == 0)
        return false;

    uintptr_t pcEnd = pcBegin + pcRange;

    // Skip augmentation data length if present
    if (cie.hasAugmentation)
    {
        uint64_t augLen = ReadUleb128(p, end);
        p += augLen;
        if (p > end)
            return false;
    }

    // Execute initial CIE instructions to establish baseline register state
    VmRegs initialRegs;
    ExecuteCfi(cie.initialInstructions, cie.initialInstructionsLen,
               cie, pcBegin, pcEnd, VmRegs{}, outDeltas);

    // Now we have the CIE-default register state; the FDE instructions refine it.
    // Re-parse to just get the initial state (not emit deltas), then parse FDE.
    VmRegs cieRegs;
    {
        const uint8_t* ip = cie.initialInstructions;
        const uint8_t* ie = ip + cie.initialInstructionsLen;

        while (ip < ie)
        {
            uint8_t opcode = *ip++;
            uint8_t primary = opcode & DW_CFA_primary_mask;
            uint8_t operand = opcode & ~DW_CFA_primary_mask;

            if (primary == DW_CFA_offset_mask)
            {
                int64_t off = static_cast<int64_t>(ReadUleb128(ip, ie)) * cie.dataAlignFactor;
                uint64_t reg = operand;
                if (reg == kDwarfRegFp) { cieRegs.fpOffset = static_cast<int32_t>(off); cieRegs.fpSaved = true; }
                if (reg == kDwarfRegLr || reg == cie.returnAddressRegister) { cieRegs.raOffset = static_cast<int32_t>(off); cieRegs.raSaved = true; }
            }
            else if (primary == DW_CFA_advance_loc_mask)
            {
                // skip
            }
            else if (primary == DW_CFA_restore_mask)
            {
                // skip
            }
            else
            {
                switch (opcode)
                {
                    case DW_CFA_def_cfa:
                    {
                        uint64_t reg = ReadUleb128(ip, ie);
                        uint64_t off = ReadUleb128(ip, ie);
                        cieRegs.cfaReg = (reg == kDwarfRegFp) ? UnwindReg::Fp : UnwindReg::Sp;
                        cieRegs.cfaOffset = static_cast<int32_t>(off);
                        break;
                    }
                    case DW_CFA_def_cfa_register:
                    {
                        uint64_t reg = ReadUleb128(ip, ie);
                        cieRegs.cfaReg = (reg == kDwarfRegFp) ? UnwindReg::Fp : UnwindReg::Sp;
                        break;
                    }
                    case DW_CFA_def_cfa_offset:
                        cieRegs.cfaOffset = static_cast<int32_t>(ReadUleb128(ip, ie));
                        break;
                    case DW_CFA_def_cfa_sf:
                    {
                        uint64_t reg = ReadUleb128(ip, ie);
                        int64_t off = ReadSleb128(ip, ie) * cie.dataAlignFactor;
                        cieRegs.cfaReg = (reg == kDwarfRegFp) ? UnwindReg::Fp : UnwindReg::Sp;
                        cieRegs.cfaOffset = static_cast<int32_t>(off);
                        break;
                    }
                    case DW_CFA_def_cfa_offset_sf:
                        cieRegs.cfaOffset = static_cast<int32_t>(ReadSleb128(ip, ie) * cie.dataAlignFactor);
                        break;
                    case DW_CFA_offset_extended:
                    {
                        uint64_t reg = ReadUleb128(ip, ie);
                        int64_t off = static_cast<int64_t>(ReadUleb128(ip, ie)) * cie.dataAlignFactor;
                        if (reg == kDwarfRegFp) { cieRegs.fpOffset = static_cast<int32_t>(off); cieRegs.fpSaved = true; }
                        if (reg == kDwarfRegLr || reg == cie.returnAddressRegister) { cieRegs.raOffset = static_cast<int32_t>(off); cieRegs.raSaved = true; }
                        break;
                    }
                    case DW_CFA_offset_extended_sf:
                    {
                        uint64_t reg = ReadUleb128(ip, ie);
                        int64_t off = ReadSleb128(ip, ie) * cie.dataAlignFactor;
                        if (reg == kDwarfRegFp) { cieRegs.fpOffset = static_cast<int32_t>(off); cieRegs.fpSaved = true; }
                        if (reg == kDwarfRegLr || reg == cie.returnAddressRegister) { cieRegs.raOffset = static_cast<int32_t>(off); cieRegs.raSaved = true; }
                        break;
                    }
                    case DW_CFA_nop:
                    case DW_CFA_remember_state:
                    case DW_CFA_restore_state:
                        break;
                    default:
                        // Skip other opcodes with their operands
                        switch (opcode)
                        {
                            case DW_CFA_advance_loc1: ip += 1; break;
                            case DW_CFA_advance_loc2: ip += 2; break;
                            case DW_CFA_advance_loc4: ip += 4; break;
                            case DW_CFA_set_loc: ip += sizeof(uintptr_t); break;
                            case DW_CFA_undefined:
                            case DW_CFA_same_value:
                            case DW_CFA_restore_extended:
                                ReadUleb128(ip, ie); break;
                            case DW_CFA_register:
                                ReadUleb128(ip, ie); ReadUleb128(ip, ie); break;
                            case DW_CFA_val_offset:
                                ReadUleb128(ip, ie); ReadUleb128(ip, ie); break;
                            case DW_CFA_val_offset_sf:
                                ReadUleb128(ip, ie); ReadSleb128(ip, ie); break;
                            case DW_CFA_def_cfa_expression:
                            case DW_CFA_expression:
                            case DW_CFA_val_expression:
                            {
                                if (opcode != DW_CFA_def_cfa_expression)
                                    ReadUleb128(ip, ie);
                                uint64_t blockLen = ReadUleb128(ip, ie);
                                ip += blockLen;
                                break;
                            }
                            case DW_CFA_GNU_args_size:
                                ReadUleb128(ip, ie); break;
                            case DW_CFA_GNU_negative_offset_extended:
                                ReadUleb128(ip, ie); ReadUleb128(ip, ie); break;
                            default:
                                break;
                        }
                        break;
                }
            }
        }
    }

    if (cie.isSignalFrame)
    {
        StackDelta sd;
        sd.address = pcBegin;
        sd.info = kUnwindInfoSignal;
        outDeltas.push_back(sd);

        StackDelta endSd;
        endSd.address = pcEnd;
        endSd.info = kUnwindInfoInvalid;
        outDeltas.push_back(endSd);
        return true;
    }

    // Execute FDE instructions
    return ExecuteCfi(p, static_cast<size_t>(end - p),
                      cie, pcBegin, pcEnd, cieRegs, outDeltas);
}

bool EhFrameParser::ExecuteCfi(const uint8_t* instructions, size_t length,
                                const CieInfo& cie, uintptr_t pcBegin, uintptr_t pcEnd,
                                VmRegs initialRegs,
                                std::vector<StackDelta>& outDeltas)
{
    VmRegs regs = initialRegs;
    VmRegs savedRegs = initialRegs;
    uintptr_t currentPc = pcBegin;

    // Emit initial delta at function start
    StackDelta startDelta;
    startDelta.address = pcBegin;
    startDelta.info = VmRegsToUnwindInfo(regs);
    outDeltas.push_back(startDelta);

    UnwindInfo lastInfo = startDelta.info;

    const uint8_t* ip = instructions;
    const uint8_t* ie = instructions + length;

    while (ip < ie && currentPc < pcEnd)
    {
        uint8_t opcode = *ip++;
        uint8_t primary = opcode & DW_CFA_primary_mask;
        uint8_t operand = opcode & ~DW_CFA_primary_mask;

        if (primary == DW_CFA_advance_loc_mask)
        {
            currentPc += static_cast<uintptr_t>(operand) * static_cast<uintptr_t>(cie.codeAlignFactor);
        }
        else if (primary == DW_CFA_offset_mask)
        {
            uint64_t reg = operand;
            int64_t off = static_cast<int64_t>(ReadUleb128(ip, ie)) * cie.dataAlignFactor;
            if (reg == kDwarfRegFp) { regs.fpOffset = static_cast<int32_t>(off); regs.fpSaved = true; }
            if (reg == kDwarfRegLr || reg == cie.returnAddressRegister) { regs.raOffset = static_cast<int32_t>(off); regs.raSaved = true; }
        }
        else if (primary == DW_CFA_restore_mask)
        {
            uint64_t reg = operand;
            if (reg == kDwarfRegFp) { regs.fpOffset = initialRegs.fpOffset; regs.fpSaved = initialRegs.fpSaved; }
            if (reg == kDwarfRegLr || reg == cie.returnAddressRegister) { regs.raOffset = initialRegs.raOffset; regs.raSaved = initialRegs.raSaved; }
        }
        else
        {
            switch (opcode)
            {
                case DW_CFA_set_loc:
                    currentPc = ReadValue<uintptr_t>(ip, ie);
                    break;
                case DW_CFA_advance_loc1:
                    currentPc += static_cast<uintptr_t>(ReadValue<uint8_t>(ip, ie)) * static_cast<uintptr_t>(cie.codeAlignFactor);
                    break;
                case DW_CFA_advance_loc2:
                    currentPc += static_cast<uintptr_t>(ReadValue<uint16_t>(ip, ie)) * static_cast<uintptr_t>(cie.codeAlignFactor);
                    break;
                case DW_CFA_advance_loc4:
                    currentPc += static_cast<uintptr_t>(ReadValue<uint32_t>(ip, ie)) * static_cast<uintptr_t>(cie.codeAlignFactor);
                    break;
                case DW_CFA_def_cfa:
                {
                    uint64_t reg = ReadUleb128(ip, ie);
                    uint64_t off = ReadUleb128(ip, ie);
                    regs.cfaReg = (reg == kDwarfRegFp) ? UnwindReg::Fp : UnwindReg::Sp;
                    regs.cfaOffset = static_cast<int32_t>(off);
                    break;
                }
                case DW_CFA_def_cfa_sf:
                {
                    uint64_t reg = ReadUleb128(ip, ie);
                    int64_t off = ReadSleb128(ip, ie) * cie.dataAlignFactor;
                    regs.cfaReg = (reg == kDwarfRegFp) ? UnwindReg::Fp : UnwindReg::Sp;
                    regs.cfaOffset = static_cast<int32_t>(off);
                    break;
                }
                case DW_CFA_def_cfa_register:
                {
                    uint64_t reg = ReadUleb128(ip, ie);
                    regs.cfaReg = (reg == kDwarfRegFp) ? UnwindReg::Fp : UnwindReg::Sp;
                    break;
                }
                case DW_CFA_def_cfa_offset:
                    regs.cfaOffset = static_cast<int32_t>(ReadUleb128(ip, ie));
                    break;
                case DW_CFA_def_cfa_offset_sf:
                    regs.cfaOffset = static_cast<int32_t>(ReadSleb128(ip, ie) * cie.dataAlignFactor);
                    break;
                case DW_CFA_offset_extended:
                {
                    uint64_t reg = ReadUleb128(ip, ie);
                    int64_t off = static_cast<int64_t>(ReadUleb128(ip, ie)) * cie.dataAlignFactor;
                    if (reg == kDwarfRegFp) { regs.fpOffset = static_cast<int32_t>(off); regs.fpSaved = true; }
                    if (reg == kDwarfRegLr || reg == cie.returnAddressRegister) { regs.raOffset = static_cast<int32_t>(off); regs.raSaved = true; }
                    break;
                }
                case DW_CFA_offset_extended_sf:
                {
                    uint64_t reg = ReadUleb128(ip, ie);
                    int64_t off = ReadSleb128(ip, ie) * cie.dataAlignFactor;
                    if (reg == kDwarfRegFp) { regs.fpOffset = static_cast<int32_t>(off); regs.fpSaved = true; }
                    if (reg == kDwarfRegLr || reg == cie.returnAddressRegister) { regs.raOffset = static_cast<int32_t>(off); regs.raSaved = true; }
                    break;
                }
                case DW_CFA_undefined:
                case DW_CFA_same_value:
                {
                    uint64_t reg = ReadUleb128(ip, ie);
                    if (reg == kDwarfRegFp) { regs.fpSaved = false; }
                    if (reg == kDwarfRegLr || reg == cie.returnAddressRegister) { regs.raSaved = false; }
                    break;
                }
                case DW_CFA_restore_extended:
                {
                    uint64_t reg = ReadUleb128(ip, ie);
                    if (reg == kDwarfRegFp) { regs.fpOffset = initialRegs.fpOffset; regs.fpSaved = initialRegs.fpSaved; }
                    if (reg == kDwarfRegLr || reg == cie.returnAddressRegister) { regs.raOffset = initialRegs.raOffset; regs.raSaved = initialRegs.raSaved; }
                    break;
                }
                case DW_CFA_register:
                {
                    ReadUleb128(ip, ie);
                    ReadUleb128(ip, ie);
                    break;
                }
                case DW_CFA_remember_state:
                    savedRegs = regs;
                    break;
                case DW_CFA_restore_state:
                    regs = savedRegs;
                    break;
                case DW_CFA_nop:
                    break;
                case DW_CFA_GNU_args_size:
                    ReadUleb128(ip, ie);
                    break;
                case DW_CFA_GNU_negative_offset_extended:
                {
                    uint64_t reg = ReadUleb128(ip, ie);
                    int64_t off = -static_cast<int64_t>(ReadUleb128(ip, ie)) * cie.dataAlignFactor;
                    if (reg == kDwarfRegFp) { regs.fpOffset = static_cast<int32_t>(off); regs.fpSaved = true; }
                    if (reg == kDwarfRegLr || reg == cie.returnAddressRegister) { regs.raOffset = static_cast<int32_t>(off); regs.raSaved = true; }
                    break;
                }
                case DW_CFA_def_cfa_expression:
                case DW_CFA_val_expression:
                {
                    if (opcode == DW_CFA_val_expression)
                        ReadUleb128(ip, ie);
                    uint64_t blockLen = ReadUleb128(ip, ie);
                    ip += blockLen;
                    break;
                }
                case DW_CFA_expression:
                {
                    ReadUleb128(ip, ie);
                    uint64_t blockLen = ReadUleb128(ip, ie);
                    ip += blockLen;
                    break;
                }
                case DW_CFA_val_offset:
                    ReadUleb128(ip, ie); ReadUleb128(ip, ie);
                    break;
                case DW_CFA_val_offset_sf:
                    ReadUleb128(ip, ie); ReadSleb128(ip, ie);
                    break;
                default:
                    break;
            }
        }

        // After each location-advancing opcode, check if the unwind info changed
        UnwindInfo newInfo = VmRegsToUnwindInfo(regs);
        if (std::memcmp(&newInfo, &lastInfo, sizeof(UnwindInfo)) != 0 && currentPc < pcEnd)
        {
            StackDelta sd;
            sd.address = currentPc;
            sd.info = newInfo;
            outDeltas.push_back(sd);
            lastInfo = newInfo;
        }
    }

    // End-of-function marker
    StackDelta endDelta;
    endDelta.address = pcEnd;
    endDelta.info = kUnwindInfoInvalid;
    outDeltas.push_back(endDelta);

    return true;
}

UnwindInfo EhFrameParser::VmRegsToUnwindInfo(const VmRegs& regs)
{
    UnwindInfo info;
    info.flags = UnwindFlags::None;
    info.baseReg = regs.cfaReg;
    info.param = regs.cfaOffset;
    info._reserved = 0;

    // On ARM64, if CFA is FP-based and both FP and LR are saved at canonical
    // positions (FP at CFA-16, LR at CFA-8), this is a standard frame-pointer
    // frame. Emit the compact FramePointer command.
    if (regs.cfaReg == UnwindReg::Fp && regs.cfaOffset == 16 &&
        regs.fpSaved && regs.fpOffset == -16 &&
        regs.raSaved && regs.raOffset == -8)
    {
        return kUnwindInfoFramePointer;
    }

    // RA recovery
    if (regs.raSaved)
    {
        // RA is at CFA + raOffset. auxBaseReg/auxParam encode the RA location
        // relative to CFA (base = CFA register, offset includes both CFA offset and RA offset).
        info.auxBaseReg = regs.cfaReg;
        info.auxParam = regs.cfaOffset + regs.raOffset;
    }
    else
    {
        // RA is still in LR (leaf function or not yet saved)
        info.auxBaseReg = UnwindReg::Lr;
        info.auxParam = 0;
    }

    return info;
}

bool EhFrameParser::ExtractDeltas(const dl_phdr_info* info, std::vector<StackDelta>& outDeltas)
{
    if (info == nullptr)
        return false;

    // Find .eh_frame_hdr and .eh_frame program headers
    uintptr_t ehFrameHdrAddr = 0;
    uintptr_t ehFrameHdrSize = 0;
    uintptr_t ehFrameAddr = 0;

    for (int i = 0; i < info->dlpi_phnum; ++i)
    {
        const auto& phdr = info->dlpi_phdr[i];
        if (phdr.p_type == PT_GNU_EH_FRAME)
        {
            ehFrameHdrAddr = info->dlpi_addr + phdr.p_vaddr;
            ehFrameHdrSize = phdr.p_memsz;
        }
    }

    if (ehFrameHdrAddr == 0 || ehFrameHdrSize < 4)
        return false;

    // Parse .eh_frame_hdr to find the .eh_frame section
    auto* hdr = reinterpret_cast<const uint8_t*>(ehFrameHdrAddr);
    const uint8_t* hdrEnd = hdr + ehFrameHdrSize;

    uint8_t hdrVersion = hdr[0];
    if (hdrVersion != 1)
        return false;

    uint8_t ehFramePtrEnc = hdr[1];
    // uint8_t fdeCountEnc = hdr[2]; // encoding of FDE count
    // uint8_t tableEnc = hdr[3]; // encoding of binary search table entries

    const uint8_t* p = hdr + 4;
    ehFrameAddr = ReadEncodedPointer(p, hdrEnd, ehFramePtrEnc, ehFrameHdrAddr);
    if (ehFrameAddr == 0)
        return false;

    // Walk .eh_frame entries directly
    // We use a large limit to handle the worst case, but stop at any zero-length entry
    auto* frameData = reinterpret_cast<const uint8_t*>(ehFrameAddr);

    // Map of CIE offset -> CieInfo for FDE back-references
    struct CieEntry { uintptr_t offset; CieInfo info; };
    std::vector<CieEntry> cies;
    cies.reserve(16);

    size_t initialSize = outDeltas.size();

    // Safety limit: 256 MiB to avoid unbounded reads on corrupt data
    const uint8_t* frameEnd = frameData + (256 * 1024 * 1024);

    const uint8_t* pos = frameData;
    while (pos + 4 <= frameEnd)
    {
        const uint8_t* entryStart = pos;
        uint32_t length32 = ReadValue<uint32_t>(pos, frameEnd);
        if (length32 == 0)
            break;

        bool is64Bit = false;
        uint64_t entryLength = length32;
        if (length32 == 0xFFFFFFFF)
        {
            is64Bit = true;
            entryLength = ReadValue<uint64_t>(pos, frameEnd);
            if (entryLength == 0)
                break;
        }

        const uint8_t* entryData = pos;
        const uint8_t* entryEnd = pos + entryLength;
        if (entryEnd > frameEnd)
            break;
        pos = entryEnd;

        // Read CIE id / pointer
        uint64_t cieIdOrPtr;
        if (is64Bit)
            cieIdOrPtr = ReadValue<uint64_t>(entryData, entryEnd);
        else
            cieIdOrPtr = ReadValue<uint32_t>(entryData, entryEnd);

        if (cieIdOrPtr == 0)
        {
            // This is a CIE
            CieInfo cie;
            if (ParseCie(entryData, static_cast<size_t>(entryEnd - entryData), is64Bit, cie))
            {
                uintptr_t cieOffset = static_cast<uintptr_t>(entryStart - frameData);
                cies.push_back({cieOffset, cie});
            }
        }
        else
        {
            // This is an FDE. cieIdOrPtr is offset from current position back to CIE.
            // In .eh_frame: CIE pointer is relative to the start of the CIE pointer field.
            uintptr_t ciePointerFieldStart = static_cast<uintptr_t>((entryData - (is64Bit ? 8 : 4)) - frameData);
            uintptr_t cieOffset = ciePointerFieldStart - cieIdOrPtr;

            // Find the referenced CIE
            const CieInfo* cie = nullptr;
            for (const auto& entry : cies)
            {
                if (entry.offset == cieOffset)
                {
                    cie = &entry.info;
                    break;
                }
            }

            if (cie != nullptr)
            {
                ParseFde(entryData, static_cast<size_t>(entryEnd - entryData),
                         *cie, ehFrameAddr, is64Bit, ehFrameAddr, outDeltas);
            }
        }
    }

    // Sort the deltas by address
    if (outDeltas.size() > initialSize)
    {
        std::sort(outDeltas.begin() + initialSize, outDeltas.end(),
                  [](const StackDelta& a, const StackDelta& b) { return a.address < b.address; });
        return true;
    }

    return false;
}

#ifdef DD_TEST
bool EhFrameParser::ExtractDeltasFromRaw(
    const uint8_t* ehFrameData, size_t ehFrameSize,
    uintptr_t loadBias, bool is64Bit,
    std::vector<StackDelta>& outDeltas)
{
    struct CieEntry { uintptr_t offset; CieInfo info; };
    std::vector<CieEntry> cies;
    cies.reserve(8);

    size_t initialSize = outDeltas.size();

    const uint8_t* pos = ehFrameData;
    const uint8_t* frameEnd = ehFrameData + ehFrameSize;

    while (pos + 4 <= frameEnd)
    {
        const uint8_t* entryStart = pos;
        uint32_t length32 = ReadValue<uint32_t>(pos, frameEnd);
        if (length32 == 0)
            break;

        uint64_t entryLength = length32;
        if (length32 == 0xFFFFFFFF)
        {
            entryLength = ReadValue<uint64_t>(pos, frameEnd);
            is64Bit = true;
            if (entryLength == 0)
                break;
        }

        const uint8_t* entryData = pos;
        const uint8_t* entryEnd = pos + entryLength;
        if (entryEnd > frameEnd)
            break;
        pos = entryEnd;

        uint64_t cieIdOrPtr;
        if (is64Bit)
            cieIdOrPtr = ReadValue<uint64_t>(entryData, entryEnd);
        else
            cieIdOrPtr = ReadValue<uint32_t>(entryData, entryEnd);

        if (cieIdOrPtr == 0)
        {
            CieInfo cie;
            if (ParseCie(entryData, static_cast<size_t>(entryEnd - entryData), is64Bit, cie))
            {
                uintptr_t cieOffset = static_cast<uintptr_t>(entryStart - ehFrameData);
                cies.push_back({cieOffset, cie});
            }
        }
        else
        {
            uintptr_t ciePointerFieldStart = static_cast<uintptr_t>((entryData - (is64Bit ? 8 : 4)) - ehFrameData);
            uintptr_t cieOffset = ciePointerFieldStart - cieIdOrPtr;

            const CieInfo* cie = nullptr;
            for (const auto& entry : cies)
            {
                if (entry.offset == cieOffset)
                {
                    cie = &entry.info;
                    break;
                }
            }

            if (cie != nullptr)
            {
                ParseFde(entryData, static_cast<size_t>(entryEnd - entryData),
                         *cie, loadBias, is64Bit, reinterpret_cast<uintptr_t>(ehFrameData), outDeltas);
            }
        }
    }

    if (outDeltas.size() > initialSize)
    {
        std::sort(outDeltas.begin() + initialSize, outDeltas.end(),
                  [](const StackDelta& a, const StackDelta& b) { return a.address < b.address; });
        return true;
    }

    return false;
}
#endif
