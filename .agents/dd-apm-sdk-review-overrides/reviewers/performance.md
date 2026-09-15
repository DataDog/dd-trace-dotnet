Override for `reviewers/performance.md` (in the core skill folder) — read that file first, then this.

# Performance — dd-trace-dotnet specifics

This file starts with one confirmed pattern and should grow. Do not treat it as exhaustive.

The source of truth is **AGENTS.md § "Performance Guidelines"**. Apply that section as written; do not paraphrase it into a weaker rule. The critical paths are that section's list — Bootstrap/Startup **and** the per-request Hot Paths (including request/response pipeline). Do not drop items from it.

## No `params` arrays on those paths

A `params` array on a path from that list is a finding, regardless of element type (`object[]`, `int[]`, reference or value) and regardless of whether the helper is new. That includes a new call into an existing `params` helper, or extending one so a critical path now hits it. Provide overloads for the common 0/1/2-arg cases instead.

A `params` helper used only in tests is not this finding. Do not treat startup as cold: AGENTS.md names Bootstrap/Startup as critical path #1.

This pattern is **P1** (SEV-2). It resolves the core performance lens's SEV-2/3 straddle for this allocation to SEV-2 — do not report it as P2.
