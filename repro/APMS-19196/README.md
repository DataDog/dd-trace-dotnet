# APMS-19196 — InvalidProgram EH Clause Sort Bug

**Status:** ✅ **FIXED** - PR #8428 resolves the issue.

Minimal ASP.NET Core app with complex nested try-catch-finally structure that triggers the EH clause sorting issue in Datadog .NET Tracer v3.41.0.

## Quick Start

```bash
cd repro/APMS-19196

# Test with affected version v3.41.0 (demonstrates the issue)
bash reproduce-crash.sh

# Test with fixed tracer (issue is resolved)
bash test-fixed-tracer.sh
```

---

## 🔬 Root Cause Analysis

### The Affected Code (v3.41.0)

The bug occurs in `tracer/src/Datadog.Tracer.Native/il_rewriter.cpp` lines 689-692:

```cpp
std::sort(m_pEH, m_pEH + m_nEH, [](EHClause a, EHClause b) {
    return a.m_pTryBegin->m_offset > b.m_pTryBegin->m_offset &&
           a.m_pTryEnd->m_offset < b.m_pTryEnd->m_offset;
});
```

### Why This Logic Causes Issues

The `&&` operator creates a comparator that **only works for perfect nesting** but fails for other patterns that occur in real IL code.

## 🎯 The Three Breaking Cases

### **Case 1: Disjoint Ranges**
```
Clause A: [10----20]
Clause B:           [30----40]

comp(a,b): (10 > 30) && (20 < 40) = false && true = FALSE
comp(b,a): (30 > 10) && (40 < 20) = true && false = FALSE
```
**Both return false** → violates strict weak ordering → `std::sort` corrupts the array

#### C# Example:
```csharp
// Two separate try-catch blocks
try {
    Console.WriteLine("Block 1");    // EH Clause A: [offset 10-20]
} catch { }

try {
    Console.WriteLine("Block 2");    // EH Clause B: [offset 30-40] 
} catch { }
```

### **Case 2: Overlapping Ranges**
```
Clause A: [10-------25]
Clause B:      [20-------35]

comp(a,b): (10 > 20) && (25 < 35) = false && true = FALSE  
comp(b,a): (20 > 10) && (35 < 25) = true && false = FALSE
```
**Both return false** → same corruption problem

#### C# Example:
```csharp
try {
    // Outer try creates complex overlapping patterns in async state machines
    var task = Task.Run(async () => {
        try {
            await Task.Delay(1);  // Inner async try overlaps outer
        } catch { }
    });
    await task;
} catch { }
```

### **Case 3: Identical Try Ranges** ⭐ **THIS BREAKS OUR REPRODUCTION**
```
Clause A: [10----20]  (catch handler)
Clause B: [10----20]  (finally handler - same try range!)

comp(a,b): (10 > 10) && (20 < 20) = false && false = FALSE
comp(b,a): (10 > 10) && (20 < 20) = false && false = FALSE
```
**Both return false** → no way to order them

#### C# Examples That Create This:

**Multiple Catch Blocks:**
```csharp
try {
    SomeRiskyOperation();           // Try range: [100-200]
} 
catch (ArgumentException ex) {      // EH Clause A: tryRange[100,200]
    Console.WriteLine("Arg error");
}
catch (Exception ex) {              // EH Clause B: tryRange[100,200] (SAME!)
    Console.WriteLine("General error");
}
```

**Catch + Finally:**
```csharp
try {
    await DoSomething();            // Try range: [50-150]
}
catch (Exception ex) {              // EH Clause A: tryRange[50,150]
    Console.WriteLine("Error");
}
finally {                           // EH Clause B: tryRange[50,150] (SAME!)
    Console.WriteLine("Cleanup");
}
```

**Our Crashing Pattern (Every Level Has This):**
```csharp
try {
    Console.WriteLine($"[L9] **CRASH TRIGGER** {requestId}");
    await _next(context);
}
catch (Exception ex) {              // EH Clause A: same try range
    Console.WriteLine("Exception");
}
finally {                           // EH Clause B: same try range  
    Console.WriteLine("Finally");
}
```

---

## 🔧 The Fix Explained

### The Problem: Raw Offsets vs Semantic Depth

**Original approach:** Try to determine nesting AND sort in one step using raw IL offsets
**The fixed approach:** Calculate semantic nesting depth first, then sort by that depth

### The Two-Step Solution

#### Step 1: Calculate Nesting Depth
```cpp
// Count how many other clauses contain each clause
for (unsigned i = 0; i < nEH; i++) {
    for (unsigned j = 0; j < nEH; j++) {
        if (i == j) continue;
        
        // Is clause i nested inside clause j's try or handler region?
        bool inTry = (iTryBegin >= jTryBegin && iTryEnd <= jTryEnd) && !(same range);
        bool inHandler = (iTryBegin >= jHandlerBegin && iTryEnd <= jHandlerEnd) && !(same range);
        
        if (inTry || inHandler) {
            depth[i]++;  // This clause is nested inside j
        }
    }
}
```

#### Step 2: Sort By Depth (Not Raw Offsets)
```cpp
std::sort(indices, indices + nEH, [&](unsigned a, unsigned b) {
    if (depth[a] != depth[b]) return depth[a] > depth[b];  // Deeper first (ECMA-335)
    return pEH[a].m_pTryBegin->m_offset < pEH[b].m_pTryBegin->m_offset;  // Offset for tie-breaking
});
```

### Why This Works ✅

- **Always gives consistent results:** Every pair of clauses gets a definitive ordering
- **Follows ECMA-335:** "Most deeply nested try blocks shall come before the try blocks that enclose them"
- **Proper strict weak ordering:** Satisfies all mathematical requirements for `std::sort`

---

## 💡 Key Insights Discovered

### 1. **The Crash Happens at JIT Time, Not Runtime**
The `InvalidProgramException` occurs when the CLR tries to JIT compile the method and sees the corrupted EH table - no exceptions need to be thrown.

### 2. **Why Console.WriteLine Calls Are Critical**
These prevent compiler optimizations that would eliminate empty try blocks, preserving the problematic EH structure needed to trigger the bug.

### 3. **Offsets vs Depth**
- **Offsets** are good for: "Is A contained within B?" 
- **Offsets** are bad for: "Should A come before B in sorted order?"
- **Depth** captures the semantic relationship ECMA-335 actually cares about

### 4. **Why One-Step Doesn't Work**
Comparison functions in `std::sort` only see 2 elements at a time, but depth calculation requires seeing ALL elements. You can't do global analysis inside local pairwise comparisons.

### 5. **Environment Variables Explained**
- `DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL=true` - **Testing flag**: Instruments ALL methods immediately at JIT time (bypasses normal triggering)
- `DD_EXCEPTION_REPLAY_ENABLED=true` - **Production feature**: Instruments methods reactively after exceptions occur
- For crash reproduction, we need immediate instrumentation, hence the first flag.

---

## 🧪 Test Results

### With Affected Version (v3.41.0):
```
System.InvalidProgramException: Common Language Runtime detected an invalid program.
   at Program.<<Main>$>g__CrashTriggerEndpoint|0_0(HttpContext context)
```

### With Fixed Tracer:
```
[L1] 1234
[L5] 1234
[L6] 1234
[L7] 1234
[L8] 1234
[L9] **CRASH TRIGGER** 1234
[L9] Success 1234
HTTP 200 OK - Success
```

---

## 📋 Reproduction Details

### Requirements
- **Native Linux x86_64** (WSL2 on Windows works perfectly)
- **.NET 9** runtime  
- **Datadog .NET Tracer** (v3.41.0 for issue reproduction, or current version for fixed behavior)

### Key Environment Variables
```bash
export DD_INTERNAL_DEBUGGER_INSTRUMENT_ALL=true  # Critical: Instruments at JIT time
export DD_TRACE_DEBUG=false                      # Timing-sensitive crash
```

### The Crashing Pattern
**6+ levels of nested try-catch-finally** with identical try ranges:
- Each level has both `catch` and `finally` handlers
- Creates multiple EH clauses with identical try ranges  
- Pattern: `ROOT` → `L1` → `L5` → `L6` → `L7` → `L8` → `L9` (skipping L2,L3,L4)
- The original comparator has issues with identical-range clauses

---

## 🎯 Summary

**APMS-19196** was caused by a fundamentally flawed EH clause sorting algorithm that:
1. **Used raw IL offsets for comparison instead of semantic nesting depth**
2. **Failed on common IL patterns** (multiple handlers, async state machines)  
3. **Violated strict weak ordering requirements** for `std::sort`
4. **Corrupted the EH table**, causing `InvalidProgramException` at JIT time

**The fix** properly implements ECMA-335 requirements by calculating nesting depth first, then sorting by that semantic relationship rather than raw byte positions.

**This reproduction serves as a valuable test case** for validating EH clause sorting logic and preventing regression of this subtle but critical bug.

**Jira:** APMS-19196