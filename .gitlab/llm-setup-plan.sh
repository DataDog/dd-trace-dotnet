#!/usr/bin/env bash
# Static inventory/selection validation only: no agents, judges, gateway auth, or quality verdict.
set -euo pipefail
set +x

: "${CI_PROJECT_DIR:?Consumer checkout is required}"
: "${LLMVAL_PLATFORM_PROJECT:?Platform project is required}"
: "${LLMVAL_PLATFORM_REF:?Pinned platform SHA is required}"
[[ "$LLMVAL_PLATFORM_REF" =~ ^[0-9a-f]{40}$ ]] || {
  echo "Setup planning requires an immutable platform commit SHA." >&2
  exit 2
}
cd "$CI_PROJECT_DIR"
# Keep diagnostics separate so the JSON artifact always contains only the plan.
exec > >(tee setup-plan.log) 2>&1
echo "=== LLM setup plan: platform $LLMVAL_PLATFORM_REF ==="

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
echo "Setup plan validated. No model evaluation or quality verdict was produced."
