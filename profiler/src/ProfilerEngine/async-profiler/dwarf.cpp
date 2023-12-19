/*
 * Copyright 2022 Nick Ripley
 * Copyright 2021 Andrei Pangin
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 *
 * Modified by Nick Ripley to extract components needed for call stack unwinding
 */

#include "dwarf.h"
#include <stdlib.h>

#include <cstdio>

#define DEBUG

enum {
  DW_CFA_nop = 0x0,
  DW_CFA_set_loc = 0x1,
  DW_CFA_advance_loc1 = 0x2,
  DW_CFA_advance_loc2 = 0x3,
  DW_CFA_advance_loc4 = 0x4,
  DW_CFA_offset_extended = 0x5,
  DW_CFA_restore_extended = 0x6,
  DW_CFA_undefined = 0x7,
  DW_CFA_same_value = 0x8,
  DW_CFA_register = 0x9,
  DW_CFA_remember_state = 0xa,
  DW_CFA_restore_state = 0xb,
  DW_CFA_def_cfa = 0xc,
  DW_CFA_def_cfa_register = 0xd,
  DW_CFA_def_cfa_offset = 0xe,
  DW_CFA_def_cfa_expression = 0xf,
  DW_CFA_expression = 0x10,
  DW_CFA_offset_extended_sf = 0x11,
  DW_CFA_def_cfa_sf = 0x12,
  DW_CFA_def_cfa_offset_sf = 0x13,
  DW_CFA_val_offset = 0x14,
  DW_CFA_val_offset_sf = 0x15,
  DW_CFA_val_expression = 0x16,
  DW_CFA_GNU_args_size = 0x2e,

  DW_CFA_advance_loc = 0x1,
  DW_CFA_offset = 0x2,
  DW_CFA_restore = 0x3,
};

enum {
  DW_OP_breg_pc = 0x70 + DW_REG_PC,
  DW_OP_const1u = 0x08,
  DW_OP_const1s = 0x09,
  DW_OP_const2u = 0x0a,
  DW_OP_const2s = 0x0b,
  DW_OP_const4u = 0x0c,
  DW_OP_const4s = 0x0d,
  DW_OP_constu = 0x10,
  DW_OP_consts = 0x11,
  DW_OP_minus = 0x1c,
  DW_OP_plus = 0x22,
};

FrameDesc FrameDesc::default_frame = {0, DW_REG_FP | (2 * DW_STACK_SLOT) << 8,
                                      -2 * DW_STACK_SLOT};

DwarfParser::DwarfParser(const char *name, const char *image_base,
                         const char *eh_frame_hdr, u64 adjust_eh_frame) {
  _name = name;
  _image_base = image_base;

  _capacity = 128;
  _count = 0;
  _table = (FrameDesc *)malloc(_capacity * sizeof(FrameDesc));
  _prev = NULL;

  _code_align = sizeof(instruction_t);
  _data_align = -(int)sizeof(void *);

  parse(eh_frame_hdr, adjust_eh_frame);
}

void DwarfParser::parse(const char *eh_frame_hdr, u64 adjust_eh_frame) {
  u8 version = eh_frame_hdr[0];
  u8 eh_frame_ptr_enc = eh_frame_hdr[1];
  u8 fde_count_enc = eh_frame_hdr[2];
  u8 table_enc = eh_frame_hdr[3];

  printf("eh_frame_ptr_enc = %lx \n", eh_frame_ptr_enc);
  printf("table_enc = %lx \n", table_enc);
  if (version != 1 || (eh_frame_ptr_enc & 0x7) != 0x3 ||
      (fde_count_enc & 0x7) != 0x3 || (table_enc & 0xf7) != 0x33) {
    return;
  }

  int fde_count = *(int *)(eh_frame_hdr + 8);
#ifdef DEBUG
  printf("fde count = %d \n", fde_count);
#endif
  int *table = (int *)(eh_frame_hdr + 16);
  for (int i = 0; i < fde_count; i++) {
    _ptr = eh_frame_hdr + table[i * 2] - adjust_eh_frame;
    if (i == 0) {
      printf("ptr = %lx, table offset = %lx \n", _ptr, table[i * 2]);
    }
    parseFde();
  }
}

void DwarfParser::parseCie() {
  u32 cie_len = get32();
  if (cie_len == 0 || cie_len == 0xffffffff) {
    return;
  }

  const char *cie_start = _ptr;
  _ptr += 5;
  while (*_ptr++) {}
  _code_align = getLeb();
  _data_align = getSLeb();
  _ptr = cie_start + cie_len;
}

void DwarfParser::parseFde() {

  u32 fde_len = get32();
  //  printf("fde len = %u \n", fde_len);
  if (fde_len == 0 || fde_len == 0xffffffff) {
    return;
  }

  const char *fde_start = _ptr;
  u32 cie_offset = get32();
  if (_count == 0) {
#ifdef DEBUG
    printf("Change pointer to %lx - %lx \n", fde_start, cie_offset);
#endif
    _ptr = fde_start - cie_offset;
    parseCie();
    _ptr = fde_start + 4;
  }

  u32 range_start = getPtr() - _image_base;
  if (_count == 0) {
    printf("Dwarf range start: %lx (ptr) - %lx (image) = %lx \n", getPtr(),
           _image_base, range_start);
  }

  u32 range_len = get32();
  _ptr += getLeb();
  parseInstructions(range_start, fde_start + fde_len);
  addRecord(range_start + range_len, DW_REG_SP, DW_STACK_SLOT, DW_SAME_FP);
}

void DwarfParser::parseInstructions(u32 loc, const char *end) {
  const u32 code_align = _code_align;
  const int data_align = _data_align;

  u32 cfa_reg = DW_REG_SP;
  int cfa_off = DW_STACK_SLOT;
  int fp_off = DW_SAME_FP;
  int pc_off = -DW_STACK_SLOT;

  u32 rem_cfa_reg;
  int rem_cfa_off;
  int rem_fp_off;
  int rem_pc_off;

  while (_ptr < end) {
    u8 op = get8();
    switch (op >> 6) {
    case 0:
      switch (op) {
      case DW_CFA_nop:
      case DW_CFA_set_loc:
        _ptr = end;
        break;
      case DW_CFA_advance_loc1:
        addRecord(loc, cfa_reg, cfa_off, fp_off);
        loc += get8() * code_align;
        break;
      case DW_CFA_advance_loc2:
        addRecord(loc, cfa_reg, cfa_off, fp_off);
#ifdef ALIGN_ISSUES
        // 16 bits alignement check
        if ((reinterpret_cast<unsigned long>(_ptr) & 0xf) != 0) {
          // The address is not 16-bit aligned here
          printf("Addr is not aligned 0x%lx (code_align=%i)\n", _ptr,
                 code_align);
          exit(1);
        }
#endif
        loc += get16() * code_align;
        break;
      case DW_CFA_advance_loc4:
        addRecord(loc, cfa_reg, cfa_off, fp_off);
        loc += get32() * code_align;
        break;
      case DW_CFA_offset_extended:
        switch (getLeb()) {
        case DW_REG_FP:
          fp_off = getLeb() * data_align;
          break;
        case DW_REG_PC:
          pc_off = getLeb() * data_align;
          break;
        default:
          skipLeb();
        }
        break;
      case DW_CFA_restore_extended:
      case DW_CFA_undefined:
      case DW_CFA_same_value:
        skipLeb();
        break;
      case DW_CFA_register:
        skipLeb();
        skipLeb();
        break;
      case DW_CFA_remember_state:
        rem_cfa_reg = cfa_reg;
        rem_cfa_off = cfa_off;
        rem_fp_off = fp_off;
        rem_pc_off = pc_off;
        break;
      case DW_CFA_restore_state:
        cfa_reg = rem_cfa_reg;
        cfa_off = rem_cfa_off;
        fp_off = rem_fp_off;
        pc_off = rem_pc_off;
        break;
      case DW_CFA_def_cfa:
        cfa_reg = getLeb();
        cfa_off = getLeb();
        break;
      case DW_CFA_def_cfa_register:
        cfa_reg = getLeb();
        break;
      case DW_CFA_def_cfa_offset:
        cfa_off = getLeb();
        break;
      case DW_CFA_def_cfa_expression: {
        u32 len = getLeb();
        cfa_reg = len == 11 ? DW_REG_PLT : DW_REG_INVALID;
        cfa_off = DW_STACK_SLOT;
        _ptr += len;
        break;
      }
      case DW_CFA_expression:
        skipLeb();
        _ptr += getLeb();
        break;
      case DW_CFA_offset_extended_sf:
        switch (getLeb()) {
        case DW_REG_FP:
          fp_off = getSLeb() * data_align;
          break;
        case DW_REG_PC:
          pc_off = getSLeb() * data_align;
          break;
        default:
          skipLeb();
        }
        break;
      case DW_CFA_def_cfa_sf:
        cfa_reg = getLeb();
        cfa_off = getSLeb() * data_align;
        break;
      case DW_CFA_def_cfa_offset_sf:
        cfa_off = getSLeb() * data_align;
        break;
      case DW_CFA_val_offset:
      case DW_CFA_val_offset_sf:
        skipLeb();
        skipLeb();
        break;
      case DW_CFA_val_expression:
        if (getLeb() == DW_REG_PC) {
          int pc_off = parseExpression();
          if (pc_off != 0) {
            fp_off = DW_PC_OFFSET | (pc_off << 1);
          }
        } else {
          _ptr += getLeb();
        }
        break;
      case DW_CFA_GNU_args_size:
        skipLeb();
        break;
      default:
        return;
      }
      break;
    case DW_CFA_advance_loc:
      addRecord(loc, cfa_reg, cfa_off, fp_off);
      loc += (op & 0x3f) * code_align;
      break;
    case DW_CFA_offset:
      switch (op & 0x3f) {
      case DW_REG_FP:
        fp_off = getLeb() * data_align;
        break;
      case DW_REG_PC:
        pc_off = getLeb() * data_align;
        break;
      default:
        skipLeb();
      }
      break;
    case DW_CFA_restore:
      break;
    }
  }

  addRecord(loc, cfa_reg, cfa_off, fp_off);
}

// Parse a limited subset of DWARF expressions, which is used in
// DW_CFA_val_expression to point to the previous PC relative to the current PC.
// Returns the offset of the previous PC from the current PC.
int DwarfParser::parseExpression() {
  int pc_off = 0;
  int tos = 0;

  u32 len = getLeb();
  const char *end = _ptr + len;

  while (_ptr < end) {
    u8 op = get8();
    switch (op) {
    case DW_OP_breg_pc:
      pc_off = getSLeb();
      break;
    case DW_OP_const1u:
      tos = get8();
      break;
    case DW_OP_const1s:
      tos = (signed char)get8();
      break;
    case DW_OP_const2u:
      tos = get16();
      break;
    case DW_OP_const2s:
      tos = (short)get16();
      break;
    case DW_OP_const4u:
    case DW_OP_const4s:
      tos = get32();
      break;
    case DW_OP_constu:
      tos = getLeb();
      break;
    case DW_OP_consts:
      tos = getSLeb();
      break;
    case DW_OP_minus:
      pc_off -= tos;
      break;
    case DW_OP_plus:
      pc_off += tos;
      break;
    default:
      _ptr = end;
      return 0;
    }
  }

  return pc_off;
}

void DwarfParser::addRecord(u32 loc, u32 cfa_reg, int cfa_off, int fp_off) {
  int cfa = cfa_reg | cfa_off << 8;
  if (_prev == NULL || (_prev->loc == loc && --_count >= 0) ||
      _prev->cfa != cfa || _prev->fp_off != fp_off) {
    _prev = addRecordRaw(loc, cfa, fp_off);
  }
}

FrameDesc *DwarfParser::addRecordRaw(u32 loc, int cfa, int fp_off) {
  if (_count >= _capacity) {
    _capacity *= 2;
    _table = (FrameDesc *)realloc(_table, _capacity * sizeof(FrameDesc));
  }

  FrameDesc *f = &_table[_count++];
  f->loc = loc;
  f->cfa = cfa;
  f->fp_off = fp_off;
  return f;
}
