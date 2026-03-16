# CLI Reference & Windows Pitfalls

Reference for Azure DevOps API calls and Windows-specific CLI issues. Load this when running Azure DevOps CLI commands directly (bypassing the PowerShell script).

## Azure DevOps REST API

**Base URL**: `https://dev.azure.com/datadoghq`
**Project**: `dd-trace-dotnet`
**Project ID**: `a51c4863-3eb4-4c5d-878a-58b41a049e4e`

**Using az devops invoke**:
```bash
az devops invoke \
  --area <area> \
  --resource <resource> \
  --route-parameters project=dd-trace-dotnet [key=value ...] \
  --org https://dev.azure.com/datadoghq \
  --api-version 6.0 \
  [--query-parameters key=value ...]
```

**Common endpoints**:

**Get Build Details**:
```bash
az devops invoke --area build --resource builds \
  --route-parameters project=dd-trace-dotnet buildId=$BUILD_ID
```

**Get Build Timeline**:
```bash
az devops invoke --area build --resource timeline \
  --route-parameters project=dd-trace-dotnet buildId=$BUILD_ID
```

**Get Build Logs** (may fail with HTTP 500):
```bash
az devops invoke --area build --resource logs \
  --route-parameters project=dd-trace-dotnet buildId=$BUILD_ID logId=$LOG_ID
```

### GitHub CLI

**Get PR Checks**:
```bash
gh pr checks <PR_NUMBER> --repo DataDog/dd-trace-dotnet \
  --json name,state,link,completedAt
```

Available fields: `bucket`, `completedAt`, `description`, `event`, `link`, `name`, `startedAt`, `state`, `workflow`

## Windows CLI Pitfalls (Lessons Learned)

### 0. Use Temporary Directory for Output Files

**Problem**: Using `/tmp` or hardcoded paths can cause issues on Windows

**Solution**: Use `$TEMP` (PowerShell/Windows) or a user-specified output directory

```bash
# Bad - Don't hardcode /tmp
az devops invoke ... > /tmp/timeline.json

# Good - Use $TEMP or a specific output directory
az devops invoke ... > "$TEMP/timeline.json"
```

### 1. Complex jq Filters Fail on Windows

**Problem**: Complex jq expressions with `!=` or nested filters cause parse errors

**Solution**: Save JSON to file first, then query with simpler filters

```bash
# Bad - Complex filter inline (fails on Windows)
az devops invoke ... | jq '.records[] | select(.issues != null and .issues != [])'

# Good - Save first, then query
az devops invoke ... --output json > "$TEMP/timeline.json"
cat "$TEMP/timeline.json" | jq '.records[] | select(.issues)'
```

### 2. API 500 Errors for Logs - USE TIMELINE URLs INSTEAD

**Problem**: `az devops invoke --resource logs` frequently returns HTTP 500

**Solution**: Use the log URLs directly from the timeline data instead of the logs API

```bash
# Bad - az devops invoke --resource logs returns HTTP 500
az devops invoke --area build --resource logs \
  --route-parameters project=dd-trace-dotnet buildId=$BUILD_ID logId=$LOG_ID

# Good - Extract log URL from timeline and use curl
LOG_URL=$(cat "$SCRATCHPAD/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .log.url' | head -1)
curl -s "$LOG_URL" > "$SCRATCHPAD/build-$BUILD_ID-log.txt"

# Good - Or use WebFetch tool
LOG_URL=$(cat "$SCRATCHPAD/build-$BUILD_ID-timeline.json" | \
  jq -r '.records[] | select(.result == "failed" and .type == "Task") | .log.url' | head -1)
# Then use WebFetch with $LOG_URL
```

The timeline `.log.url` field provides direct URLs that work reliably:
```
https://dev.azure.com/datadoghq/<project-id>/_apis/build/builds/<buildId>/logs/<logId>
```

### 3. Piping to head Causes Errors

**Problem**: `jq ... | head -50` can cause "Invalid argument" errors on Windows

**Solution**: Avoid piping jq to head entirely; let jq output naturally or use first()

```bash
# Bad - Causes "Invalid argument" on Windows
jq '.records[]' | head -50

# Good - Let jq output naturally (it's filtered already)
jq '.records[] | select(.result == "failed")'

# Good - Use first() if you only need one result
jq '.records[] | select(.result == "failed") | first'

# Good - Use limit if you need specific count
jq '[.records[] | select(.result == "failed")] | .[0:10]'
```

### 4. Query Parameter Escaping

**Problem**: Special characters in query parameters need escaping on Windows

**Solution**: Use single quotes for parameter values containing special chars

```bash
# Bad - $top interpreted as shell variable
--query-parameters branchName=refs/heads/master $top=5

# Good - Quote the parameter
--query-parameters branchName=refs/heads/master '$top=10'
```

### 5. `--route-parameters` Must Be Separate Arguments

**Problem**: Passing route parameters as a single space-separated string causes cryptic authentication errors (`TF400813: The user '...' is not authorized`)

**Solution**: Each key=value pair must be a separate argument

```bash
# Bad - Single string, causes auth errors
az devops invoke --route-parameters "project=dd-trace-dotnet buildId=12345"

# Good - Separate arguments
az devops invoke --route-parameters project=dd-trace-dotnet buildId=12345
```

In PowerShell, when building argument arrays, split them into individual elements:
```powershell
# Bad - Single array element
$azArgs += "project=dd-trace-dotnet buildId=$BuildId"

# Good - Separate array elements
$azArgs += "project=dd-trace-dotnet"
$azArgs += "buildId=$BuildId"
```

### 6. jq Pitfalls with Nullable Fields

**Problem**: Using `startswith()`, `contains()`, or `test()` on nullable fields causes errors when the field is null

**Solution**: Guard with `!= null` or use `// ""` default value

```bash
# Bad - Fails if .name is null
jq '.records[] | select(.name | startswith("Test"))'

# Good - Guard with null check
jq '.records[] | select(.name != null and (.name | startswith("Test")))'

# Good - Use default value
jq '.records[] | select((.name // "") | startswith("Test"))'
```
