# LLM Validation gate (GitLab CI)

Runs the [llm-validation-platform](https://gitlab.ddbuild.io/DataDog/llm-validation-platform) gate on
merge requests that change `AGENTS.md`. It compares the MR's `AGENTS.md` (candidate) against the target
branch's (baseline) by running a real Claude Code agent over this repo for each benchmark case, judging
candidate vs baseline pairwise, and posting a pass/warn/fail report to the GitHub PR.

- **Suite/config:** `.llm-validation/` in this repo (`config.yaml`, `suites/`).
- **Job/script:** `.gitlab/llm-validation/{llm-validation.yml,run.sh}`.

## Enable it

Set `LLMVAL_IMAGE` (in `llm-validation.yml`) to a Linux .NET 8 image, then add to the repo's top-level
`.gitlab-ci.yml`:

```yaml
include:
  - local: .gitlab/llm-validation/llm-validation.yml
```

## How it works

**Single job** (`run.sh`): `authanywhere --audience rapid-ai-platform` → AI Gateway env; ensure node + install
the `claude` CLI; clone + `dotnet build` the platform CLI; run
`llm-validate run --base-sha $CI_MERGE_REQUEST_DIFF_BASE_SHA …`; **print `report.md` to the log + upload it as
an artifact** (the dd-trace-py #17900 model). Posting to the GitHub PR is **optional** — `run.sh` runs
`pr-commenter --header 'LLM Validation' --on-duplicate=replace` only if `pr-commenter` is on `PATH`.
Artifacts: `results.json`, `report.md`, `details.json`.

## Prerequisites / open items (must confirm before first run)

1. **Set `LLMVAL_IMAGE` to a Linux .NET 8 image** (reuse one from the dotnet pipeline). `dotnet` builds/runs
   the platform CLI; `run.sh` installs **node** (the only gap) to get the **Claude Code CLI** — the agent
   under test (there is no .NET Claude Agent SDK). The image also needs `git`/`curl`/`jq` (jq parses the BTI
   token). `authanywhere` is downloaded at runtime. **PR comments:** `pr-commenter` lives only in the
   `benchmarking-platform-tools` image, so on a .NET image the report posts as a **log + artifact** (not a PR
   comment) until we either fetch `pr-commenter` or post via the GitHub API — add that once v1 is working.
2. **AI Gateway entitlement — CONFIRMED (2026-06).** The manual **"llm gateway check"** job minted a
   `rapid-ai-platform` token in CI and got **HTTP 200** from `ai-gateway.us1.ddbuild.io` (Bedrock-backed;
   bare model id `claude-opus-4-8` works). `gateway-check.{yml,sh}` remain for re-checking if auth changes.
3. **Merge-base availability.** `run.sh` fetches the target branch so `git show $CI_MERGE_REQUEST_DIFF_BASE_SHA:AGENTS.md`
   resolves; if `GIT_DEPTH` is very shallow you may need a deeper fetch.

## Cost / tuning

A full 12-case × 3-run pass is ≈ **$16 / ~1 hr** (serial). During rollout, set `LLMVAL_MAX_CASES` and/or
lower `LLMVAL_RUNS` in `llm-validation.yml`. Before running per-PR at scale, parallelize agent runs or
move to the Batches API. Prompt caching (the shared `AGENTS.md`) already cuts cost substantially.

## Enforcement

Advisory for now: the job is `allow_failure: true` and `run.sh` exits 0 regardless of verdict. To enforce,
set `allow_failure: false` and change the last line of `run.sh` to `exit "$GATE_EXIT"`.
