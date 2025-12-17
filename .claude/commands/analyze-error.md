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

Determine if this error is actionable within dd-trace-dotnet. Consider:

**Actionable IF**:
- The error occurs entirely within dd-trace-dotnet code
- The root cause can be identified from the stack trace
- There's a code path in dd-trace-dotnet that can be fixed to prevent the error
- The error indicates a bug, race condition, or incorrect assumption in dd-trace-dotnet

**NOT Actionable IF**:
- The error originates from framework/CLR code that dd-trace-dotnet calls
- The error is a consequence of invalid application state (unknown to us)
- Critical frames are REDACTED and we cannot determine the actual cause
- The error is expected behavior under certain conditions
- The error is environmental (missing dependencies, permissions, etc.)

### Phase 6: Version Analysis

Use Bash tool with git commands to check if this might be a known or fixed issue:

1. Search for related fixes: `git log --grep="keyword" --oneline -20` (use keywords from the error message or affected area)
2. Check recent commits to affected files: `git log --oneline -10 {file}`
3. Look for related PRs in commit messages (e.g., "(#1234)")
4. If found, construct PR links: `https://github.com/DataDog/dd-trace-dotnet/pull/{number}`

Note: The stack trace may be from an older version, so a fix may already exist in master.

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
- [#{PR number}](https://github.com/DataDog/dd-trace-dotnet/pull/{PR number}): {PR title} - {why relevant}

{If no related changes found}
- No recent changes found related to this error area

**Note**: The stack trace may be from an older version. Check if the issue persists in the latest version.

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

**Code Change**: Update the log statement to use `Log.ErrorSkipTelemetry` instead of `Log.Error`

**Reason**: {Explain why this error is not actionable and should not be tracked}

**File to Update**: [{file}](GitHub link to the file containing the Log.Error call)

## Additional Context

{Any additional useful context about the error, affected features, or environmental factors}

---
*Analysis generated by Claude Code /analyze-error command*
*This analysis determines if the error is actionable within dd-trace-dotnet and provides recommendations accordingly.*
```

## Output File Management

1. **Create output directory**: Use Bash to create `~/.claude/analysis/` if it doesn't exist
2. **Generate filename**: Use format `error-analysis-{YYYYMMDD-HHMMSS}.md` (e.g., `error-analysis-20250316-143022.md`)
3. **Save file**: Write the markdown analysis to the file
4. **Return path**: Tell the user the full path where the analysis was saved

## Important Guidelines

### DO NOT Recommend
- **Try/Catch blocks**: Catching exceptions is virtually NEVER the correct solution
- **Application code changes**: We don't control customer applications
- **Application environment variable changes**: We can't modify customer environments
- **Suppressing errors indiscriminately**: Only use ErrorSkipTelemetry when genuinely not actionable

### DO Recommend (When Actionable)
- **Null checks**: If dereferencing could fail
- **Validation**: If invalid state leads to errors
- **Synchronization**: If race conditions are evident
- **Defensive coding**: If assumptions might be violated
- **Better error handling**: If we can gracefully handle edge cases
- **Bug fixes**: If there's a clear logic error

### DO Recommend (When NOT Actionable)
- **Mark as Ignored** in Error Tracking
- **Change Log.Error to Log.ErrorSkipTelemetry** in the specific file
- Clear explanation of why it's not actionable

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
