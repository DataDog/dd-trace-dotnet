#!/usr/bin/env bash
# Validate the full inventory/selection, then run one live minimal/default setup case.
# Initial runs=1; the consumer's confirm-on-fail policy may add trials.
set -euo pipefail
set +x

: "${CI_PROJECT_DIR:?Consumer checkout is required}"
: "${LLMVAL_PLATFORM_PROJECT:?Platform project is required}"
: "${LLMVAL_PLATFORM_REF:?Pinned platform SHA is required}"
: "${LLMVAL_SETUP_CLAUDE_VERSION:?Pinned Claude CLI version is required}"
[[ "$LLMVAL_PLATFORM_REF" =~ ^[0-9a-f]{40}$ ]] || {
  echo "Setup smoke requires an immutable platform commit SHA." >&2
  exit 2
}
cd "$CI_PROJECT_DIR"
# Keep diagnostics separate so the JSON artifact always contains only the plan.
exec > >(tee setup-smoke.log) 2>&1
echo "=== LLM setup smoke: platform $LLMVAL_PLATFORM_REF ==="

if ! command -v git >/dev/null || ! command -v curl >/dev/null || ! command -v jq >/dev/null; then
  apt-get update
  apt-get install -y --no-install-recommends git curl jq ca-certificates
fi
if ! command -v authanywhere >/dev/null; then
  curl -fsSL -o /usr/local/bin/authanywhere \
    https://binaries.ddbuild.io/dd-source/authanywhere/LATEST/authanywhere-linux-amd64
  chmod +x /usr/local/bin/authanywhere
fi

# Use the same platform-source credential exchange as the existing evaluation job.
platform_owner="${LLMVAL_PLATFORM_PROJECT%%/*}"
platform_repository="${LLMVAL_PLATFORM_PROJECT##*/}"
bti_header="$(authanywhere --audience rapid-devex-ci)"
gitlab_token="$(curl -fsSL -H "$bti_header" -H 'Content-Type: application/vnd.api+json' \
  "https://bti-ci-api.us1.ddbuild.io/internal/ci/gitlab/token?owner=${platform_owner}&repository=${platform_repository}" \
  | jq -er '.token | select(type == "string" and length > 0)')"
unset bti_header
platform_directory="$(mktemp -d /tmp/llmval-setup-plan.XXXXXXXX)"
git -C "$platform_directory" init -q
# Supply credentials only to this fetch; never embed them in the remote URL or artifact.
gitlab_authorization="$(printf 'gitlab-ci-token:%s' "$gitlab_token" | base64 | tr -d '\n')"
GIT_CONFIG_COUNT=1 GIT_CONFIG_KEY_0=http.extraHeader \
  GIT_CONFIG_VALUE_0="Authorization: Basic $gitlab_authorization" \
  git -C "$platform_directory" fetch --depth 1 \
    "https://gitlab.ddbuild.io/${LLMVAL_PLATFORM_PROJECT}.git" "$LLMVAL_PLATFORM_REF"
unset gitlab_token gitlab_authorization
git -C "$platform_directory" checkout -q --detach FETCH_HEAD
test "$(git -C "$platform_directory" rev-parse HEAD)" = "$LLMVAL_PLATFORM_REF"

if ! command -v dotnet >/dev/null; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$platform_directory/dotnet-install.sh"
  bash "$platform_directory/dotnet-install.sh" --channel 10.0 --install-dir /usr/local/dotnet --no-path
  export PATH="/usr/local/dotnet:$PATH"
fi
export DOTNET_ROLL_FORWARD=Major
# SDK selection follows the working directory, even when the project path is absolute.
# Build/run from the platform checkout so the consumer's global.json cannot select its SDK.
(
  cd "$platform_directory"
  dotnet --version
  dotnet build src/Datadog.LlmValidation.Cli/Datadog.LlmValidation.Cli.csproj -c Release
  dotnet src/Datadog.LlmValidation.Cli/bin/Release/net8.0/Datadog.LlmValidation.Cli.dll \
    run --repo "$CI_PROJECT_DIR" --comparison setup --dry-run --level full > "$CI_PROJECT_DIR/setup-plan.json"
)

# A successful process alone does not establish that the intended full-suite plan was produced.
consumer_commit="$(git rev-parse HEAD)"
jq -e --arg commit "$consumer_commit" '
  .Comparison == "setup" and .DryRun == true and .Status == "plan-only"
  and .Coverage == "full" and .Inventory.Commit == $commit
  and (.SelectedCases | length > 0) and (.Inventory.Files | length > 0)
' setup-plan.json >/dev/null
jq '{Status, Coverage, Preset, Runs, SelectedCases, ModeExcludedCases,
     SetupFileCount: (.Inventory.Files | length), Commit: .Inventory.Commit}' setup-plan.json
echo "Full setup plan validated. Starting the one-case live smoke (subset coverage)."

# Match the existing gate's toolchain/authentication, with a fixed CLI version for pilot reproducibility.
if ! command -v node >/dev/null || ! command -v npm >/dev/null; then
  curl -fsSL https://deb.nodesource.com/setup_22.x | bash -
  apt-get install -y nodejs
fi
npm install -g "@anthropic-ai/claude-code@$LLMVAL_SETUP_CLAUDE_VERSION"
claude --version
gateway_header="$(authanywhere --audience rapid-ai-platform)"
export ANTHROPIC_BASE_URL="${ANTHROPIC_BASE_URL:-https://ai-gateway.us1.ddbuild.io}"
export ANTHROPIC_CUSTOM_HEADERS="source: claude-code
org-id: 2
provider: anthropic
claude-code: true
${gateway_header}"
unset gateway_header

setup_exit=0
(
  cd "$platform_directory" || exit 2
  dotnet src/Datadog.LlmValidation.Cli/bin/Release/net8.0/Datadog.LlmValidation.Cli.dll \
    run --repo "$CI_PROJECT_DIR" --comparison setup \
    --case dotnet-tracer-repo-nav-integration-001 --runs 1 --concurrency 2 --require-complete \
    --out "$CI_PROJECT_DIR/setup-results.json" \
    --details "$CI_PROJECT_DIR/setup-details.json" \
    --report "$CI_PROJECT_DIR/setup-report.md"
) || setup_exit=$?
unset ANTHROPIC_CUSTOM_HEADERS

# Preserve execution/incompleteness errors; a complete quality FAIL (1) still has reviewable artifacts.
if [ "$setup_exit" -ne 0 ] && [ "$setup_exit" -ne 1 ]; then
  exit "$setup_exit"
fi
jq -e --arg commit "$consumer_commit" '
  .Setup.Complete == true and .Setup.Simulated == false and .Setup.Coverage == "subset"
  and .Setup.Inventory.Commit == $commit and .SelectedCaseCount == 1 and .Runs == 1
  and (.InfrastructureErrors | length == 0)
' setup-results.json >/dev/null
jq -e '
  (.Cases | length == 1) and .Cases[0].CaseId == "dotnet-tracer-repo-nav-integration-001"
  and (.Cases[0].Runs | length > 0)
  and ([.Cases[0].Runs[] |
    (.Inconclusive == false and (.Pairwise | length == 2)
      and (.Baseline.Text | length > 0) and (.Candidate.Text | length > 0))] | all)
' setup-details.json >/dev/null
echo "Live setup smoke completed. Review setup-report.md and setup-details.json; this is not full-suite evaluation."
exit "$setup_exit"
