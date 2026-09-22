---
name: analyze-error
description: Error Stack Trace Analysis for dd-trace-dotnet
argument-hint: <paste-error-stack-trace> <ctrl+enter any other context>
disable-model-invocation: true
---

# Exception Analysis for dd-trace-dotnet

Analyze a redacted exception that was encountered within an application running dd-trace-dotnet.
The exception was caught by dd-trace-dotnet, redacted, and then sent to Datadog's log intake.
The goal is to: understand the error, determine if it is caused by dd-trace-dotnet, determine how to reproduce, determine how to prevent it (not catch it).

## Workflow

User provided the basic error message along with the redacted stack trace.
First line will be the Error Message - this is a constant message template that dd-trace-dotnet will log and send to Datadog
Following lines will be the stack trace.

The user may have entered additional context info afterwards such as descriptions, PR links, versions  the error was seen on.

### Considerations

If it appears that duck typing is involved, prior to doing deep analysis go over the source code within dd-trace-dotnet\tracer\src\Datadog.Trace\DuckTyping\ as it is vital to have a clear understanding of this for proper error reconstruction.

#### External Libraries

When necessary refer to the official source of known third party libraries that are within the stack to provide better error reconstruction.

### Constraints

- **REDACTED** frames are not known, but it is permissible to assume what they likely are.
- Never recommend using a try/catch.
- Log messages are constant message templates.
- Stack traces may be outdated as in the current version of the code has changed and the error may have been resolved.
- If the version isn't the latest version that does not imply it is resolved on latest
- Only recommend changes within dd-trace-dotnet.
- All exceptions / errors given have been gracefully caught.

## Output Format

Use Markdown
Do not use tables

### 1. Overview
Stack Trace, Component / CODEOWNER team, Actionability

### 2. Summary
2 to 3 sentences that can be used to explain what is happening.

### 3. Root Cause

Detailed analysis outlining the code flow that leads to the error. Format as follows:
- Bold header with a short descriptive label
- Reference simple type/method names, limit usages of URLs in this section
- Put actual code references in code blocks

### 4. Suggested Fix

Before / after code comparison, format this as a git diff

### 5. Reproduction Steps

Provide an overview of how to reproduce this error.
