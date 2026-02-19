# CI Failure Investigation Guide (for AI Agents)

This documents the most efficient workflow for identifying CI failures in this repository. The CI infrastructure spans **GitHub Actions**, **Azure DevOps (AzDO)**, and **GitLab**.

## Step 1: Determine the Scope

- **PR checks**: Use `gh pr checks <PR_NUMBER>` for a quick text summary.
- **Post-merge master failures**: The PR checks may all pass, but the master branch pipeline (triggered by the merge commit push) is a separate run and can fail independently. You must check the **merge commit's status on master**, not the PR checks.

## Step 2: Get the Merge Commit SHA

```bash
gh pr view <PR_NUMBER> --json mergeCommit --jq '.mergeCommit.oid'
```

## Step 3: Check Combined Status on Master

```bash
gh api "repos/DataDog/dd-trace-dotnet/commits/<SHA>/status" --jq ".state"
```

If `success`, there are no failures. If `failure` or `pending`, proceed.

## Step 4: Find the Failing Status Contexts

**IMPORTANT**: There can be 100+ statuses per commit. The `/status` endpoint returns only the first page (100). Use the paginated `/statuses` endpoint instead:

```bash
gh api --paginate "repos/DataDog/dd-trace-dotnet/commits/<SHA>/statuses?per_page=100" \
  --jq '.[] | select(.state == "failure") | {context: .context, description: .description, target_url: .target_url}'
```

This gives you:
- **`context`**: The job name (e.g., `integration_tests_linux`, `unit_tests_arm64`)
- **`description`**: A short summary of the failure
- **`target_url`**: Direct link to the AzDO/GitLab build (contains the **build ID**)

Extract the AzDO build ID from the `target_url` — it's the `buildId` query parameter (e.g., `buildId=196053`).

## Step 5: Get Failure Details from Azure DevOps

The AzDO timeline API is **public** (no auth required) and contains the full job/task hierarchy with results and error messages:

```bash
curl -s "https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/<BUILD_ID>/timeline?api-version=7.0"
```

### Find failed/canceled records

Parse the JSON for records with `result == "failed"` or `result == "canceled"`. The `issues` array on each record contains error messages.

Recommended Python one-liner:

```bash
curl -s "https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/<BUILD_ID>/timeline?api-version=7.0" | python -c "
import json,sys
data=json.load(sys.stdin)
for r in data.get('records',[]):
    if r.get('result') in ('failed','canceled'):
        t,n,res = r.get('type','?'), r.get('name','?'), r.get('result')
        print(f'{t}: {n} -> {res}')
        for i in r.get('issues',[])[:3]:
            print(f'  {i.get(\"type\",\"?\")}: {i.get(\"message\",\"\")[:300]}')
"
```

### Drill into a failed Stage/Phase

To find which specific job inside a failed stage/phase failed, filter by `parentId`:

```bash
curl -s "https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/<BUILD_ID>/timeline?api-version=7.0" | python -c "
import json,sys
data=json.load(sys.stdin)
records=data.get('records',[])
for r in records:
    if r.get('result') in ('failed','canceled') and r.get('type') in ('Job','Phase','Stage'):
        print(f'{r[\"type\"]}: {r[\"name\"]} -> {r[\"result\"]}')
        for i in r.get('issues',[])[:3]:
            print(f'  {i.get(\"message\",\"\")[:300]}')
        if r.get('type') == 'Job':
            for ch in records:
                if ch.get('parentId') == r['id'] and ch.get('result') not in ('succeeded','skipped',None):
                    print(f'  Task: {ch.get(\"name\",\"?\")} -> {ch.get(\"result\")}')
                    for i in ch.get('issues',[])[:2]:
                        print(f'    {i.get(\"message\",\"\")[:200]}')
"
```

### Step 5b: Get Exact Test Names from Task Logs

The timeline tells you *which job* failed, but not *which test*. To get specific test names and error messages, you must fetch the task logs.

#### Listing all tasks in failed jobs

**IMPORTANT:** Always list ALL tasks (not just failed ones) in a failed job. A job can fail because of a post-test task (e.g., `CheckBuildLogsForErrors`) even though the actual test-running task succeeded. You need the full task list to:
- Identify *which* task actually failed (it may not be `IntegrationTests`)
- Find the log URL for the *integration test* task (which succeeded) when you need to look up a specific test
- Understand the job structure (build → test → publish → log-check)

```bash
curl -s "https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/<BUILD_ID>/timeline?api-version=7.0" | python -c "
import json,sys
data=json.load(sys.stdin)
records=data.get('records',[])
failed_jobs = [r for r in records if r.get('result') in ('failed','canceled') and r.get('type') == 'Job']
for job in failed_jobs:
    print(f'=== JOB: {job[\"name\"]} -> {job[\"result\"]} ===')
    for i in job.get('issues',[])[:5]:
        print(f'  {i.get(\"message\",\"\")[:300]}')
    children = [r for r in records if r.get('parentId') == job['id']]
    for ch in sorted(children, key=lambda x: x.get('order',0)):
        res = ch.get('result','?')
        if res not in ('skipped',):
            log_url = ch.get('log',{}).get('url','')
            print(f'  [{res}] {ch.get(\"name\",\"?\")}')
            if log_url:
                print(f'    log: {log_url}')
"
```

Note: this prints **all non-skipped tasks** (including succeeded ones) so you can see the full picture and pick the right log URL.

#### Downloading logs

**IMPORTANT (Windows):** `curl` piped through bash on Windows is unreliable for these endpoints. Use Python `urllib.request` instead to download logs to a temp file:

```bash
python -c "
import urllib.request
data=urllib.request.urlopen('<LOG_URL>').read().decode('utf-8','replace')
with open('/tmp/ci_log.txt','w',encoding='utf-8') as f:
    f.write(data)
print(f'Saved {len(data.split(chr(10)))} lines')
"
```

#### Searching logs for failures

Logs can be very large (thousands of lines, megabytes). **Do NOT try to read them fully.** Use Grep to search for failure indicators:

```bash
# Primary: find failed xUnit tests
Grep pattern="\[FAIL\]" path=/tmp/ci_log.txt -C 2

# Secondary: find error messages and stack traces
Grep pattern="Error Message|Stack Trace" path=/tmp/ci_log.txt -A 5

# Summary line: shows total passed/failed/skipped counts
Grep pattern="Failed!" path=/tmp/ci_log.txt -C 3
```

The `[FAIL]` pattern gives the exact fully-qualified test name. The `Failed!` summary line gives the pass/fail/skip counts for the test assembly. The `Error Message` + `Stack Trace` lines immediately follow each `[FAIL]` and give the exception type and call stack.

#### Log search priority

Search in this order (stop as soon as you have enough info):
1. `[FAIL]` — gives exact test names
2. `Failed!` — gives pass/fail/skip summary per assembly
3. `Error Message` — gives exception details
4. `Stack Trace` — gives call stack for root cause analysis

#### When the failed task is `CheckBuildLogsForErrors` (not a test task)

This is a **post-test Nuke target** that scans tracer log files for `[Error]` entries. Its log is typically small (<100 lines) and can be read directly. It will contain:
- The path to the offending log file (e.g., `.../InstrumentationTestsLogs/<TestName>/dotnet-tracer-managed-dotnet-<PID>.log`)
- The error message from the tracer runtime (e.g., `TimeoutException` in shutdown hooks)

The test name is embedded in the log file path. The test itself **passed** — the error is in the tracer's runtime behavior during or after the test.

**To investigate further**, find the *succeeded* `IntegrationTests` task in the same job (from the all-tasks listing above), download *that* log, and search for the test name:

```bash
# Search the integration test log (not the CheckBuildLogsForErrors log) for the specific test
Grep pattern="<TEST_NAME>" path=/tmp/ci_log.txt -C 3
```

This confirms whether the test passed/failed and shows its duration and context.

## Step 6: Check for GitLab Failures

GitLab failures appear in the statuses with context prefix `dd-gitlab/`. The `target_url` links to the GitLab build page, but **GitLab APIs require authentication** and are generally not accessible. Note the failing context name and description — that is usually sufficient.

## APIs That Do NOT Work Without Auth

- `dev.azure.com/.../test/runs` (AzDO test results API) — redirects to sign-in
- GitLab API endpoints — require auth tokens

## Common Failure Patterns

| Signal | Likely Cause |
|---|---|
| Exit code **255** | Infrastructure/Docker issue (OOM, agent crash, container failure). No test code ran or it was killed mid-run. |
| **60-minute timeout** (job canceled) | Job-level time limit exceeded; check if all tasks succeeded (= slow run, not a specific test failure) |
| Exit code **1** on `docker-compose build` tasks | Docker build failure; usually transient |
| Exit code **1** on `docker-compose run` with OCI/runc/cgroup errors | Docker runtime infrastructure failure. Container never started. No test code ran. |
| `CheckBuildLogsForErrors` failed but tests passed | Post-test log scan found errors in tracer log files (e.g., `TimeoutException` in shutdown hooks). The tests themselves passed; the *tracer runtime* logged an error during the test. |
| `Unable to determine port application is listening on` | Flaky test fixture startup — the sample ASP.NET app failed to bind to a port within the timeout window. Cascading failures in dependent tests are expected. |
| `Sequence contains no matching element` after a port timeout | Cascading failure — test depends on same fixture that failed to start. Not a separate root cause. |
| Same test failing across unrelated PRs | Pre-existing flaky test, not caused by the PR |
| Different tests failing on each commit | Infrastructure flakiness, not a code regression |
| Single test failing consistently after a PR | Likely a real regression introduced by the PR |

## Comparing Across Commits

To determine if a failure is flaky vs. a real regression, check the CI status of commits **before and after** the suspect:

```bash
gh api "repos/DataDog/dd-trace-dotnet/commits?sha=master&per_page=15" \
  --jq '.[] | .sha[:10] + " " + (.commit.message | split("\n")[0])'
```

Then check each commit's status:

```bash
gh api "repos/DataDog/dd-trace-dotnet/commits/<SHA>/status" --jq ".state"
```

If commits before the suspect also fail with the same jobs, the failure is pre-existing.

## Windows / Git Bash Pitfalls

- **`curl` is unreliable** in Git Bash for piping large responses. Use `python -c "import urllib.request; ..."` to download files instead.
- **`!=` in inline Python breaks** when embedded in bash commands on Windows. The backslash-escaping mangles it. Use `not in (x, y)` instead, or write a temp `.py` file for complex scripts.
- **`/tmp/` path** works in Git Bash and maps to a Windows temp location. Safe to use for temp log files.
- **Encoding:** AzDO logs may contain Unicode (e.g., zero-width spaces in Nuke output). Always use `decode('utf-8','replace')` and `encoding='utf-8'` when writing.

## Quick Full Workflow Example

```bash
# 1. Get merge commit
SHA=$(gh pr view 8170 --json mergeCommit --jq '.mergeCommit.oid')

# 2. Check status
gh api "repos/DataDog/dd-trace-dotnet/commits/$SHA/status" --jq ".state"

# 3. Find failures (paginated)
gh api --paginate "repos/DataDog/dd-trace-dotnet/commits/$SHA/statuses?per_page=100" \
  --jq '.[] | select(.state == "failure") | .context + ": " + .description + " | " + .target_url'

# 4. Extract build ID from target_url, then query AzDO timeline for failed jobs
BUILD_ID=196053
curl -s "https://dev.azure.com/datadoghq/dd-trace-dotnet/_apis/build/builds/$BUILD_ID/timeline?api-version=7.0" | python -c "
import json,sys
for r in json.load(sys.stdin).get('records',[]):
    if r.get('result') in ('failed','canceled'):
        print(f'{r.get(\"type\")}: {r.get(\"name\")} -> {r.get(\"result\")}')
        for i in r.get('issues',[])[:3]:
            print(f'  {i.get(\"message\",\"\")[:300]}')
"

# 5. Get log URLs for failed jobs, then download and search for test names
#    (use the "Finding log URLs" script from Step 5b to get log URLs)
#    (use the "Downloading logs" script from Step 5b to save to /tmp/ci_log.txt)
#    Then search:
#      Grep pattern="\[FAIL\]" path=/tmp/ci_log.txt -C 2
#      Grep pattern="Error Message" path=/tmp/ci_log.txt -A 5
```

## Build ID Shortcut

If you already have the AzDO build URL (e.g., from the user), extract the `buildId` parameter directly and skip steps 1-3. Go straight to step 4.
