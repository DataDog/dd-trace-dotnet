#include "stack_context.h"

#define CAST_TO_VOID_STAR(ptr) reinterpret_cast<void *>(ptr)

namespace ap {

// Async profiler's unwinding only uses a subset of the registers
StackContext from_regs(const ddprof::span<uint64_t, PERF_REGS_COUNT> regs) {
  // context from saving state
  ap::StackContext sc;
  sc.pc = CAST_TO_VOID_STAR(regs[REGNAME(PC)]);
  sc.sp = regs[REGNAME(SP)];
  sc.fp = regs[REGNAME(RBP)];
  return sc;
}

} // namespace ap
