# Stack Trace Crash Analysis for dd-trace-dotnet

You are analyzing a crash stack trace for the dd-trace-dotnet repository. Perform a comprehensive investigation to help engineers understand and triage the crash. Focus on de-mystifying the crashing thread and explaining how the crash occurred.

## Input Processing
The user has provided a crash stack trace. Parse and analyze it systematically.

## Analysis Workflow

## GitHub Link Generation

When referencing files in the dd-trace-dotnet repository, always provide clickable GitHub links in addition to local paths:

**Format**:
```
[filename:line](https://github.com/DataDog/dd-trace-dotnet/blob/master/path/to/file#Lline)
```

**Examples**:
- Single line: `[cor_profiler.cpp:1430](https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Tracer.Native/cor_profiler.cpp#L1430)`
- Line range: `[rejit_handler.cpp:263-296](https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Tracer.Native/rejit_handler.cpp#L263-L296)`

**When to use**:
- All file:line references in Executive Summary
- Stack Trace Classification table "Location" column (when file paths are available)
- All code context section headings
- Related Code section

**Path construction**:
- Base URL: `https://github.com/DataDog/dd-trace-dotnet/blob/master/`
- Append the repository-relative path (strip `C:\Users\...\dd-trace-dotnet\` or similar prefixes)
- Add `#L{lineNumber}` for single line or `#L{start}-L{end}` for ranges
- Example: Local path `C:\Users\...\dd-trace-dotnet\tracer\src\Datadog.Tracer.Native\cor_profiler.cpp:1430` becomes `[cor_profiler.cpp:1430](https://github.com/DataDog/dd-trace-dotnet/blob/master/tracer/src/Datadog.Tracer.Native/cor_profiler.cpp#L1430)`

### Phase 1: Parse & Classify Stack Frames

Extract all stack frames and classify each one:

**Classification Categories:**
- **CLR Runtime**: Functions like `ReJitManager::`, `ProfToEEInterfaceImpl::`, `ICorProfiler*`, CLR DLL references (clr.dll, coreclr.dll, mscorwks.dll)
- **dd-trace-dotnet Native**: Namespace patterns (`trace::`, `debugger::`, `fault_tolerant::`, `iast::`), or paths containing `Datadog.Tracer.Native`
- **dd-trace-dotnet Managed**: References to `Datadog.Trace.dll!`
- **External/Application**: Everything else (customer code, third-party libraries, framework code)

Create a classification table showing frame number, type, location, and brief description.

### Phase 2: Locate dd-trace-dotnet Code

For each dd-trace-dotnet frame that includes file path and line number:

1. **Extract and normalize path**: Remove build-specific prefixes
   - Strip: `c:\mnt\`, `D:\a\_work\1\s\`, `/home/runner/work/dd-trace-dotnet/`, etc.
   - Result should be relative to repo root: `tracer/src/Datadog.Tracer.Native/{filename}`

2. **Find file in repository**:
   - First try direct path match
   - If not found, use Glob tool with pattern: `**/Datadog.Tracer.Native/**/{filename}`
   - If still not found, try just the filename: `**/{filename}`

3. **Read code with context**:
   - Find the function containing the crash line
   - Include 10-15 lines before the crash line
   - Mark or highlight the actual crash line
   - Include 5-10 lines after
   - Show enough context to understand what the code is doing

### Phase 3: Code Context Extraction

For each critical dd-trace-dotnet frame:
1. Show the function signature
2. Include surrounding code (described in Phase 2)
3. **Clearly mark the crash line** with a comment like `// >>> CRASH POINT <<<`
4. Explain what this code does
5. Explain why it crashed based on the evidence

Format each code section as:
```
### Frame X: {function_name} ([{file}:{line}](GitHub link))

​```cpp
// {file}:{start_line}-{end_line}
{code with crash line marked}
​```

**Analysis**: {Explanation of what this code does and why it failed}
```

### Phase 4: Reconstruct Crash Flow

Build a narrative explaining the execution flow leading to the crash. This is the primary goal of the analysis - to help engineers understand what happened:

1. **Entry point**: Where did execution start? (e.g., profiler callback, background thread loop)
2. **Key operations**: What was the code trying to do?
3. **Critical transitions**: Where did control flow between components?
4. **Failure point**: Where and why did it crash?
5. **Crash type**: Describe what type of crash this is (e.g., null pointer dereference, access violation, invalid module reference, race condition, etc.)

Write this as a clear, step-by-step narrative that someone unfamiliar with the code can follow. Focus on explaining HOW the crash happened based on the evidence in the stack trace and code, without prescribing a specific fix.

### Phase 5: Identify Related Code

Use Bash tool with git commands to find relevant context, focusing on commits associated with PRs:

1. Check recent commits to the affected files: `git log --oneline -10 {file}`
2. Search for related changes: `git log --grep="crash" --grep="fix" --oneline -20` (use keywords relevant to the crash area)
3. For each relevant commit, check if it's associated with a PR:
   - Look for PR numbers in commit messages (e.g., "(#1234)" or "PR #1234")
   - If found, construct PR link: `https://github.com/DataDog/dd-trace-dotnet/pull/{number}`
4. Prioritize commits with PR associations - these have more context
5. Look for similar code patterns in other files that might provide context

**Focus on commits with PR links** - PRs provide valuable context including descriptions, discussions, and rationale that individual commits lack.

## Output Format

Generate a well-formatted markdown document with these sections:

```markdown
# Crash Analysis Report
**Generated**: {ISO 8601 timestamp}

## Executive Summary
{2-3 sentence summary of what crashed and where in the code. Focus on demystifying the crash location and describing what the crashing thread was doing.}

## Stack Trace Classification

### Crashed Thread: #{thread_number}

| # | Type | Location | Description |
|---|------|----------|-------------|
| 0 | {type} | {function/location} | {brief description} |
| 1 | {type} | {function/location} | {brief description} |
| ... | ... | ... | ... |

{If multiple threads provided, note other interesting threads but focus on crashed thread}

## Code Context

{For each critical dd-trace-dotnet frame, show code with analysis}

### Frame X: {function} ([{file}:{line}](GitHub link))

​```cpp
{code snippet with crash line marked}
​```

**Analysis**: {Explanation of what this code does}

## Crash Flow Reconstruction

{Step-by-step narrative explaining the execution flow from start to crash point}

**Crash Type**: {Description of what type of crash this is - e.g., null pointer dereference, access violation, invalid module reference, race condition}

**How it happened**: {Clear explanation of the sequence of events that led to the crash based on the stack trace and code analysis}

## Related Code

**Relevant PRs and commits**:
- [#{PR number}](https://github.com/DataDog/dd-trace-dotnet/pull/{PR number}): {PR title/description} - {why relevant}
- [#{PR number}](https://github.com/DataDog/dd-trace-dotnet/pull/{PR number}): {PR title/description} - {why relevant}

{Only include commits without PR associations if they are particularly relevant}

**Related code locations**:
- [{file}:{line}](GitHub link) - {description}
- [{file}:{line}](GitHub link) - {description}

## Additional Context

{Any additional useful context about the application environment, runtime, features in use, or relevant background}

---
*Analysis generated by Claude Code /analyze-crash command*
*This analysis is intended to help understand and triage the crash. Engineers should review this analysis to determine if a code fix is needed.*
```

## Output File Management

1. **Create output directory**:
   - On Windows: Use `powershell.exe -NoProfile -Command 'New-Item -ItemType Directory -Force -Path (Join-Path $env:USERPROFILE ".claude\analysis") | Select-Object -ExpandProperty FullName'`
   - On Linux/Mac: Use `mkdir -p ~/.claude/analysis && echo ~/.claude/analysis`
   - **IMPORTANT**: On Windows, you MUST use single quotes around the PowerShell command to prevent bash from interpreting `$env:USERPROFILE`
2. **Generate filename**: Use format `crash-analysis-{YYYYMMDD-HHMMSS}.md` (e.g., `crash-analysis-20250316-143022.md`)
3. **Save file**: Write the markdown analysis to the file
4. **Return path**: Tell the user the full path where the analysis was saved

## Important Guidelines

- **Focus on triage and understanding**: The goal is to help engineers understand HOW the crash happened, not to prescribe specific fixes
- **Describe crash types**: It's okay to identify what type of crash this is (e.g., null pointer, race condition, invalid reference), but don't match against fixed "patterns"
- **End at explanation**: Phase 4 (Crash Flow Reconstruction) should provide a clear explanation of how the crash occurred. Engineers will then decide on fixes
- **No fix suggestions**: Do not suggest code changes, patches, or specific implementation fixes. Focus on analysis only
- **Handle path variations gracefully**: Stack traces from different build environments will have different path prefixes
- **Continue with missing information**: If a file can't be located, note this but continue the analysis with available information
- **Focus on the crashed thread**: If multiple threads are provided, focus primarily on the crashed thread but mention other relevant threads
- **Be concise but thorough**: Provide enough detail to understand the issue without unnecessary verbosity
- **Always include GitHub links**: Every file:line reference should have a clickable GitHub link to master branch
- **Prefer PR links over commits**: When git log finds relevant commits, prioritize those associated with PRs. Extract PR numbers from commit messages (e.g., "(#1234)") and link to the PR: `https://github.com/DataDog/dd-trace-dotnet/pull/{number}`. Only include standalone commit links if particularly relevant and not part of a PR
- **Mark uncertainties**: If something is unclear or speculative, explicitly state this

## Now Analyze

Parse the stack trace provided by the user and follow the workflow above to generate a comprehensive crash analysis.
