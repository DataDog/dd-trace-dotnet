# /analyze-ci

Unified CI failure analysis for dd-trace-dotnet. Analyzes failures across **all three** CI systems in a single command:

- **Azure DevOps** — Build/test pipeline (timeline analysis, failure categorization, master comparison)
- **GitLab CI / DDCI** — Serverless benchmarks, publishing, validation jobs (log fetching via DDCI API)
- **GitHub Actions** — Static analysis, code freeze, snapshot verification

## Usage

```bash
/analyze-ci              # Analyze current branch's PR
/analyze-ci pr 8218      # Analyze a specific PR
/analyze-ci build 196122 # Analyze a specific Azure DevOps build only
```

## Prerequisites

- `gh` (GitHub CLI) — authenticated
- `az` (Azure CLI) — for AzDO timeline queries (public API, no auth needed)
- `jq` — for JSON parsing
- `ddtool` — for DDCI/GitLab log access (install: `brew install datadog/tap/ddtool`)
- AppGate VPN — for DDCI API access

## How It Works

1. Fetches `gh pr checks` to get all CI check statuses in one call
2. Categorizes checks by CI system (AzDO / GitLab / GitHub Actions)
3. For each system with failures, fetches logs and analyzes root causes
4. Presents a unified summary with categorized failures and recommendations
5. Offers deep-dive options (master comparison, log analysis, retry)

## Relationship to Other Skills

This skill orchestrates two specialized skills:
- **[troubleshoot-ci-build](../troubleshoot-ci-build/README.md)** — Azure DevOps deep analysis
- **dd:ci:fix** (Datadog plugin) — GitLab CI/DDCI log fetching scripts
