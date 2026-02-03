# Error Stack Trace Analysis for dd-trace-dotnet

You are analyzing an error stack trace from the dd-trace-dotnet library. These errors originated from customer applications but are caused by dd-trace-dotnet. Your goal is to understand the error, determine if it provides enough information to identify the root cause, and recommend a fix ONLY if the error is actionable within dd-trace-dotnet.

## Input Processing
The user has provided an error stack trace. Parse and analyze it systematically.

## Analysis Workflow

## GitHub Link Generation

When referencing files in the dd-trace-dotnet repository, always provide clickable GitHub links to the master branch:

**Format**:
```
[filename:line](https://github.com/DataDog/dd-trace-dotnet/blob/master/path/to/file#Lline)
```

**Examples**:
- Single line: `[PerformanceCountersListener.cs:123](https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace/RuntimeMetrics/PerformanceCountersListener.cs#L123)`
- Line range: `[DataStreamsWriter.cs:45-60](https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Trace/DataStreamsMonitoring/DataStreamsWriter.cs#L45-L60)`

**Path construction**:
- Base URL: `https://github.com/DataDog/dd-trace-dotnet/blob/master/`
- Append the repository-relative path (strip workspace prefixes)
- Add `#L{lineNumber}` for single line or `#L{start}-L{end}` for ranges

### Phase 1: Parse & Classify Stack Frames

Extract all stack frames and classify each one:

**Classification Categories:**
- **dd-trace-dotnet**: Methods in `Datadog.Trace.*` namespaces
- **CLR Runtime**: Framework methods (System.*, Microsoft.*, etc.)
- **REDACTED**: Frames marked as REDACTED in the stack trace
- **External/Application**: Other identifiable frames

Create a classification table showing frame number, type, location, and brief description.

**Important**:
- REDACTED frames indicate customer/application code that we cannot see
- We have NO information about what happens in REDACTED frames
- Do NOT speculate about REDACTED frame behavior

### Phase 2: Locate dd-trace-dotnet Code

For each dd-trace-dotnet frame that includes file path, method name, or line number:

1. **Search for the method/class**:
   - Use Glob to find files matching the class name: `**/{ClassName}.cs`
   - If the namespace is known, search within the appropriate directory structure
   - For example, `Datadog.Trace.RuntimeMetrics.PerformanceCountersListener` → `tracer/src/Datadog.Trace/RuntimeMetrics/PerformanceCountersListener.cs`

2. **Read code with context**:
   - Find the method mentioned in the stack trace
   - Include enough context to understand what the code is doing
   - Show the relevant code path that leads to the error
   - If a line number is available, focus on that specific line

### Phase 3: Code Context Extraction

For each critical dd-trace-dotnet frame:
1. Show the function/method signature
2. Include surrounding code
3. **Highlight the error point** (if line number available)
4. Explain what this code does
5. Explain what likely went wrong based on the exception type and message

Format each code section as:
```
### Frame X: {method_name} ([{file}:{line}](GitHub link))

​```csharp
// {file}:{start_line}-{end_line}
{code with error point marked if available}
​```

**Analysis**: {Explanation of what this code does and what went wrong}
```

### Phase 4: Error Flow Reconstruction

Build a narrative explaining the execution flow leading to the error:

1. **Entry point**: Where did execution likely start? (e.g., background task, instrumentation callback)
2. **Key operations**: What was the code trying to do?
3. **Error point**: Where did the exception occur and why?
4. **Exception type**: What does this exception typically indicate?
5. **Context**: Are there REDACTED frames that might be relevant but unknowable?

Write this as a clear, step-by-step narrative focused on HOW the error occurred.

**Important considerations**:
- Log messages are constant templates - we don't have the actual runtime values
- REDACTED frames are unknown - acknowledge their presence but don't speculate
- Stack trace may be from an older version of the tracer

### Phase 5: Actionability Assessment

Determine if this error is actionable within dd-trace-dotnet. **Default to YES (actionable) unless there's clear evidence otherwise.**

**Actionable (YES) IF**:
- The error occurs entirely within dd-trace-dotnet code
- The root cause can be identified from the stack trace
- There's a code path in dd-trace-dotnet that can be fixed to prevent the error
- The error indicates a bug, race condition, or incorrect assumption in dd-trace-dotnet

**NOT Actionable (NO) IF**:
- The error originates from framework/CLR code that dd-trace-dotnet calls
- The error is a consequence of invalid application state (unknown to us)
- Critical frames are REDACTED and we cannot determine the actual cause
- The error is expected behavior under certain conditions
- The error is environmental (missing dependencies, permissions, etc.)
- **AND** you have explicit proof from PR descriptions/commits that this exact error was already fixed

**IMPORTANT**: Do NOT mark as "not actionable" just because you found a PR that touched the same area. You need explicit evidence that this specific error scenario was fixed.

### Phase 6: Version Analysis

Use Bash tool with git commands to check if this might be a known or fixed issue:

1. Search for related fixes: `git log --grep="keyword" --oneline -20` (use keywords from the error message or affected area)
2. Check recent commits to affected files: `git log --oneline -10 {file}`
3. Look for related PRs in commit messages (e.g., "(#1234)")
4. If found, construct PR links: `https://github.com/DataDog/dd-trace-dotnet/pull/{number}`

**CRITICAL - Do NOT assume a PR fixed this error unless**:
- The PR explicitly mentions fixing this EXACT exception type and error message
- The PR description or commit messages reference the specific bug being reported
- You can verify that the code changes would prevent this exact error scenario

**A PR that touches the same file or area is NOT sufficient evidence** - it might have fixed a different issue or even introduced this one. Default to treating the error as actionable unless you have clear proof it's fixed.

## Output Format

Generate a well-formatted markdown document with these sections:

```markdown
# Error Analysis Report
**Generated**: {ISO 8601 timestamp}

## Executive Summary
{2-3 sentence summary of what error occurred, where in the code, and whether it's actionable}

## Error Details

**Error Message**: `{error message template}`
**Exception Type**: `{exception type}`
**Source**: Customer application instrumented with dd-trace-dotnet

## Stack Trace Classification

| # | Type | Location | Description |
|---|------|----------|-------------|
| 0 | {type} | {method/location} | {brief description} |
| 1 | {type} | {method/location} | {brief description} |
| ... | ... | ... | ... |

**Note**: REDACTED frames indicate customer/application code. We have no visibility into these frames.

## Code Context

{For each critical dd-trace-dotnet frame, show code with analysis}

### Frame X: {method} ([{file}:{line}](GitHub link))

​```csharp
{code snippet}
​```

**Analysis**: {Explanation of what this code does}

## Error Flow Reconstruction

{Step-by-step narrative explaining how the error occurred}

**Exception Type**: {What this exception typically indicates}

**How it happened**: {Clear explanation of the sequence of events based on the stack trace and code analysis}

**Unknown Factors**: {Note any REDACTED frames or missing information that limits our understanding}

## Actionability Assessment

### Is This Error Actionable?
{YES or NO with clear justification}

{If NO, explain why:}
- {Reason 1}
- {Reason 2}

{If YES, explain what can be fixed in dd-trace-dotnet}

## Version Analysis

**Potentially Related Changes**:
{If found, list related PRs and commits}
- [#{PR number}](https://github.com/DataDog/dd-trace-dotnet/pull/{PR number}): {PR title} - {why relevant and whether it definitively fixes THIS error}

{If no related changes found}
- No recent changes found related to this error area

**IMPORTANT**: Just because a PR touched this code area does NOT mean it fixed this error. Only conclude an error is fixed if:
1. The PR explicitly mentions this exception type and scenario
2. The code changes clearly prevent this exact error
3. The PR description or commits reference this specific bug

Otherwise, treat the error as NEW and actionable.

## Recommended Action

### {If Actionable}

**Recommended Fix**:
{Describe the code change needed in dd-trace-dotnet to prevent this error}

**Implementation Details**:
- {File to modify}: [{file}](GitHub link)
- {What to change}: {specific change description}
- {Why this fixes it}: {explanation}

**Testing**:
- {How to test the fix}
- {What scenarios to cover}

### {If NOT Actionable}

**Recommended Action**: Mark this error as **Ignored** in Error Tracking

**Code Change Analysis**: Before recommending `ErrorSkipTelemetry`, analyze the error handling flow:

1. **Identify where the error actually originates** - Look at the full call stack
2. **Check if inner methods already handle expected errors** - If they do, outer catch blocks only catch unexpected exceptions (bugs)
3. **Determine the appropriate log level**:
   - Intermediate retry attempts → `Log.Debug` (not Error at all)
   - Final failure after retries → `Log.ErrorSkipTelemetry` with helpful context
   - Outer catch blocks that shouldn't normally be hit → Keep `Log.Error`

**If changing to ErrorSkipTelemetry is appropriate:**
- File to Update: [{file}](GitHub link)
- Use generic log methods: `Log.ErrorSkipTelemetry<T>(...)` not `.ToString()`
- Include helpful context (endpoint, troubleshooting URL)

**If intermediate retry logging should be Debug:**
- Change from `Log.Error` to `Log.Debug<int>` for intermediate attempts
- Only log Error/ErrorSkipTelemetry for final failures

**Reason**: {Explain why this error is not actionable and the recommended approach}

## Additional Context

{Any additional useful context about the error, affected features, or environmental factors}

---
*Analysis generated by Claude Code /analyze-error command*
*This analysis determines if the error is actionable within dd-trace-dotnet and provides recommendations accordingly.*
```

## Output File Management

1. **Create output directory**:
   - On Windows: Use `powershell.exe -NoProfile -Command 'New-Item -ItemType Directory -Force -Path (Join-Path $env:USERPROFILE ".claude\analysis") | Select-Object -ExpandProperty FullName'`
   - On Linux/Mac: Use `mkdir -p ~/.claude/analysis && echo ~/.claude/analysis`
   - **IMPORTANT**: On Windows, you MUST use single quotes around the PowerShell command to prevent bash from interpreting `$env:USERPROFILE`
2. **Generate filename**: Use format `error-analysis-{YYYYMMDD-HHMMSS}.md` (e.g., `error-analysis-20250316-143022.md`)
3. **Save file**: Write the markdown analysis to the file
4. **Return path**: Tell the user the full path where the analysis was saved

## Important Guidelines

### DO NOT Recommend
- **Try/Catch blocks**: Catching exceptions is virtually NEVER the correct solution
- **Application code changes**: We don't control customer applications
- **Application environment variable changes**: We can't modify customer environments
- **Suppressing errors indiscriminately**: Only use ErrorSkipTelemetry when genuinely not actionable
- **ToString() in log arguments**: Never use `.ToString()` on numeric types in log calls - use generic log methods instead

### Code Quality Requirements
When recommending logging changes, follow these patterns:

**Never use ToString() for log arguments:**
```csharp
// BAD - allocates a string unnecessarily
Log.Error(ex, "Error (attempt {Attempt})", (attempt + 1).ToString());

// GOOD - uses generic method, no allocation
Log.Debug<int>(ex, "Error (attempt {Attempt})", attempt + 1);
```

**Understand code flow before applying ErrorSkipTelemetry:**
- If inner methods already catch and handle expected errors (network issues, timeouts), outer catch blocks would only catch UNEXPECTED exceptions
- Outer catch blocks should typically remain `Log.Error` since they indicate bugs
- Only apply `ErrorSkipTelemetry` at the point where expected errors actually occur

**Use appropriate log levels for retry operations:**
- `Log.Debug`: Intermediate retry attempts (transient errors are expected)
- `Log.Error` or `Log.ErrorSkipTelemetry`: Final failure after all retries
- `Log.Error`: Non-retryable errors like HTTP 400 (indicates a bug)

### DO Recommend (When Actionable)
- **Null checks**: If dereferencing could fail
- **Validation**: If invalid state leads to errors
- **Synchronization**: If race conditions are evident
- **Defensive coding**: If assumptions might be violated
- **Better error handling**: If we can gracefully handle edge cases
- **Bug fixes**: If there's a clear logic error

### DO Recommend (When NOT Actionable)
- **Mark as Ignored** in Error Tracking
- **Change Log.Error to Log.ErrorSkipTelemetry** ONLY at the specific location where expected errors occur
- **Change intermediate retry logs to Debug level** if transient errors are expected
- Clear explanation of why it's not actionable
- **Understand the full error handling flow** before recommending changes to any catch block

### Analysis Constraints
- **REDACTED frames are black boxes**: Don't speculate about their behavior
- **Log messages are templates**: Don't assume specific runtime values
- **Stack traces may be old**: Note if the code has changed since
- **Link to master branch**: All GitHub links should point to current code
- **Focus on dd-trace-dotnet**: Only recommend changes within our codebase

### Quality Standards
- **Be concise but thorough**: Provide enough detail without unnecessary verbosity
- **Be definitive on actionability**: Clearly state YES or NO with justification
- **Provide specific fixes**: If actionable, describe exactly what to change
- **Acknowledge limitations**: Explicitly state when information is missing or uncertain
- **Link everything**: Every file reference should have a GitHub link

## Now Analyze

Parse the error stack trace provided by the user and follow the workflow above to generate a comprehensive error analysis with actionability assessment and recommendations.
