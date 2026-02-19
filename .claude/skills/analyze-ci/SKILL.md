---
name: analyze-ci
description: Unified CI failure analysis across all CI systems (Azure DevOps, GitLab CI, GitHub Actions). Combines AzDO pipeline analysis with DDCI/GitLab log fetching for a complete view of PR health.
argument-hint: [pr NUMBER | build BUILD_ID]
disable-model-invocation: true
user-invocable: true
allowed-tools: WebFetch, Bash(az devops invoke:*), Bash(az pipelines build list:*), Bash(az pipelines build show:*), Bash(az pipelines runs artifact list:*), Bash(az pipelines runs list:*), Bash(az pipelines runs show:*)
---

# Unified CI Failure Analysis

Analyze CI failures across **all** CI systems used by dd-trace-dotnet: Azure DevOps, GitLab CI (via DDCI), and GitHub Actions.

## Arguments

- **`pr <NUMBER>`** — Analyze all CI failures for a GitHub PR (default: current branch's PR)
- **`build <BUILD_ID>`** — Analyze a specific Azure DevOps build only
- No arguments — Auto-detect PR from current branch

Arguments are available as: `$ARGUMENTS`

## Step 1: Get PR Checks (Single Source of Truth)

Fetch all CI checks for the PR:

```bash
gh pr checks --json link,state,name
```

If `$ARGUMENTS` starts with `pr`, use:
```bash
gh pr checks $PR_NUMBER --repo DataDog/dd-trace-dotnet --json link,state,name
```

If `$ARGUMENTS` starts with `build`, skip to Step 3 (Azure DevOps only).

**If "no pull requests found"**, fall back to commit SHA:
```bash
commit_sha=$(git rev-parse HEAD)
repo=$(gh repo view --json nameWithOwner -q .nameWithOwner)
pr_number=$(gh api "repos/${repo}/commits/${commit_sha}/pulls" --jq '.[0].number')
gh pr checks "$pr_number" --json link,state,name
```

From the output, categorize checks into three buckets:

| CI System | How to Identify | Example Link |
|---|---|---|
| **Azure DevOps** | Link contains `dev.azure.com` | `https://dev.azure.com/datadoghq/dd-trace-dotnet/_build/results?buildId=196122` |
| **GitLab CI / DDCI** | Link contains `gitlab.ddbuild.io` or `mosaic.us1.ddbuild.io` | `https://gitlab.ddbuild.io/.../builds/1438941054` |
| **GitHub Actions** | Link contains `github.com/.../actions` | `https://github.com/DataDog/dd-trace-dotnet/actions/runs/...` |

Also extract:
- **AzDO Build ID**: from `?buildId=<ID>` in any `dev.azure.com` link
- **DDCI request_id**: from the **"DDCI Task Sourcing"** check link: `https://mosaic.us1.ddbuild.io/change-request/<request_id>`

## Step 2: Quick Failure Summary

Count and list failures by CI system. Present a quick overview:

```markdown
# CI Status for PR #<NUMBER>

## Overview
- **Azure DevOps**: X passed, Y failed, Z pending
- **GitLab CI**: X passed, Y failed, Z pending
- **GitHub Actions**: X passed, Y failed, Z pending

## Failed Checks
<list failed check names grouped by CI system>
```

If everything is passing, say so and stop.

If there are failures, proceed to analyze each CI system **in parallel** where possible.

## Step 3: Azure DevOps Analysis

Reference: [troubleshoot-ci-build skill](../troubleshoot-ci-build/SKILL.md)

Use the AzDO Build ID extracted from Step 1 (or from `$ARGUMENTS` if `build <BUILD_ID>`).

### Fetch build timeline:
```bash
az devops invoke \
  --area build --resource timeline \
  --route-parameters project=dd-trace-dotnet buildId=$BUILD_ID \
  --org https://dev.azure.com/datadoghq \
  --api-version 6.0 \
  --output json > "$SCRATCHPAD/build-$BUILD_ID-timeline.json"
```

### Extract failures:
```bash
# Failed tasks
cat "$SCRATCHPAD/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .name' | \
  sort | uniq -c | sort -rn

# Failed jobs (platforms affected)
cat "$SCRATCHPAD/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Job") | .name'

# Failed test names from error messages
cat "$SCRATCHPAD/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .issues[]? | .message' \
  > "$SCRATCHPAD/build-$BUILD_ID-errors.txt"
```

### Categorize using failure-patterns.md:
Reference [failure-patterns.md](../troubleshoot-ci-build/failure-patterns.md) to classify:
- **Real Failures**: Test assertions, compilation errors, segfaults, missing spans
- **Flaky Tests**: Tests with `previousAttempts > 0`, stack walking failures, known intermittent tests
- **Infrastructure Issues**: Docker rate limiting, network timeouts, disk space, job timeouts

## Step 4: GitLab CI / DDCI Analysis

Use the DDCI `request_id` extracted from Step 1.

### List failed DDCI jobs:
```bash
${CLAUDE_PLUGIN_ROOT}/skills/fetch-ci-results/scripts/get_ddci_logs.sh --list-failed <request_id>
```

Output is tab-separated: `job_id`, `job_name`, `status`, `failure_reason`.

If `CLAUDE_PLUGIN_ROOT` is not set, find the script at the datadog-claude-plugins cache path. Check:
```bash
ls ~/.claude/plugins/cache/datadog-claude-plugins/dd/*/skills/fetch-ci-results/scripts/get_ddci_logs.sh
```

### Fetch logs for each failed job:
```bash
${CLAUDE_PLUGIN_ROOT}/skills/fetch-ci-results/scripts/get_ddci_logs.sh <job_id> <request_id> --summary
```

If the script times out, the user likely needs to connect AppGate VPN.

If the script is not available (no DD plugin installed), fall back to checking the GitLab job links from `gh pr checks` output and report them as links for manual inspection.

## Step 5: GitHub Actions Analysis

For any failed GitHub Actions checks, fetch logs:
```bash
# Extract run_id from the link URL
gh run view <run_id> --log-failed
```

## Step 6: Present Combined Analysis

Present a unified report:

```markdown
# CI Failure Analysis for PR #<NUMBER>

## Summary
| CI System | Status | Failed | Details |
|---|---|---|---|
| Azure DevOps | X/Y passed | <count> failures | [View build](<azdo_link>) |
| GitLab CI | X/Y passed | <count> failures | [View pipeline](<gitlab_link>) |
| GitHub Actions | X/Y passed | <count> failures | [View runs](<gh_link>) |

## Azure DevOps Failures
<For each failure: task name, job/platform, category (real/flaky/infra), brief description>

## GitLab CI Failures
<For each failure: job name, failure reason, log excerpt>

## GitHub Actions Failures
<For each failure: workflow name, error summary>

## Recommendations
1. **Retry-worthy** (infra/flaky): <list>
2. **Needs investigation** (real failures): <list with root cause analysis>
3. **Needs code fix**: <list with proposed changes>
```

## Step 7: Offer Next Steps

Ask the user what they want to do:
1. **Deep dive** into a specific failure (download logs, compare with master)
2. **Propose fixes** for real failures
3. **Retry** flaky/infra jobs (use DDCI retry script for GitLab jobs)
4. **Compare with master** to identify new vs pre-existing failures

For deep AzDO analysis (master comparison, log download), follow the Phase 2 steps in [troubleshoot-ci-build](../troubleshoot-ci-build/SKILL.md).

## Error Handling

- **DDCI script not found**: Fall back to reporting GitLab failures as links only
- **DDCI timeout**: Ask user to connect AppGate VPN
- **AzDO timeline fetch fails**: Report AzDO failures as links from `gh pr checks`
- **No failures found**: Confirm all green, note any pending/in-progress checks
- **`build` mode**: Skip GitLab/GitHub Actions analysis, only do AzDO

## Windows CLI Notes

- Use scratchpad directory from system prompt, never `/tmp`
- Save JSON to files before querying with jq
- Use timeline `.log.url` for AzDO logs (avoid `--resource logs` API, returns HTTP 500)
- Avoid piping jq to `head` (causes "Invalid argument")
- Quote `$top` in az CLI: `'$top=10'`
