Override for `reviewers/performance.md` (in the core skill folder) — read that file first, then this.

# Performance — dd-trace-dotnet specifics

This file starts with one confirmed pattern and should grow. Do not treat it as exhaustive.

The source of truth is **AGENTS.md § "Performance Guidelines"**. Apply that section as written; do not paraphrase it into a weaker rule.

## No `params` arrays on hot paths

Hot paths (span creation/tagging, context propagation, sampling, instrumentation callbacks) must not introduce a new `params object[]` (or `params` of a reference type) helper that the callback hits on every request. Provide overloads for the common 0/1/2-arg cases instead. A `params` helper used only at startup or in tests is not this finding.
