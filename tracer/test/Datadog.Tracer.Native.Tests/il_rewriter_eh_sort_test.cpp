#include "pch.h"

#include "../../src/Datadog.Tracer.Native/il_rewriter.h"

// Helper to build a doubly-linked chain of ILInstr nodes with specified offsets.
// Returns a heap-allocated array; the caller must delete[].
// Each node's m_pNext and m_pPrev are wired up in sequence. The last node's
// m_pNext points to a sentinel (the first node acts as its own sentinel for
// handler-end calculations via m_pHandlerEnd->m_pNext).
static ILInstr* MakeInstrChain(const std::vector<unsigned>& offsets)
{
    auto n = offsets.size();
    auto* instrs = new ILInstr[n];
    memset(instrs, 0, n * sizeof(ILInstr));
    for (size_t i = 0; i < n; i++)
    {
        instrs[i].m_offset = offsets[i];
        instrs[i].m_pPrev = (i > 0) ? &instrs[i - 1] : &instrs[n - 1];
        instrs[i].m_pNext = (i < n - 1) ? &instrs[i + 1] : &instrs[0];
    }
    return instrs;
}

// Find the ILInstr with a given offset in the chain.
static ILInstr* FindInstr(ILInstr* chain, size_t count, unsigned offset)
{
    for (size_t i = 0; i < count; i++)
    {
        if (chain[i].m_offset == offset) return &chain[i];
    }
    return nullptr;
}

// ============================================================================
// Test: simple try-in-try nesting
// Inner try [20,50) nested in outer try [10,80). Inner should come first.
// ============================================================================
TEST(ILRewriterEHSortTest, TryInTryNesting)
{
    // Offsets: 10, 20, 50, 55, 60, 80, 85, 90
    auto* instrs = MakeInstrChain({10, 20, 50, 55, 60, 80, 85, 90});

    EHClause clauses[2];
    memset(clauses, 0, sizeof(clauses));

    // clauses[0]: outer -- try [10,80), handler [80,90)
    clauses[0].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[0].m_pTryBegin = FindInstr(instrs, 8, 10);
    clauses[0].m_pTryEnd = FindInstr(instrs, 8, 80);
    clauses[0].m_pHandlerBegin = FindInstr(instrs, 8, 80);
    clauses[0].m_pHandlerEnd = FindInstr(instrs, 8, 85); // last instr in handler

    // clauses[1]: inner -- try [20,50), handler [50,55)
    clauses[1].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[1].m_pTryBegin = FindInstr(instrs, 8, 20);
    clauses[1].m_pTryEnd = FindInstr(instrs, 8, 50);
    clauses[1].m_pHandlerBegin = FindInstr(instrs, 8, 50);
    clauses[1].m_pHandlerEnd = FindInstr(instrs, 8, 55); // handler end -> m_pNext is offset 60

    ILRewriter::SortEHClauses(clauses, 2);

    // Inner (try at 20) should come before outer (try at 10)
    EXPECT_EQ(clauses[0].m_pTryBegin->m_offset, 20u);
    EXPECT_EQ(clauses[1].m_pTryBegin->m_offset, 10u);

    delete[] instrs;
}

// ============================================================================
// Test: try-in-handler nesting (the debugger async middleware scenario)
// Outer: try [10,60), handler [60,95)
// Inner: try [65,80), handler [80,88) -- inside outer's handler
// Inner should come first.
// ============================================================================
TEST(ILRewriterEHSortTest, TryInHandlerNesting)
{
    auto* instrs = MakeInstrChain({10, 60, 65, 80, 88, 90, 95, 100});

    EHClause clauses[2];
    memset(clauses, 0, sizeof(clauses));

    // clauses[0]: outer -- try [10,60), handler [60,95)
    clauses[0].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[0].m_pTryBegin = FindInstr(instrs, 8, 10);
    clauses[0].m_pTryEnd = FindInstr(instrs, 8, 60);
    clauses[0].m_pHandlerBegin = FindInstr(instrs, 8, 60);
    clauses[0].m_pHandlerEnd = FindInstr(instrs, 8, 90); // m_pNext->m_offset = 95

    // clauses[1]: inner -- try [65,80), handler [80,88)
    clauses[1].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[1].m_pTryBegin = FindInstr(instrs, 8, 65);
    clauses[1].m_pTryEnd = FindInstr(instrs, 8, 80);
    clauses[1].m_pHandlerBegin = FindInstr(instrs, 8, 80);
    clauses[1].m_pHandlerEnd = FindInstr(instrs, 8, 88); // m_pNext->m_offset = 90

    ILRewriter::SortEHClauses(clauses, 2);

    // Inner (try at 65, inside outer's handler) should come before outer (try at 10)
    EXPECT_EQ(clauses[0].m_pTryBegin->m_offset, 65u);
    EXPECT_EQ(clauses[1].m_pTryBegin->m_offset, 10u);

    delete[] instrs;
}

// ============================================================================
// Test: sibling clauses (no nesting) -- order by try offset
// ============================================================================
TEST(ILRewriterEHSortTest, SiblingClauses)
{
    auto* instrs = MakeInstrChain({10, 20, 25, 30, 50, 60, 65, 70});

    EHClause clauses[2];
    memset(clauses, 0, sizeof(clauses));

    // clauses[0]: try [50,60), handler [60,65)
    clauses[0].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[0].m_pTryBegin = FindInstr(instrs, 8, 50);
    clauses[0].m_pTryEnd = FindInstr(instrs, 8, 60);
    clauses[0].m_pHandlerBegin = FindInstr(instrs, 8, 60);
    clauses[0].m_pHandlerEnd = FindInstr(instrs, 8, 65); // m_pNext = 70

    // clauses[1]: try [10,20), handler [20,25)
    clauses[1].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[1].m_pTryBegin = FindInstr(instrs, 8, 10);
    clauses[1].m_pTryEnd = FindInstr(instrs, 8, 20);
    clauses[1].m_pHandlerBegin = FindInstr(instrs, 8, 20);
    clauses[1].m_pHandlerEnd = FindInstr(instrs, 8, 25); // m_pNext = 30

    ILRewriter::SortEHClauses(clauses, 2);

    // Same depth, ordered by try offset ascending: 10 before 50
    EXPECT_EQ(clauses[0].m_pTryBegin->m_offset, 10u);
    EXPECT_EQ(clauses[1].m_pTryBegin->m_offset, 50u);

    delete[] instrs;
}

// ============================================================================
// Test: 3-level nesting. C nested in B, B nested in A.
// A: try [0,100), handler [100,110)
// B: try [10,80),  handler [80,90)
// C: try [20,50),  handler [50,60)
// Expected order: C, B, A
// ============================================================================
TEST(ILRewriterEHSortTest, ThreeLevelNesting)
{
    auto* instrs = MakeInstrChain({0, 10, 20, 50, 60, 80, 90, 100, 110, 120});

    EHClause clauses[3];
    memset(clauses, 0, sizeof(clauses));

    // A: outermost
    clauses[0].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[0].m_pTryBegin = FindInstr(instrs, 10, 0);
    clauses[0].m_pTryEnd = FindInstr(instrs, 10, 100);
    clauses[0].m_pHandlerBegin = FindInstr(instrs, 10, 100);
    clauses[0].m_pHandlerEnd = FindInstr(instrs, 10, 110); // m_pNext = 120

    // B: middle
    clauses[1].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[1].m_pTryBegin = FindInstr(instrs, 10, 10);
    clauses[1].m_pTryEnd = FindInstr(instrs, 10, 80);
    clauses[1].m_pHandlerBegin = FindInstr(instrs, 10, 80);
    clauses[1].m_pHandlerEnd = FindInstr(instrs, 10, 90); // m_pNext = 100

    // C: innermost
    clauses[2].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[2].m_pTryBegin = FindInstr(instrs, 10, 20);
    clauses[2].m_pTryEnd = FindInstr(instrs, 10, 50);
    clauses[2].m_pHandlerBegin = FindInstr(instrs, 10, 50);
    clauses[2].m_pHandlerEnd = FindInstr(instrs, 10, 60); // m_pNext = 80

    ILRewriter::SortEHClauses(clauses, 3);

    // C (depth 2) first, B (depth 1) second, A (depth 0) last
    EXPECT_EQ(clauses[0].m_pTryBegin->m_offset, 20u);
    EXPECT_EQ(clauses[1].m_pTryBegin->m_offset, 10u);
    EXPECT_EQ(clauses[2].m_pTryBegin->m_offset, 0u);

    delete[] instrs;
}

// ============================================================================
// Test: debugger async middleware scenario
//
// Simulates a MoveNext() rewritten by the debugger with these EH clauses:
//   Original[0]: inner try/finally for await   -- try [30,55), nested in Original[1]'s try
//   Original[1]: outer state machine try/catch -- try [20,70), handler [70,100)
//   New[2]:      beginMethod try/catch         -- try [5,12),  before everything
//   New[3]:      endMethod(SetException)       -- try [72,85), inside Original[1]'s handler
//   New[4]:      endMethod(SetResult)          -- try [102,115), after everything
//
// Required ordering per ECMA-335:
//   Original[0] before Original[1] (try-in-try)
//   New[3] before Original[1] (try-in-handler)
//   New[2] and New[4] have no nesting constraints
//
// The old sort would leave New[3] after Original[1] because it only checked
// try-in-try containment. This test verifies the fix.
// ============================================================================
TEST(ILRewriterEHSortTest, DebuggerAsyncMiddlewareScenario)
{
    auto* instrs = MakeInstrChain(
        {5, 12, 15, 20, 30, 55, 58, 70, 72, 85, 88, 95, 100, 102, 115, 118, 120, 125});
    const size_t instrCount = 18;

    EHClause clauses[5];
    memset(clauses, 0, sizeof(clauses));

    // Original[0]: inner try/finally -- try [30,55), handler [55,58)
    clauses[0].m_Flags = COR_ILEXCEPTION_CLAUSE_FINALLY;
    clauses[0].m_pTryBegin = FindInstr(instrs, instrCount, 30);
    clauses[0].m_pTryEnd = FindInstr(instrs, instrCount, 55);
    clauses[0].m_pHandlerBegin = FindInstr(instrs, instrCount, 55);
    clauses[0].m_pHandlerEnd = FindInstr(instrs, instrCount, 58); // m_pNext = 70

    // Original[1]: outer try/catch -- try [20,70), handler [70,100)
    clauses[1].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[1].m_pTryBegin = FindInstr(instrs, instrCount, 20);
    clauses[1].m_pTryEnd = FindInstr(instrs, instrCount, 70);
    clauses[1].m_pHandlerBegin = FindInstr(instrs, instrCount, 70);
    clauses[1].m_pHandlerEnd = FindInstr(instrs, instrCount, 95); // m_pNext = 100

    // New[2]: beginMethod try/catch -- try [5,12), handler [12,15)
    clauses[2].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[2].m_pTryBegin = FindInstr(instrs, instrCount, 5);
    clauses[2].m_pTryEnd = FindInstr(instrs, instrCount, 12);
    clauses[2].m_pHandlerBegin = FindInstr(instrs, instrCount, 12);
    clauses[2].m_pHandlerEnd = FindInstr(instrs, instrCount, 15); // m_pNext = 20

    // New[3]: endMethod(SetException) try/catch -- try [72,85), handler [85,88)
    // This try block is INSIDE Original[1]'s handler [70,100)
    clauses[3].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[3].m_pTryBegin = FindInstr(instrs, instrCount, 72);
    clauses[3].m_pTryEnd = FindInstr(instrs, instrCount, 85);
    clauses[3].m_pHandlerBegin = FindInstr(instrs, instrCount, 85);
    clauses[3].m_pHandlerEnd = FindInstr(instrs, instrCount, 88); // m_pNext = 95

    // New[4]: endMethod(SetResult) try/catch -- try [102,115), handler [115,118)
    clauses[4].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[4].m_pTryBegin = FindInstr(instrs, instrCount, 102);
    clauses[4].m_pTryEnd = FindInstr(instrs, instrCount, 115);
    clauses[4].m_pHandlerBegin = FindInstr(instrs, instrCount, 115);
    clauses[4].m_pHandlerEnd = FindInstr(instrs, instrCount, 118); // m_pNext = 120

    ILRewriter::SortEHClauses(clauses, 5);

    // Verify nesting constraints:
    // Original[0] (try-in-try, depth 2) must come before Original[1] (depth 0)
    // New[3] (try-in-handler, depth 1) must come before Original[1] (depth 0)
    //
    // Expected depths: Original[0]=2, Original[1]=0, New[2]=0, New[3]=1, New[4]=0
    // Sort order by (depth desc, offset asc):
    //   depth 2: Original[0] try@30
    //   depth 1: New[3] try@72
    //   depth 0: New[2] try@5, Original[1] try@20, New[4] try@102

    EXPECT_EQ(clauses[0].m_pTryBegin->m_offset, 30u);  // Original[0] -- depth 2
    EXPECT_EQ(clauses[1].m_pTryBegin->m_offset, 72u);  // New[3] -- depth 1
    EXPECT_EQ(clauses[2].m_pTryBegin->m_offset, 5u);   // New[2] -- depth 0
    EXPECT_EQ(clauses[3].m_pTryBegin->m_offset, 20u);  // Original[1] -- depth 0
    EXPECT_EQ(clauses[4].m_pTryBegin->m_offset, 102u); // New[4] -- depth 0

    // Key invariant: New[3] (try@72) appears BEFORE Original[1] (try@20)
    // This was the bug -- the old comparator couldn't detect try-in-handler nesting,
    // so New[3] stayed after Original[1], violating ECMA-335.
    int new3Index = -1, orig1Index = -1;
    for (int i = 0; i < 5; i++)
    {
        if (clauses[i].m_pTryBegin->m_offset == 72) new3Index = i;
        if (clauses[i].m_pTryBegin->m_offset == 20) orig1Index = i;
    }
    EXPECT_LT(new3Index, orig1Index) << "EndMethod(SetException) clause must precede the outer clause it is nested in";

    delete[] instrs;
}

// ============================================================================
// Test: proves the OLD comparator fails the middleware scenario.
// Applies the original broken sort (try-in-try only) and verifies that
// New[3] (EndMethod/SetException, try inside handler) is NOT placed before
// Original[1] -- i.e., the bug is present.
// ============================================================================
TEST(ILRewriterEHSortTest, OldComparatorFailsMiddlewareScenario)
{
    auto* instrs = MakeInstrChain(
        {5, 12, 15, 20, 30, 55, 58, 70, 72, 85, 88, 95, 100, 102, 115, 118, 120, 125});
    const size_t instrCount = 18;

    EHClause clauses[5];
    memset(clauses, 0, sizeof(clauses));

    // Same setup as DebuggerAsyncMiddlewareScenario
    clauses[0].m_Flags = COR_ILEXCEPTION_CLAUSE_FINALLY;
    clauses[0].m_pTryBegin = FindInstr(instrs, instrCount, 30);
    clauses[0].m_pTryEnd = FindInstr(instrs, instrCount, 55);
    clauses[0].m_pHandlerBegin = FindInstr(instrs, instrCount, 55);
    clauses[0].m_pHandlerEnd = FindInstr(instrs, instrCount, 58);

    clauses[1].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[1].m_pTryBegin = FindInstr(instrs, instrCount, 20);
    clauses[1].m_pTryEnd = FindInstr(instrs, instrCount, 70);
    clauses[1].m_pHandlerBegin = FindInstr(instrs, instrCount, 70);
    clauses[1].m_pHandlerEnd = FindInstr(instrs, instrCount, 95);

    clauses[2].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[2].m_pTryBegin = FindInstr(instrs, instrCount, 5);
    clauses[2].m_pTryEnd = FindInstr(instrs, instrCount, 12);
    clauses[2].m_pHandlerBegin = FindInstr(instrs, instrCount, 12);
    clauses[2].m_pHandlerEnd = FindInstr(instrs, instrCount, 15);

    clauses[3].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[3].m_pTryBegin = FindInstr(instrs, instrCount, 72);
    clauses[3].m_pTryEnd = FindInstr(instrs, instrCount, 85);
    clauses[3].m_pHandlerBegin = FindInstr(instrs, instrCount, 85);
    clauses[3].m_pHandlerEnd = FindInstr(instrs, instrCount, 88);

    clauses[4].m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clauses[4].m_pTryBegin = FindInstr(instrs, instrCount, 102);
    clauses[4].m_pTryEnd = FindInstr(instrs, instrCount, 115);
    clauses[4].m_pHandlerBegin = FindInstr(instrs, instrCount, 115);
    clauses[4].m_pHandlerEnd = FindInstr(instrs, instrCount, 118);

    // Apply the OLD broken comparator (try-in-try only, no handler check)
    std::sort(clauses, clauses + 5, [](const EHClause& a, const EHClause& b) {
        return a.m_pTryBegin->m_offset > b.m_pTryBegin->m_offset &&
               a.m_pTryEnd->m_offset < b.m_pTryEnd->m_offset;
    });

    // Find positions of New[3] and Original[1] after the old sort
    int new3Index = -1, orig1Index = -1;
    for (int i = 0; i < 5; i++)
    {
        if (clauses[i].m_pTryBegin->m_offset == 72) new3Index = i;
        if (clauses[i].m_pTryBegin->m_offset == 20) orig1Index = i;
    }

    // The old sort does NOT move New[3] before Original[1] -- this is the bug.
    // New[3]'s try [72,85) is inside Original[1]'s handler [70,100), but the
    // old comparator only checks try-in-try, so it treats them as unrelated.
    EXPECT_GT(new3Index, orig1Index)
        << "Old comparator should fail to order try-in-handler nesting correctly";

    delete[] instrs;
}

// ============================================================================
// Test: single clause -- no sorting needed, should not crash
// ============================================================================
TEST(ILRewriterEHSortTest, SingleClause)
{
    auto* instrs = MakeInstrChain({0, 10, 15, 20});

    EHClause clause;
    memset(&clause, 0, sizeof(clause));
    clause.m_Flags = COR_ILEXCEPTION_CLAUSE_NONE;
    clause.m_pTryBegin = FindInstr(instrs, 4, 0);
    clause.m_pTryEnd = FindInstr(instrs, 4, 10);
    clause.m_pHandlerBegin = FindInstr(instrs, 4, 10);
    clause.m_pHandlerEnd = FindInstr(instrs, 4, 15); // m_pNext = 20

    ILRewriter::SortEHClauses(&clause, 1);

    EXPECT_EQ(clause.m_pTryBegin->m_offset, 0u);

    delete[] instrs;
}

// ============================================================================
// Test: zero clauses -- should not crash
// ============================================================================
TEST(ILRewriterEHSortTest, ZeroClauses)
{
    ILRewriter::SortEHClauses(nullptr, 0);
    // Just verify no crash
}
