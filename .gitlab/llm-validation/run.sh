#!/usr/bin/env bash
# LLM Validation gate runner (GitLab CI). See README.md in this directory.
#
# Flow (mirrors dd-trace-py PR #17900 for auth, and the BP microbenchmarks for PR commenting):
#   1. authanywhere -> Datadog AI Gateway via ANTHROPIC_* env
#   2. install the claude CLI (the agent under test)
#   3. clone + build the platform CLI from DataDog/llm-validation-platform
#   4. run the gate: candidate = this checkout's AGENTS.md, baseline = the MR merge-base
#   5. post report.md to the GitHub PR via pr-commenter
set -euo pipefail

PLATFORM_REF="${LLMVAL_PLATFORM_REF:-main}"
# Case selection + repeats come from the level preset in .llm-validation/config.yaml
# (LLMVAL_LEVEL, default `minimum`); LLMVAL_RUNS / LLMVAL_CASES / LLMVAL_MAX_CASES override it below.

echo "=== LLM Validation: ensure base tools (git, curl, jq) ==="
# The .NET SDK image (Debian) is fairly bare — install the small tools run.sh needs.
if ! { command -v git && command -v curl && command -v jq; } >/dev/null 2>&1; then
  apt-get update >/dev/null 2>&1 \
    && apt-get install -y --no-install-recommends git curl jq ca-certificates >/dev/null 2>&1 \
    || echo "WARN: could not apt-get base tools; assuming they are present."
fi

echo "=== LLM Validation: resolve baseline + skip if AGENTS.md unchanged ==="
# Do this BEFORE the expensive installs (auth/node/.NET/clone/build): on branches that didn't touch the
# prompt, the job exits here in seconds instead of paying to install a whole toolchain just to no-op.
# Now that the job runs on_success (not manual), this early skip is what keeps cost ~zero on non-prompt PRs.
# dd-trace-dotnet GitLab pipelines are branch pipelines (no MR vars), so baseline = merge-base with the
# target branch (default master). The CLI later reads `git show <base-sha>:AGENTS.md`.
BASE_REF="${LLMVAL_BASE_REF:-master}"
git -C "$CI_PROJECT_DIR" fetch --depth 200 origin "$BASE_REF" || true
BASE_SHA="$(git -C "$CI_PROJECT_DIR" merge-base "origin/$BASE_REF" HEAD 2>/dev/null || echo "origin/$BASE_REF")"
echo "baseline = $BASE_SHA (vs $BASE_REF)"
if git -C "$CI_PROJECT_DIR" diff --quiet "$BASE_SHA" HEAD -- AGENTS.md 2>/dev/null; then
  echo "AGENTS.md unchanged vs $BASE_REF — nothing to validate. Exiting 0."
  exit 0
fi

echo "=== LLM Validation: gateway auth (rapid-ai-platform) ==="
curl -fsSL -o /usr/local/bin/authanywhere \
  "https://binaries.ddbuild.io/dd-source/authanywhere/LATEST/authanywhere-linux-amd64"
chmod +x /usr/local/bin/authanywhere

AI_BEARER="$(authanywhere --audience rapid-ai-platform)"   # emits: "Authorization: Bearer <jwt>"
export ANTHROPIC_BASE_URL="https://ai-gateway.us1.ddbuild.io"
export ANTHROPIC_API_KEY="not-set"
export ANTHROPIC_CUSTOM_HEADERS="source: claude-code
org-id: 2
provider: anthropic
claude-code: true
${AI_BEARER}"

echo "=== LLM Validation: ensure node + claude CLI ==="
if ! command -v node >/dev/null 2>&1; then
  echo "node not found — installing via NodeSource (assumes a Debian/Ubuntu base)..."
  curl -fsSL https://deb.nodesource.com/setup_22.x | bash - >/dev/null 2>&1 \
    && apt-get install -y nodejs >/dev/null 2>&1 \
    || { echo "ERROR: could not install node. Point LLMVAL_IMAGE at an image that already has node/npm."; exit 1; }
fi
npm i -g @anthropic-ai/claude-code >/dev/null

echo "=== LLM Validation: ensure .NET 10 SDK ==="
if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet not found — installing .NET 10 SDK (supports the .slnx solution format)..."
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
    && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/local/dotnet --no-path \
    || { echo "ERROR: could not install .NET (egress to dot.net blocked?). Use a baked image — see README."; exit 1; }
  export PATH="/usr/local/dotnet:$PATH"
fi
# Projects target net8.0; let that output run on the .NET 10 runtime that ships with the SDK.
export DOTNET_ROLL_FORWARD=Major
dotnet --version || true

echo "=== LLM Validation: fetch + build platform CLI ($PLATFORM_REF) ==="
BTI_TOKEN="$(authanywhere --audience rapid-devex-ci)"
GITLAB_TOKEN="$(curl -fsSL -H "$BTI_TOKEN" -H 'Content-Type: application/vnd.api+json' \
  "https://bti-ci-api.us1.ddbuild.io/internal/ci/gitlab/token?owner=DataDog&repository=llm-validation-platform" \
  | jq -r '.token // empty')"
[ -n "$GITLAB_TOKEN" ] || echo "WARN: empty GitLab token from BTI (repo may not be a known ddbuild GitLab project)."
git clone --depth 1 --branch "$PLATFORM_REF" \
  "https://gitlab-ci-token:${GITLAB_TOKEN}@gitlab.ddbuild.io/DataDog/llm-validation-platform.git" /tmp/llmval

echo "--- cloned ref=$PLATFORM_REF; what did we get? ---"
git -C /tmp/llmval log --oneline -5 2>/dev/null || true
ls -la /tmp/llmval
if [ ! -f /tmp/llmval/Datadog.LlmValidation.slnx ]; then
  echo "ERROR: cloned repo (ref=$PLATFORM_REF) is missing Datadog.LlmValidation.slnx."
  echo "       Likely the gitlab.ddbuild.io mirror of llm-validation-platform is empty/stale or this ref"
  echo "       isn't mirrored. Remote branches:"
  git -C /tmp/llmval branch -a 2>/dev/null || true
  exit 1
fi
( cd /tmp/llmval && dotnet build -c Release Datadog.LlmValidation.slnx )
CLI_DLL="/tmp/llmval/src/Datadog.LlmValidation.Cli/bin/Release/net8.0/Datadog.LlmValidation.Cli.dll"

echo "=== LLM Validation: run gate (baseline=$BASE_SHA, level=${LLMVAL_LEVEL:-default}) ==="
# Level preset (minimum/medium/full) is defined in .llm-validation/config.yaml; explicit overrides win.
EXTRA=()
[ -n "${LLMVAL_LEVEL:-}" ] && EXTRA+=(--level "$LLMVAL_LEVEL")
[ -n "${LLMVAL_CASES:-}" ] && EXTRA+=(--case "$LLMVAL_CASES")          # override: target specific case id(s)
[ -n "${LLMVAL_MAX_CASES:-}" ] && EXTRA+=(--max-cases "$LLMVAL_MAX_CASES")
[ -n "${LLMVAL_RUNS:-}" ] && EXTRA+=(--runs "$LLMVAL_RUNS")            # override: repeats per case
set +e
dotnet "$CLI_DLL" run \
  --repo "$CI_PROJECT_DIR" \
  --base-sha "$BASE_SHA" \
  --evaluators /tmp/llmval/evaluators \
  --out results.json --report report.md --details details.json \
  "${EXTRA[@]}"
GATE_EXIT=$?
set -e
echo "gate exit code: $GATE_EXIT"

# The report is always available in the job log + as an artifact (the #17900 model).
echo "=== LLM Validation: report ==="
[ -f report.md ] && cat report.md

# Posting to the PR is optional and additive: do it only if pr-commenter is available.
echo "=== LLM Validation: PR comment (optional) ==="
if command -v pr-commenter >/dev/null 2>&1 && [ "${CI_COMMIT_REF_NAME:-}" != "master" ] && [ -f report.md ]; then
  cat report.md | pr-commenter \
    --for-repo="$CI_PROJECT_NAME" \
    --for-pr="$CI_COMMIT_REF_NAME" \
    --header='LLM Validation' \
    --on-duplicate=replace || echo "WARN: pr-commenter failed (non-fatal)"
else
  echo "pr-commenter not on PATH (or master branch) — see report.md in the log above / job artifacts."
fi

# Enforcing: a FAIL verdict fails the job (and thus the pipeline), blocking the merge. The PR comment
# above always posts first, so the verdict is visible regardless. PASS/WARN exit 0.
exit "$GATE_EXIT"
