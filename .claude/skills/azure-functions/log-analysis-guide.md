# Azure Functions Log Analysis Guide

This guide provides grep patterns and techniques for manual log investigation. Start with `Get-AzureFunctionLogs.ps1 -All` for automated analysis (version check, span counts, parenting validation). Use these patterns when you need to dig deeper than the automated analysis provides.

## Log File Behavior

### Append-Only Files
- Log files are **never cleared** on deployment
- New entries are appended to the end
- Old entries from previous versions remain at the beginning
- A single file may contain entries spanning days or weeks

### Process Lifecycle
- Worker processes restart on deployment
- They may **reuse the same PID** (Linux containers)
- Multiple process lifetimes appear in the same file
- Always verify which initialization is current

## Log File Names

Actual log file names follow this pattern (run from the `LogFiles/datadog/` directory):
- **Worker**: `dotnet-tracer-managed-dotnet-<pid>.log`
- **Host**: `dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-<pid>.log`

Multi-step script examples below use short aliases (`worker.log`, `host.log`) for readability after `cd`ing into the log directory. Single-command examples use globs directly.

## Essential Grep Patterns

### Finding Executions by Timestamp
```bash
# Specific minute
grep "<YYYY-MM-DD HH:MM>:" dotnet-tracer-managed-dotnet-*.log

# Specific seconds
grep "<YYYY-MM-DD HH:MM:3[89]>" dotnet-tracer-managed-dotnet-*.log

# Entire hour range
grep "<YYYY-MM-DD HH:[0-9][0-9]>:" dotnet-tracer-managed-dotnet-*.log

# Last 5 minutes (adjust regex for your time range)
grep "<YYYY-MM-DD HH:5[3-7]>:" dotnet-tracer-managed-dotnet-*.log
```

### Finding Initializations
```bash
# All worker initializations (shows restart history)
grep "Assembly metadata" dotnet-tracer-managed-dotnet-*.log

# Most recent initialization (current process)
grep "Assembly metadata" dotnet-tracer-managed-dotnet-*.log | tail -1

# Get initialization timestamp for filtering
grep "Assembly metadata" dotnet-tracer-managed-dotnet-*.log | tail -1 | awk '{print $1, $2}'
```

### Finding Span Information
```bash
# All spans created in timeframe
grep "<YYYY-MM-DD HH:MM>:" dotnet-tracer-managed-dotnet-*.log | grep "Span started"

# Specific trace ID
grep "<trace-id>" dotnet-tracer-managed-dotnet-*.log

# Root spans only (orphaned traces)
grep "p_id: null" dotnet-tracer-managed-dotnet-*.log | grep "Span started"

# Span closed events (see final tags)
grep "<YYYY-MM-DD HH:MM>:" dotnet-tracer-managed-dotnet-*.log | grep "Span closed"
```

### Finding Integration Activity
```bash
# Azure Functions specific
grep "<YYYY-MM-DD HH:MM>:" dotnet-tracer-managed-dotnet-*.log | grep -i "azure.functions\|FunctionExecutionMiddleware"

# Host process integrations
grep "<YYYY-MM-DD HH:MM>:" dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-*.log | grep -i "ToRpcHttp\|FunctionInvocationMiddleware"

# AsyncLocal context flow
grep "<YYYY-MM-DD HH:MM>:" dotnet-tracer-managed-dotnet-*.log | grep -i "asynclocal\|active.*scope"
```

### Context Lines for Better Understanding
```bash
# Show 2 lines before and after
grep -B 2 -A 2 "Span started" dotnet-tracer-managed-dotnet-*.log

# Show 5 lines after span creation (see tags)
grep -A 5 "Span started" dotnet-tracer-managed-dotnet-*.log

# Show 10 lines around error
grep -B 5 -A 5 "ERROR\|Exception" dotnet-tracer-managed-dotnet-*.log
```

## Parsing Span Relationships

### Extract Span Fields
```bash
# Extract trace_id | span_id | parent_id
grep "Span started" dotnet-tracer-managed-dotnet-*.log \
  | sed -E 's/.*\[s_id: ([^,]+), p_id: ([^,]+), t_id: ([^\]]+)\].*/\3 | \1 | \2/' \
  | sort
```

### Build Span Tree
```bash
# Find all spans in a trace
grep "<trace-id>" dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-*.log dotnet-tracer-managed-dotnet-*.log \
  | grep "Span started" \
  | awk -F'[\\[\\]]' '{print $2}'
```

### Identify Span Type
```bash
# Extract operation names
grep "Span started" dotnet-tracer-managed-dotnet-*.log \
  | sed -E 's/.*OperationName: "([^"]+)".*/\1/'

# Find specific operation
grep "Span started" dotnet-tracer-managed-dotnet-*.log | grep "azure_functions.invoke"
```

## Common Investigation Patterns

> These multi-step patterns use short aliases (`worker_log`, `host_log`) for readability. Set them first.

### Pattern 1: Verify Version After Deployment
```bash
worker_log="dotnet-tracer-managed-dotnet-*.log"

# 1. Find most recent worker initialization
INIT_TIME=$(grep "Assembly metadata" $worker_log | tail -1 | awk '{print $1, $2}')
echo "Most recent initialization: $INIT_TIME"

# 2. Check version in that initialization
grep "$INIT_TIME" $worker_log | grep "TracerVersion"

# 3. Verify version in subsequent logs
grep "<YYYY-MM-DD HH:MM>:" $worker_log | grep "TracerVersion" | head -1
```

### Pattern 2: Trace Host→Worker Flow
```bash
worker_log="dotnet-tracer-managed-dotnet-*.log"
host_log="dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-*.log"

# 1. Find host execution time
grep "Executing 'Functions" $host_log | tail -1

# 2. Get host trace ID
HOST_TIME="<YYYY-MM-DD HH:MM:SS>"
TRACE_ID=$(grep "$HOST_TIME" $host_log | grep "Span started" | grep -o 't_id: [^]]*' | cut -d' ' -f2)
echo "Host trace ID: $TRACE_ID"

# 3. Check if worker has spans in same trace
grep "$TRACE_ID" $worker_log | wc -l
echo "Worker spans in this trace: $(grep "$TRACE_ID" $worker_log | wc -l)"

# 4. Show all spans in chronological order
grep "$TRACE_ID" $host_log $worker_log | grep "Span started" | sort
```

### Pattern 3: Find Orphaned Traces
```bash
worker_log="dotnet-tracer-managed-dotnet-*.log"
host_log="dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-*.log"

# Find all root spans (should only be in host)
grep "<YYYY-MM-DD HH:MM>:" $host_log $worker_log \
  | grep "Span started" \
  | grep "p_id: null"

# Count root spans by file
echo "Host root spans:"
grep "<YYYY-MM-DD HH:MM>:" $host_log | grep "Span started" | grep "p_id: null" | wc -l
echo "Worker root spans (should be 0):"
grep "<YYYY-MM-DD HH:MM>:" $worker_log | grep "Span started" | grep "p_id: null" | wc -l
```

### Pattern 4: Compare Multiple Executions
```bash
worker_log="dotnet-tracer-managed-dotnet-*.log"

# Save logs for each execution (substitute your actual timestamps)
grep "<YYYY-MM-DD HH:MM>:" $worker_log > exec1.log
grep "<YYYY-MM-DD HH:MM>:" $worker_log > exec2.log

# Compare span counts
echo "Execution 1 spans: $(grep "Span started" exec1.log | wc -l)"
echo "Execution 2 spans: $(grep "Span started" exec2.log | wc -l)"

# Compare trace IDs
echo "Execution 1 trace IDs:"
grep "Span started" exec1.log | grep -o 't_id: [^]]*' | cut -d' ' -f2 | sort -u
echo "Execution 2 trace IDs:"
grep "Span started" exec2.log | grep -o 't_id: [^]]*' | cut -d' ' -f2 | sort -u
```

## Span Relationship Examples

### Healthy Trace Structure
```
Host process (PID 27):
[s_id: abc123, p_id: null, t_id: 68e948...]       ← Root span
[s_id: def456, p_id: abc123, t_id: 68e948...]     ← Child of root
[s_id: ghi789, p_id: def456, t_id: 68e948...]     ← HTTP call to worker

Worker process (PID 56):
[s_id: jkl012, p_id: ghi789, t_id: 68e948...]     ← Receives from host
[s_id: mno345, p_id: jkl012, t_id: 68e948...]     ← Azure function execution
```

**All spans share the same trace ID (68e948...)**

### Broken Trace Structure (Separate Traces)
```
Host process (PID 27):
[s_id: abc123, p_id: null, t_id: 68e948...]       ← Host trace
[s_id: def456, p_id: abc123, t_id: 68e948...]
[s_id: ghi789, p_id: def456, t_id: 68e948...]

Worker process (PID 56):
[s_id: xyz999, p_id: null, t_id: 68e012...]       ← SEPARATE worker trace! ❌
[s_id: uvw888, p_id: xyz999, t_id: 68e012...]
```

**Worker has different trace ID (68e012...) and root span**

## Advanced Analysis Scripts

### Complete Trace Visualization
```bash
#!/bin/bash
# trace-viz.sh - Visualize trace structure

TIMESTAMP="<YYYY-MM-DD HH:MM:S[89]>"  # adjust regex to match your seconds
worker_log="dotnet-tracer-managed-dotnet-*.log"
host_log="dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-*.log"

echo "=== Host Spans ==="
grep "$TIMESTAMP" $host_log \
  | grep "Span started" \
  | sed -E 's/.*\[s_id: ([^,]+), p_id: ([^,]+), t_id: ([^\]]+)\].*OperationName: "([^"]+)".*/\4: s=\1 p=\2 t=\3/'

echo ""
echo "=== Worker Spans ==="
grep "$TIMESTAMP" $worker_log \
  | grep "Span started" \
  | sed -E 's/.*\[s_id: ([^,]+), p_id: ([^,]+), t_id: ([^\]]+)\].*OperationName: "([^"]+)".*/\4: s=\1 p=\2 t=\3/'
```

### Find Timing Information
```bash
#!/bin/bash
# span-timing.sh - Extract span durations

TRACE_ID="<trace-id>"
worker_log="dotnet-tracer-managed-dotnet-*.log"
host_log="dotnet-tracer-managed-Microsoft.Azure.WebJobs.Script.WebHost-*.log"

# Find spans with start/end times
grep "$TRACE_ID" $host_log $worker_log \
  | grep "Span \(started\|closed\)" \
  | awk '{
    if ($0 ~ /Span started/) {
      match($0, /s_id: ([^,]+)/, arr);
      start[arr[1]] = $1 " " $2;
    }
    if ($0 ~ /Span closed/) {
      match($0, /s_id: ([^,]+)/, arr);
      end[arr[1]] = $1 " " $2;
      print arr[1], "started:", start[arr[1]], "ended:", end[arr[1]];
    }
  }'
```

## Quick Reference Commands

### One-Liner Investigations
```bash
# Count spans by operation name
grep "Span started" dotnet-tracer-managed-dotnet-*.log | grep -o 'OperationName: "[^"]*"' | sort | uniq -c

# Find all unique trace IDs in logs
grep "Span started" dotnet-tracer-managed-dotnet-*.log | grep -o 't_id: [^]]*' | cut -d' ' -f2 | sort -u

# Check for errors during execution
grep "<YYYY-MM-DD HH:MM>:" dotnet-tracer-managed-dotnet-*.log | grep -i "error\|exception\|fail"

# Verify tracer configuration
grep "<YYYY-MM-DD HH:MM>:" dotnet-tracer-managed-dotnet-*.log | grep -i "DD_TRACE"

# Find active scope information
grep "<YYYY-MM-DD HH:MM>:" dotnet-tracer-managed-dotnet-*.log | grep -i "activescope\|internalactivescope"
```

## Tips for Effective Log Analysis

1. **Always use timestamp filtering** - Never rely on file position
2. **Verify tracer version first** - Wrong version = wrong behavior
3. **Start with host logs** - Get trace ID and execution time
4. **Follow the trace ID** - Verify it flows to worker
5. **Check span relationships** - Ensure proper parent-child links
6. **Use context lines (-A/-B)** - See surrounding log entries
7. **Save intermediate results** - Pipe grep results to files for comparison
8. **Build a timeline** - Sort merged host+worker logs chronologically
9. **Look for patterns** - Compare successful vs failed executions
10. **Check both processes** - Some issues only visible in one process

## Noisy Host Spans (Filtered by Script)

The `Get-AzureFunctionLogs.ps1` script automatically filters out these noisy spans from host log analysis (span counting and parenting):

- **`command_execution` spans** — Generated by the Process.Start() instrumentation when the Azure Functions host launches child processes (e.g., the worker, compatibility layer). Disable with `DD_TRACE_Process_ENABLED=false`.
- **`GET /admin/*` spans** — Azure Functions health check pings (`GET /admin/host/ping`) that run continuously. Already excluded by default sampling rules.
- **`GET /robots*.txt` spans** — Bot detection requests to the host, not user traffic.

These spans are internal Azure Functions infrastructure noise and are not relevant for trace parenting or function invocation analysis.

## Common Gotchas

- **Timestamps are UTC** - Adjust for local timezone when correlating
- **Log rotation** - Check for numbered log files (*_001.log, *_002.log)
- **Multiple workers** - Different PIDs may be active simultaneously
- **Async operations** - Spans may interleave unpredictably
- **Case sensitivity** - Use `-i` flag for case-insensitive matching
- **Regex escaping** - Use proper escaping for special characters
- **Process tags** - Added during serialization, not in span creation logs
