---
name: find-allocation-opportunities
description: >-
  This skill should be used when the user asks to "find allocations", "allocation review",
  "heap allocation", "reduce allocations", "optimize allocations", "check for allocations",
  "allocation opportunities", "avoid boxing", "avoid allocation", "zero-alloc", "hot path
  review", "GC pressure", "reduce GC", "allocation-free", "avoid heap", "memory pressure",
  or mentions reviewing code for allocation overhead. Also trigger when reviewing PRs that
  touch hot paths (span creation, context propagation, instrumentation callbacks,
  serialization) and the user wants to check for unnecessary allocations. Covers both
  scanning existing code and reviewing diffs/PRs for missed optimization opportunities.
---

# Find Allocation Opportunities

## Why This Matters

The dd-trace-dotnet tracer runs **in-process** with customer applications. Every heap allocation
in hot paths adds GC pressure to the customer's app. The team has developed 22+ proven patterns
for avoiding allocations — this skill codifies that knowledge for consistent application.

## Critical Code Paths

Not all code needs aggressive optimization. Focus effort based on path temperature:

1. **Hot paths** (highest priority): Span creation/tagging, context propagation, sampling
   decisions, instrumentation callbacks (CallTarget `OnMethodBegin`/`OnMethodEnd`), request/response
   pipeline, MessagePack serialization
2. **Startup/bootstrap** (medium priority): Managed loader, tracer initialization, static
   constructors, configuration loading, integration registration
3. **Cold paths** (lower priority): One-time setup, error handling, diagnostic logging,
   configuration changes — still worth optimizing but not critical

## Modes of Operation

### PR Review Mode

Given a PR number, diff, or set of changed files:

1. Fetch the diff (or read the provided files)
2. Identify which changed code touches hot paths vs. cold paths
3. Read `references/anti-patterns.md` and scan the diff for matches
4. For each finding, look up the appropriate fix in `references/patterns.md`
5. Report findings prioritized by path temperature

### Codebase Scan Mode

Given a file, directory, or glob pattern:

1. Read the target files (exclude `Vendors/`, `Generated/`, and `test/` directories)
2. Classify path temperature using Step 1 below
3. Read `references/anti-patterns.md` and search for matches using Grep
4. For each finding, look up the appropriate fix in `references/patterns.md`
5. Report findings prioritized by path temperature

## Workflow

### Step 1: Determine scope and temperature

Classify the target code:
- Files under `ClrProfiler/`, `Agent/MessagePack/`, `Propagators/`, `Tagging/`, `Sampling/`,
  `Processors/` = hot path
- Files under `Configuration/`, `ClrProfiler/Managed.Loader/` = startup path
- Everything else = assess based on call frequency

### Step 2: Load reference material

Read the appropriate reference files based on what the scan reveals:
- **`references/anti-patterns.md`** — The anti-patterns to search for, with grep patterns
- **`references/patterns.md`** — The proven optimization patterns with concrete examples from
  this codebase

### Step 3: Search for anti-patterns

Use Grep to search for the anti-patterns documented in `references/anti-patterns.md`. Each
anti-pattern entry includes suggested search patterns. See the "Search Guidelines" section
at the top of that file for exclusion rules (`Vendors/`, `Generated/`, `test/`).

### Step 4: Assess and report

For each finding, determine:
- **Does this actually cause a heap allocation?** Only report findings that produce heap
  allocations (object/array/string/delegate/boxing). Do not report redundant calls, style
  issues, or other inefficiencies that don't allocate — those are out of scope for this skill.
- Is this on a hot path or eager initialization path? (Check callers if unclear)
  Hot-path allocations are highest priority. Eager initialization allocations matter too —
  they affect cold-start times. Prefer lazy initialization when possible. True one-shot
  cold-path allocations (error handling, rare config changes) are lowest priority.
- Is there already an optimization in place? (e.g., the allocation might be behind an `IsEnabled` guard)
- What's the concrete fix using an existing pattern from this codebase?

### Step 5: Format output

Report each finding as:

```
## [Priority: High/Medium/Low] — [Anti-pattern name]

**Location**: `file_path:line_number`
**Path temperature**: Hot / Startup / Cold
**Issue**: [Brief description of the allocation]
**Fix**: [Concrete code change using a pattern from references/patterns.md]

Before:
```csharp
// allocating code
```

After:
```csharp
// optimized code
```
```

Group findings by priority. Include a summary count at the top.

### Step 6: Verify fixes compile

When suggesting concrete code changes, verify they compile for all target frameworks:
```
dotnet build tracer/src/Datadog.Trace/ -c Release -f net6.0
```
Drop the `-f` flag to test all TFMs if the change involves `#if` guards.

## Important Caveats

- Readability matters — not every allocation needs eliminating. Focus on hot paths and
  frequently-called code.
- **Check `#if` preprocessor context**: Many patterns (`Span<T>`, `stackalloc`,
  `ValueStringBuilder`, `ValueTask`, `SpanCharSplitter`) require `NETCOREAPP` or
  `NETCOREAPP3_1_OR_GREATER`. Before suggesting these patterns, check whether the code is
  inside an `#if NETFRAMEWORK` block. When the code must work on both runtimes, provide
  both the optimized path and the fallback wrapped in `#if` / `#else`.
- `stackalloc` buffers should have a reasonable size threshold (typically 256-512 bytes).
  Fall back to `ArrayPool` for larger buffers.
- Verify that "optimized" code actually compiles for all target frameworks before suggesting it.

## Reference Files

- **`references/patterns.md`** — Complete catalog of 22+ allocation-avoidance patterns used in
  this codebase, organized by category with concrete file:line examples
- **`references/anti-patterns.md`** — Anti-patterns to detect, with grep search patterns and
  suggested fixes. Covers both tracer-specific patterns (from AGENTS.md Performance Guidelines)
  and general .NET allocation pitfalls
