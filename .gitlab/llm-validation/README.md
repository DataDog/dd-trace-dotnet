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
the `claude` CLI; clone + `dotnet build` the platform CLI; run `llm-validate run` with **baseline =
merge-base with `master`** (dd-trace-dotnet's GitLab runs *branch* pipelines, not `merge_request_event`, so
there are no MR vars — we key off the branch like the BP jobs do); **print `report.md` to the log + upload it
as an artifact** (the dd-trace-py #17900 model). Posting to the GitHub PR is **optional** — `run.sh` runs
`pr-commenter --for-pr=$CI_COMMIT_REF_NAME --on-duplicate=replace` only if `pr-commenter` is on `PATH`.
Artifacts: `results.json`, `report.md`, `details.json`.

The job runs **automatically** (`when: on_success`) on every non-`master` pipeline and **blocks the merge on a
FAIL** (`allow_failure: false`). To keep cost ~zero on PRs that don't touch the prompt, `run.sh` self-skips
**early** (exit 0, before any installs) when `AGENTS.md` is unchanged vs `master`.

## Prerequisites / open items (must confirm before first run)

1. **Image must be an approved `registry.ddbuild.io` image.** The k8s runners enforce a registry allowlist
   (`ValidatingAdmissionPolicy 'third-party-registry'`); external images (`mcr.microsoft.com/...`, dockerhub)
   are **denied**. `LLMVAL_IMAGE` is set to `registry.ddbuild.io/images/benchmarking-platform-tools-ubuntu:latest`
   — proven approved (the gateway-check ran on it) and it has `pr-commenter`/git/jq/curl (so PR comments work).
   On that base `run.sh` installs **.NET 8 SDK** (dotnet-install.sh), **node** + the **Claude Code CLI**
   (agent under test — no .NET Agent SDK), and downloads `authanywhere`.
2. **Runtime egress for those installs is the next unknown.** Internal calls work (authanywhere/binaries,
   AI gateway, the gitlab.ddbuild.io clone), but the .NET installer (`dot.net`), NodeSource/npm, and NuGet
   (`api.nuget.org`) are external. If the runner blocks them, **bake a custom approved image**
   (`FROM benchmarking-platform-tools-ubuntu` + .NET 8 + node + `@anthropic-ai/claude-code`, pushed to
   registry.ddbuild.io) so the job makes only internal calls. The first run's log shows exactly which install fails.
3. **AI Gateway entitlement — CONFIRMED (2026-06).** The manual **"llm gateway check"** job minted a
   `rapid-ai-platform` token in CI and got **HTTP 200** from `ai-gateway.us1.ddbuild.io` (Bedrock-backed;
   bare model id `claude-opus-4-8` works). `gateway-check.{yml,sh}` remain for re-checking if auth changes.
4. **Merge-base availability.** `run.sh` resolves baseline = `git merge-base origin/master HEAD` after a
   `--depth 200` fetch of `master`; if history is too shallow it falls back to comparing against `origin/master`.

## Cost / tuning

A full 12-case × 3-run pass is ≈ **$16 / ~1 hr** (serial). During rollout, set `LLMVAL_MAX_CASES` and/or
lower `LLMVAL_RUNS` in `llm-validation.yml`. Before running per-PR at scale, parallelize agent runs or
move to the Batches API. Prompt caching (the shared `AGENTS.md`) already cuts cost substantially.

## Enforcement

**Enforcing (2026-06):** `allow_failure: false` and `run.sh` ends in `exit "$GATE_EXIT"`, so a **FAIL** verdict
fails the job → fails the pipeline → blocks the merge. PASS/WARN exit 0. The PR comment posts before the exit,
so the verdict is always visible. To make the gate advisory again, set `allow_failure: true` (the job will
still post the report but won't block).
