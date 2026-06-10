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
RUNS="${LLMVAL_RUNS:-3}"

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

echo "=== LLM Validation: fetch + build platform CLI ($PLATFORM_REF) ==="
BTI_TOKEN="$(authanywhere --audience rapid-devex-ci)"
GITLAB_TOKEN="$(curl -fsSL -H "$BTI_TOKEN" -H 'Content-Type: application/vnd.api+json' \
  "https://bti-ci-api.us1.ddbuild.io/internal/ci/gitlab/token?owner=DataDog&repository=llm-validation-platform" \
  | jq -r '.token // empty')"
git clone --depth 1 --branch "$PLATFORM_REF" \
  "https://gitlab-ci-token:${GITLAB_TOKEN}@gitlab.ddbuild.io/DataDog/llm-validation-platform.git" /tmp/llmval
( cd /tmp/llmval && dotnet build -c Release Datadog.LlmValidation.slnx )
CLI_DLL="/tmp/llmval/src/Datadog.LlmValidation.Cli/bin/Release/net8.0/Datadog.LlmValidation.Cli.dll"

echo "=== LLM Validation: ensure the merge-base commit is present ==="
# The CLI reads `git show <base-sha>:AGENTS.md`; make sure the target branch is fetched.
git -C "$CI_PROJECT_DIR" fetch --depth 50 origin "${CI_MERGE_REQUEST_TARGET_BRANCH_NAME:-master}" || true

echo "=== LLM Validation: run gate (baseline=$CI_MERGE_REQUEST_DIFF_BASE_SHA, runs=$RUNS) ==="
EXTRA=()
[ -n "${LLMVAL_MAX_CASES:-}" ] && EXTRA+=(--max-cases "$LLMVAL_MAX_CASES")
set +e
dotnet "$CLI_DLL" run \
  --repo "$CI_PROJECT_DIR" \
  --base-sha "$CI_MERGE_REQUEST_DIFF_BASE_SHA" \
  --runs "$RUNS" \
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

# Advisory during the POC: surface the verdict but don't fail the pipeline.
# To enforce later, replace the next line with: exit "$GATE_EXIT"
exit 0
