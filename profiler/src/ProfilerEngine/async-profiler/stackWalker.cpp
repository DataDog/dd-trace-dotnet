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
#include "stackWalker.h"
#include "codeCache.h"
#include "dwarf.h"
#include "safeAccess.h"
#include "stackFrame.h"
#include <cstdio>

const intptr_t MIN_VALID_PC = 0x1000;
const intptr_t MAX_WALK_SIZE = 0x100000;
const intptr_t MAX_FRAME_SIZE = 0x40000;

bool read_memory(uint64_t addr, uint64_t *res, const ap::StackBuffer &buffer) {
  if (addr < 4095) {
    return false;
  }
  if ((addr & 0x7) != 0) {
    // not aligned
    return false;
  }
  if (addr > addr + sizeof(uint64_t)) {
    return false;
  }

  if (addr < buffer.sp_start && addr > buffer.sp_start - 4096) {
    if (*res > buffer.sp_start && *res < buffer.sp_end) {
      // todo this is true only on leaf function afaik ?
      // printf("red zone optim (current value %lx) -- OK\n", *res);
      // high addr
      //
      // sp(2)
      //
      // sp(3) (-32 fp_off)
      //
      // low addr
      // red zone
      return true;
    }
    //  printf("red zone optim (current value %lx) -- KO\n", *res);
    return false;
  } else if (addr < buffer.sp_start ||
             addr + sizeof(uint64_t) > buffer.sp_end) {
    return false;
  }
  uint64_t stack_idx = addr - buffer.sp_start;
  if (stack_idx > addr) {
    return false;
  }
  *res = *(uint64_t *)(buffer._bytes.data() + stack_idx);
  return true;
}

// todo const correctness
CodeCache *findLibraryByAddress(CodeCacheArray *cache, const void *address) {
  const int native_lib_count = cache->count();
  for (int i = 0; i < native_lib_count; i++) {
    if (cache->operator[](i)->contains(address)) {
      return cache->operator[](i);
    }
  }
  return NULL;
}

bool stepStackContext(ap::StackContext &sc, const ap::StackBuffer &buffer,
                      FrameDesc *f);

bool stepStackContext(ap::StackContext &sc, const ap::StackBuffer &buffer,
                      CodeCacheArray *cache) {
  FrameDesc *f;
  CodeCache *cc = findLibraryByAddress(cache, sc.pc);
  if (cc == NULL || (f = cc->findFrameDesc(sc.pc)) == NULL) {
    f = &FrameDesc::default_frame;
  }
//  const char *sym = cc?cc->binarySearch(sc.pc):"unknown";
//  printf("-- Unwind from %s, %s \n", sym?sym:"unknown", cc?cc->name():"unknown");
  return stepStackContext(sc, buffer, f);
}

bool stepStackContext(ap::StackContext &sc, const ap::StackBuffer &buffer,
                      FrameDesc *f) {
  uintptr_t bottom = sc.sp + MAX_WALK_SIZE;
  uintptr_t prev_sp = sc.sp;

  u8 cfa_reg = (u8)f->cfa;
  int cfa_off = f->cfa >> 8;
  if (cfa_reg == DW_REG_SP) {
    sc.sp = sc.sp + cfa_off;
  } else if (cfa_reg == DW_REG_FP) {
    sc.sp = sc.fp + cfa_off;
  } else if (cfa_reg == DW_REG_PLT) {
    sc.sp += ((uintptr_t)sc.pc & 15) >= 11 ? cfa_off * 2 : cfa_off;
  } else {
    // it is interesting to categorize these cases
    // printf("unhandled reg \n");
    return false;
  }
  // Check if the next frame is below on the current stack
  if (sc.sp < prev_sp || sc.sp >= prev_sp + MAX_FRAME_SIZE || sc.sp >= bottom) {
    return false;
  }

  // Stack pointer must be word aligned
  if ((sc.sp & (sizeof(uintptr_t) - 1)) != 0) {
    return false;
  }

  if (f->fp_off & DW_PC_OFFSET) {
    sc.pc = (const char *)sc.pc + (f->fp_off >> 1);
  } else {
    if (f->fp_off != DW_SAME_FP && f->fp_off < MAX_FRAME_SIZE &&
        f->fp_off > -MAX_FRAME_SIZE) {
      // Update the frame pointer (based on fp offset)
      if (!read_memory(sc.sp + f->fp_off, reinterpret_cast<uint64_t *>(&sc.fp),
                       buffer)) {
        return false;
      }
    }
    // Update the pc using return address
    if (!read_memory(reinterpret_cast<uint64_t>((void **)sc.sp - 1),
                     reinterpret_cast<uint64_t *>(&sc.pc), buffer)) {
      return false;
    }
  }

  if (sc.pc < (const void *)MIN_VALID_PC ||
      sc.pc > (const void *)-MIN_VALID_PC) {
    return false;
  }
  return true;
}

void populateStackContext(ap::StackContext &sc, void *ucontext) {
  if (ucontext == NULL) {
    sc.pc = __builtin_return_address(0);
    sc.fp =
        (uintptr_t)__builtin_frame_address(1); // XXX(nick): this isn't safe....
    sc.sp = (uintptr_t)__builtin_frame_address(0);
  } else {
    StackFrame frame(ucontext);
    sc.pc = (const void *)frame.pc();
    sc.fp = frame.fp();
    sc.sp = frame.sp();
  }
}

int stackWalk(CodeCacheArray *cache, ap::StackContext &sc,
              const ap::StackBuffer &buffer, void const **callchain,
              int max_depth, int skip) {
  int depth = -skip;
  // Walk until the bottom of the stack or until the first Java frame
  while (depth < max_depth) {
    int d = depth++;
    if (d >= 0) {
      callchain[d] = sc.pc;
    }
    if (!stepStackContext(sc, buffer, cache)) {
      break;
    }
  }
  return depth;
}
