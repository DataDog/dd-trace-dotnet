// Standalone reproduction of APMS-19196 EH clause sort bug.
// No dependencies — compile with: g++ -std=c++17 -o eh_sort_repro eh_sort_repro.cpp && ./eh_sort_repro
//
// Clause layout (offsets, try/handler bounds, instruction chain) matches the native unit test
//   ILRewriterEHSortTest.DebuggerAsyncMiddlewareScenario
// in tracer/test/Datadog.Tracer.Native.Tests/il_rewriter_eh_sort_test.cpp — the same scenario
// EndAsyncMethodProbe / ILRewriter::Export see after instrumenting an async middleware MoveNext().

#include <algorithm>
#include <cstring>
#include <cstdio>
#include <vector>
#include <cassert>

// ── Minimal re-implementation of the structs from il_rewriter.h ───────────────

struct ILInstr {
    ILInstr* m_pNext;
    ILInstr* m_pPrev;
    unsigned m_opcode;
    unsigned m_offset;
};

struct EHClause {
    unsigned  m_Flags;
    ILInstr*  m_pTryBegin;
    ILInstr*  m_pTryEnd;
    ILInstr*  m_pHandlerBegin;
    ILInstr*  m_pHandlerEnd;   // last instr inside handler; m_pNext is one past the end
};

// ── Helper: build a linked chain of instruction nodes ─────────────────────────

static ILInstr* MakeChain(const std::vector<unsigned>& offsets) {
    auto n = offsets.size();
    auto* instrs = new ILInstr[n]();
    for (size_t i = 0; i < n; i++) {
        instrs[i].m_offset = offsets[i];
        instrs[i].m_pPrev  = (i > 0)     ? &instrs[i-1] : &instrs[n-1];
        instrs[i].m_pNext  = (i < n-1)   ? &instrs[i+1] : &instrs[0];
    }
    return instrs;
}

static ILInstr* Find(ILInstr* chain, size_t n, unsigned offset) {
    for (size_t i = 0; i < n; i++)
        if (chain[i].m_offset == offset) return &chain[i];
    return nullptr;
}

// ── THE OLD (BROKEN) sort comparator ─────────────────────────────────────────
// Copied verbatim from il_rewriter.cpp before the fix.
// Only checks try-in-try containment; completely ignores handler regions.

static void OldSort(EHClause* pEH, unsigned nEH) {
    std::sort(pEH, pEH + nEH, [](EHClause a, EHClause b) {
        return a.m_pTryBegin->m_offset > b.m_pTryBegin->m_offset &&
               a.m_pTryEnd->m_offset   < b.m_pTryEnd->m_offset;
    });
}

// ── THE NEW (FIXED) sort from PR #8428 ────────────────────────────────────────

static void SortEHClauses(EHClause* pEH, unsigned nEH) {
    if (nEH <= 1) return;

    auto* depth = new unsigned[nEH]();

    for (unsigned i = 0; i < nEH; i++) {
        auto iTryBegin = pEH[i].m_pTryBegin->m_offset;
        auto iTryEnd   = pEH[i].m_pTryEnd->m_offset;

        for (unsigned j = 0; j < nEH; j++) {
            if (i == j) continue;

            auto jTryBegin     = pEH[j].m_pTryBegin->m_offset;
            auto jTryEnd       = pEH[j].m_pTryEnd->m_offset;
            auto jHandlerBegin = pEH[j].m_pHandlerBegin->m_offset;
            auto jHandlerEnd   = pEH[j].m_pHandlerEnd->m_pNext->m_offset;

            bool inTry = (iTryBegin >= jTryBegin && iTryEnd <= jTryEnd)
                         && !(iTryBegin == jTryBegin && iTryEnd == jTryEnd);

            bool inHandler = (iTryBegin >= jHandlerBegin && iTryEnd <= jHandlerEnd)
                             && !(iTryBegin == jHandlerBegin && iTryEnd == jHandlerEnd);

            if (inTry || inHandler) depth[i]++;
        }
    }

    auto* indices = new unsigned[nEH];
    for (unsigned i = 0; i < nEH; i++) indices[i] = i;

    std::sort(indices, indices + nEH, [&](unsigned a, unsigned b) {
        if (depth[a] != depth[b]) return depth[a] > depth[b];
        return pEH[a].m_pTryBegin->m_offset < pEH[b].m_pTryBegin->m_offset;
    });

    auto* sorted = new EHClause[nEH];
    for (unsigned i = 0; i < nEH; i++) sorted[i] = pEH[indices[i]];
    for (unsigned i = 0; i < nEH; i++) pEH[i] = sorted[i];

    delete[] sorted;
    delete[] indices;
    delete[] depth;
}

// ── Real scenario (reported crash + repro app ReproMiddleware) ─────────────────
//
// ReproMiddleware.InvokeAsync (async Task, try { await _next } catch (UnauthorizedAccessException))
// compiles to <InvokeAsync>d__.MoveNext(). That IL is what the CLR profiler + debugger rewriter
// instrument (Exception Replay → ReJIT → EndAsyncMethodProbe, then ILRewriter::Export sorts EH).
//
// Below is NOT arbitrary: it is the same five synthetic EH rows as DebuggerAsyncMiddlewareScenario:
//   compiler-generated clauses for the async state machine (Original[0]=await finally, Original[1]=middleware try/catch),
//   plus debugger-injected beginMethod / endMethod(SetException) / endMethod(SetResult) clauses.
//
//   Original[0]: COR_ILEXCEPTION_CLAUSE_FINALLY — await machinery  try [30,55)  finally handler [55,58)
//   Original[1]: outer try/catch (middleware body + SM outer catch)  try [20,70)  handler [70,100)
//   Injected[2]: beginMethod try/catch                          try [5,12)   handler [12,15)
//   Injected[3]: endMethod(SetException) try/catch  — try [72,85) lies INSIDE Original[1]'s handler [70,100)
//   Injected[4]: endMethod(SetResult) try/catch                 try [102,115) handler [115,118)
//
// ECMA-335 II.19: nested clauses must appear before enclosing clauses → Injected[3] before Original[1].
// v3.41.0 old std::sort only compared try-in-try containment, so it often left Injected[3] after Original[1]
// → invalid EH table → InvalidProgramException (APMS-19196).

static void BuildClauses(EHClause* clauses, ILInstr* instrs, size_t n) {
    memset(clauses, 0, 5 * sizeof(EHClause));

    // Original[0]: inner try/finally — same as il_rewriter_eh_sort_test.cpp clauses[0]
    clauses[0].m_pTryBegin     = Find(instrs, n, 30);
    clauses[0].m_pTryEnd       = Find(instrs, n, 55);
    clauses[0].m_pHandlerBegin = Find(instrs, n, 55);
    clauses[0].m_pHandlerEnd   = Find(instrs, n, 58);   // ->m_pNext = 70

    // Original[1]: outer try/catch — same as test clauses[1]
    clauses[1].m_pTryBegin     = Find(instrs, n, 20);
    clauses[1].m_pTryEnd       = Find(instrs, n, 70);
    clauses[1].m_pHandlerBegin = Find(instrs, n, 70);
    clauses[1].m_pHandlerEnd   = Find(instrs, n, 95);   // ->m_pNext = 100

    // Injected[2]: beginMethod — same as test clauses[2]
    clauses[2].m_pTryBegin     = Find(instrs, n, 5);
    clauses[2].m_pTryEnd       = Find(instrs, n, 12);
    clauses[2].m_pHandlerBegin = Find(instrs, n, 12);
    clauses[2].m_pHandlerEnd   = Find(instrs, n, 15);   // ->m_pNext = 20

    // Injected[3]: endMethod(SetException) — same as test clauses[3]; try region inside Original[1] handler
    clauses[3].m_pTryBegin     = Find(instrs, n, 72);
    clauses[3].m_pTryEnd       = Find(instrs, n, 85);
    clauses[3].m_pHandlerBegin = Find(instrs, n, 85);
    clauses[3].m_pHandlerEnd   = Find(instrs, n, 88);   // ->m_pNext = 95

    // Injected[4]: endMethod(SetResult) — same as test clauses[4]
    clauses[4].m_pTryBegin     = Find(instrs, n, 102);
    clauses[4].m_pTryEnd       = Find(instrs, n, 115);
    clauses[4].m_pHandlerBegin = Find(instrs, n, 115);
    clauses[4].m_pHandlerEnd   = Find(instrs, n, 118);  // ->m_pNext = 120
}

static void PrintOrder(const char* label, EHClause* clauses, int n) {
    printf("%s\n", label);
    for (int i = 0; i < n; i++)
        printf("  [%d] try [%3u, %3u)\n", i,
               clauses[i].m_pTryBegin->m_offset,
               clauses[i].m_pTryEnd->m_offset);
}

static bool IsSetExceptionBeforeOuter(EHClause* clauses, int n) {
    int setExceptionIdx = -1, outerIdx = -1;
    for (int i = 0; i < n; i++) {
        if (clauses[i].m_pTryBegin->m_offset == 72) setExceptionIdx = i;
        if (clauses[i].m_pTryBegin->m_offset == 20) outerIdx = i;
    }
    return setExceptionIdx < outerIdx;
}

int main() {
    auto* instrs = MakeChain({5,12,15,20,30,55,58,70,72,85,88,95,100,102,115,118,120,125});
    const size_t N = 18;

    printf("==========================================================\n");
    printf("APMS-19196 — EH Clause Sort Bug Reproduction\n");
    printf("==========================================================\n\n");

    printf("Scenario: same EH rows as ILRewriterEHSortTest.DebuggerAsyncMiddlewareScenario\n");
    printf("  (async InvokeAsync -> MoveNext(); repro app: ReproMiddleware)\n");
    printf("After debugger injects beginMethod / endMethod(SetException) / endMethod(SetResult):\n\n");
    printf("  Original[0]: try [30,55) finally   -- await machinery\n");
    printf("  Original[1]: try [20,70) catch handler [70,100)  -- middleware + state machine\n");
    printf("  Injected[2]: try [5,12)   -- beginMethod\n");
    printf("  Injected[3]: try [72,85)  -- endMethod(SetException) *** inside Original[1] handler ***\n");
    printf("  Injected[4]: try [102,115) -- endMethod(SetResult)\n\n");
    printf("Per ECMA-335 II.19: Injected[3] MUST come before Original[1].\n");
    printf("If it doesn't → CLR throws InvalidProgramException → pod crash.\n\n");

    // ── Test 1: Old sort ──────────────────────────────────────────────────────
    EHClause old_clauses[5];
    BuildClauses(old_clauses, instrs, N);
    OldSort(old_clauses, 5);

    PrintOrder("── OLD SORT (before fix) ──────────────────────────", old_clauses, 5);
    bool oldOk = IsSetExceptionBeforeOuter(old_clauses, 5);
    printf("  SetException before outer? %s\n", oldOk ? "YES ✓" : "NO ✗  ← BUG: CLR would throw InvalidProgramException");
    printf("\n");

    // ── Test 2: New sort ──────────────────────────────────────────────────────
    EHClause new_clauses[5];
    BuildClauses(new_clauses, instrs, N);
    SortEHClauses(new_clauses, 5);

    PrintOrder("── NEW SORT (PR #8428 fix) ─────────────────────────", new_clauses, 5);
    bool newOk = IsSetExceptionBeforeOuter(new_clauses, 5);
    printf("  SetException before outer? %s\n", newOk ? "YES ✓  ← FIXED: valid EH table, CLR accepts it" : "NO ✗");
    printf("\n");

    // ── Summary ───────────────────────────────────────────────────────────────
    printf("==========================================================\n");
    printf("RESULT: Bug %s | Fix %s\n",
           !oldOk ? "REPRODUCED ✓" : "not reproduced ✗",
           newOk  ? "VERIFIED ✓"   : "not working ✗");
    printf("==========================================================\n");

    delete[] instrs;

    return (!oldOk && newOk) ? 0 : 1;
}
