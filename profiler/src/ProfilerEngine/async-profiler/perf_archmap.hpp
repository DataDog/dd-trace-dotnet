// Unless explicitly stated otherwise all files in this repository are licensed
// under the Apache License Version 2.0. This product includes software
// developed at Datadog (https://www.datadoghq.com/). Copyright 2021-Present
// Datadog, Inc.

#pragma once

#include <array> // for std::size()

// Architecture mapfile

#ifdef __x86_64__
// Registers 0-11, 16-23
#  define PERF_REGS_COUNT 20
#  define PERF_REGS_MASK 0xff0fff
#  define REGNAME(x) PAM_X86_##x
enum PERF_ARCHMAP_X86 {
  PAM_X86_RAX,
  PAM_X86_RBX,
  PAM_X86_RCX,
  PAM_X86_RDX,
  PAM_X86_RSI,
  PAM_X86_RDI,
  PAM_X86_RBP,
  PAM_X86_RSP,
  PAM_X86_SP = PAM_X86_RSP, // For uniformity
  PAM_X86_RIP,
  PAM_X86_PC = PAM_X86_RIP, // For uniformity
  PAM_X86_FL,
  PAM_X86_CS,
  PAM_X86_SS,
  /*
    PAM_X86_DS,  // These segment registers cannot be read using common user
    PAM_X86_ES,  // permissions.  Accordingly, they are omitted from the mask.
    PAM_X86_FS,  // They are retained here for documentation.
    PAM_X86_GS,  // <-- and this one too
  */
  PAM_X86_R8,
  PAM_X86_R9,
  PAM_X86_R10,
  PAM_X86_R11,
  PAM_X86_R12,
  PAM_X86_R13,
  PAM_X86_R14,
  PAM_X86_R15,
  PAM_X86_MAX,
};
#elif __aarch64__
// Registers 0-32
#  define PERF_REGS_COUNT 33
#  define PERF_REGS_MASK (~(~0ull << PERF_REGS_COUNT))
#  define REGNAME(x) PAM_ARM_##x
enum PERF_ARCHMAP_ARM {
  PAM_ARM_X0,
  PAM_ARM_X1,
  PAM_ARM_X2,
  PAM_ARM_X3,
  PAM_ARM_X4,
  PAM_ARM_X5,
  PAM_ARM_X6,
  PAM_ARM_X7,
  PAM_ARM_X8,
  PAM_ARM_X9,
  PAM_ARM_X10,
  PAM_ARM_X11,
  PAM_ARM_X12,
  PAM_ARM_X13,
  PAM_ARM_X14,
  PAM_ARM_X15,
  PAM_ARM_X16,
  PAM_ARM_X17,
  PAM_ARM_X18,
  PAM_ARM_X19,
  PAM_ARM_X20,
  PAM_ARM_X21,
  PAM_ARM_X22,
  PAM_ARM_X23,
  PAM_ARM_X24,
  PAM_ARM_X25,
  PAM_ARM_X26,
  PAM_ARM_X27,
  PAM_ARM_X28,
  PAM_ARM_X29,
  PAM_ARM_FP = PAM_ARM_X29, // For uniformity
  PAM_ARM_LR,
  PAM_ARM_SP,
  PAM_ARM_PC,
  PAM_ARM_MAX,
};
#else
// cppcheck-suppress preprocessorErrorDirective
#  error Architecture not supported
#endif

// F[i] = y_i; where i is the DWARF regno and y_i is the Linux perf regno
constexpr unsigned int dwarf_to_perf_regno(unsigned int i) {
  constexpr unsigned int lookup[] = {
#ifdef __x86_64__
      REGNAME(RAX), REGNAME(RDX), REGNAME(RCX), REGNAME(RBX), REGNAME(RSI),
      REGNAME(RDI), REGNAME(RBP), REGNAME(SP),  REGNAME(R8),  REGNAME(R9),
      REGNAME(R10), REGNAME(R11), REGNAME(R12), REGNAME(R13), REGNAME(R14),
      REGNAME(R15), REGNAME(PC),
#elif __aarch64__
      REGNAME(X0),  REGNAME(X1),  REGNAME(X2),  REGNAME(X3),  REGNAME(X4),
      REGNAME(X5),  REGNAME(X6),  REGNAME(X7),  REGNAME(X8),  REGNAME(X9),
      REGNAME(X10), REGNAME(X11), REGNAME(X12), REGNAME(X13), REGNAME(X14),
      REGNAME(X15), REGNAME(X16), REGNAME(X17), REGNAME(X18), REGNAME(X19),
      REGNAME(X20), REGNAME(X21), REGNAME(X22), REGNAME(X23), REGNAME(X24),
      REGNAME(X25), REGNAME(X26), REGNAME(X27), REGNAME(X28), REGNAME(FP),
      REGNAME(LR),  REGNAME(SP),
#else
#  error Architecture not supported
#endif
  };

  if (i >= std::size(lookup)) {
    return -1u; // implicit sentinel value
  }

  return lookup[i];
};

constexpr unsigned int param_to_perf_regno(unsigned int param_no) {
// Populate lookups for converting parameter number (1-indexed) to regno
#define R(x) REGNAME(x)
#ifdef __x86_64__
  constexpr int reg_lookup[] = {-1,     R(RDI), R(RSI), R(RDX),
                                R(RCX), R(R8),  R(R9)};
#elif __aarch64__
  constexpr int reg_lookup[] = {-1,    R(X0), R(X1), R(X2),
                                R(X3), R(X4), R(X5), R(X6)};
#else
// cppcheck-suppress preprocessorErrorDirective
#  error Architecture not supported
#endif
#undef R

  if (!param_no || param_no >= std::size(reg_lookup))
    return -1u;

  return reg_lookup[param_no];
}

