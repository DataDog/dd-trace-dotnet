# LLM Validation gate (GitLab CI)

Runs the [llm-validation-platform](https://gitlab.ddbuild.io/DataDog/llm-validation-platform) gate on
merge requests that change `AGENTS.md`. It compares the MR's `AGENTS.md` (candidate) against the target
branch's (baseline) by running a real Claude Code agent over this repo for each benchmark case, judging
candidate vs baseline pairwise, and posting a pass/warn/fail report to the GitHub PR.

- **Suite/config:** `.llm-validation/` in this repo (`config.yaml`, `suites/`).
- **Job/script:** `.gitlab/llm-validation/{llm-validation.yml,run.sh}`.

## Enable it

Add to the repo's top-level `.gitlab-ci.yml`:

```yaml
include:
  - local: .gitlab/llm-validation/llm-validation.yml
```

## How it works

`run.sh`: `authanywhere --audience rapid-ai-platform` → AI Gateway env (`ANTHROPIC_BASE_URL` +
`ANTHROPIC_CUSTOM_HEADERS`); install the `claude` CLI; clone + `dotnet build` the platform CLI; run
`llm-validate run --base-sha $CI_MERGE_REQUEST_DIFF_BASE_SHA …`; `pr-commenter --header 'LLM Validation'
--on-duplicate=replace` to the GitHub PR. Artifacts: `results.json`, `report.md`, `details.json`.

## Prerequisites / open items (must confirm before first run)

1. **Image must provide `dotnet` 8 SDK + `node`/`npm`.** `dotnet` builds/runs the platform CLI; `node`/`npm`
   are needed to install and run the **Claude Code CLI** (`@anthropic-ai/claude-code`) — the agent under
   test (there is no .NET Claude Agent SDK). `authanywhere` is a standalone binary and needs neither. The
   `benchmarking-platform-tools-ubuntu` image has `pr-commenter`/`git`/`jq`/`curl` but likely **not** .NET
   or node — switch image, install them at job start, or bake a dedicated image. This is the main blocker.
2. **AI Gateway entitlement.** This repo's CI identity must be allowed the `rapid-ai-platform` audience
   (the repo already uses `authanywhere --audience rapid-devex-ci`, but the AI audience is unverified).
   **Confirm it cheaply first** with the one-off check job: `include` `gateway-check.yml` and run the
   manual **"llm gateway check"** job — it mints the token and pings the gateway using only curl/jq (no
   .NET/node). HTTP 200 = entitled (proceed to build the image); 401/403 = request the `rapid-ai-platform`
   grant for dd-trace-dotnet CI from the AI-gateway / dd-source owners.
3. **Merge-base availability.** `run.sh` fetches the target branch so `git show $CI_MERGE_REQUEST_DIFF_BASE_SHA:AGENTS.md`
   resolves; if `GIT_DEPTH` is very shallow you may need a deeper fetch.

## Cost / tuning

A full 12-case × 3-run pass is ≈ **$16 / ~1 hr** (serial). During rollout, set `LLMVAL_MAX_CASES` and/or
lower `LLMVAL_RUNS` in `llm-validation.yml`. Before running per-PR at scale, parallelize agent runs or
move to the Batches API. Prompt caching (the shared `AGENTS.md`) already cuts cost substantially.

## Enforcement

Advisory for now: the job is `allow_failure: true` and `run.sh` exits 0 regardless of verdict. To enforce,
set `allow_failure: false` and change the last line of `run.sh` to `exit "$GATE_EXIT"`.
