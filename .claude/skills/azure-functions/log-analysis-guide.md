# Azure Functions Log Analysis Guide

This guide provides detailed patterns for analyzing Azure Functions tracer logs.

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

## Essential Grep Patterns

### Finding Executions by Timestamp
```bash
# Specific minute
grep "2026-01-23 17:53:" worker.log

# Specific seconds
grep "2026-01-23 17:53:3[89]" worker.log

# Entire hour range
grep "2026-01-23 17:5[0-9]:" worker.log

# Last 5 minutes (adjust regex for your time)
grep "2026-01-23 17:5[3-7]:" worker.log
```

### Finding Initializations
```bash
# All worker initializations (shows restart history)
grep "Assembly metadata" worker.log

# Most recent initialization (current process)
grep "Assembly metadata" worker.log | tail -1

# Get initialization timestamp for filtering
grep "Assembly metadata" worker.log | tail -1 | awk '{print $1, $2}'
```

### Finding Span Information
```bash
# All spans created in timeframe
grep "2026-01-23 17:53:" worker.log | grep "Span started"

# Specific trace ID
grep "68e948220000000047fef7bad8bb854e" worker.log

# Root spans only (orphaned traces)
grep "p_id: null" worker.log | grep "Span started"

# Span closed events (see final tags)
grep "2026-01-23 17:53:" worker.log | grep "Span closed"
```

### Finding Integration Activity
```bash
# Azure Functions specific
grep "2026-01-23 17:53:" worker.log | grep -i "azure.functions\|FunctionExecutionMiddleware"

# Host process integrations
grep "2026-01-23 17:53:" host.log | grep -i "ToRpcHttp\|FunctionInvocationMiddleware"

# AsyncLocal context flow
grep "2026-01-23 17:53:" worker.log | grep -i "asynclocal\|active.*scope"
```

### Context Lines for Better Understanding
```bash
# Show 2 lines before and after
grep -B 2 -A 2 "Span started" worker.log

# Show 5 lines after span creation (see tags)
grep -A 5 "Span started" worker.log

# Show 10 lines around error
grep -B 5 -A 5 "ERROR\|Exception" worker.log
```

## Parsing Span Relationships

### Extract Span Fields
```bash
# Extract trace_id | span_id | parent_id
grep "Span started" worker.log \
  | sed -E 's/.*\[s_id: ([^,]+), p_id: ([^,]+), t_id: ([^\]]+)\].*/\3 | \1 | \2/' \
  | sort
```

### Build Span Tree
```bash
# Find all spans in a trace
grep "68e948220000000047fef7bad8bb854e" host.log worker.log \
  | grep "Span started" \
  | awk -F'[\\[\\]]' '{print $2}'
```

### Identify Span Type
```bash
# Extract operation names
grep "Span started" worker.log \
  | sed -E 's/.*OperationName: "([^"]+)".*/\1/'

# Find specific operation
grep "Span started" worker.log | grep "azure_functions.invoke"
```

## Common Investigation Patterns

### Pattern 1: Verify Version After Deployment
```bash
# 1. Find most recent worker initialization
INIT_TIME=$(grep "Assembly metadata" worker.log | tail -1 | awk '{print $1, $2}')
echo "Most recent initialization: $INIT_TIME"

# 2. Check version in that initialization
grep "$INIT_TIME" worker.log | grep "TracerVersion"

# 3. Verify version in subsequent logs
grep "2026-01-23 17:53:" worker.log | grep "TracerVersion" | head -1
```

### Pattern 2: Trace Host→Worker Flow
```bash
# 1. Find host execution time
grep "Executing 'Functions" host.log | tail -1

# 2. Get host trace ID
HOST_TIME="2026-01-23 17:53:39"
TRACE_ID=$(grep "$HOST_TIME" host.log | grep "Span started" | grep -o 't_id: [^]]*' | cut -d' ' -f2)
echo "Host trace ID: $TRACE_ID"

# 3. Check if worker has spans in same trace
grep "$TRACE_ID" worker.log | wc -l
echo "Worker spans in this trace: $(grep "$TRACE_ID" worker.log | wc -l)"

# 4. Show all spans in chronological order
grep "$TRACE_ID" host.log worker.log | grep "Span started" | sort
```

### Pattern 3: Find Orphaned Traces
```bash
# Find all root spans (should only be in host)
grep "2026-01-23 17:53:" host.log worker.log \
  | grep "Span started" \
  | grep "p_id: null"

# Count root spans by file
echo "Host root spans:"
grep "2026-01-23 17:53:" host.log | grep "Span started" | grep "p_id: null" | wc -l
echo "Worker root spans (should be 0):"
grep "2026-01-23 17:53:" worker.log | grep "Span started" | grep "p_id: null" | wc -l
```

### Pattern 4: Compare Multiple Executions
```bash
# Save logs for each execution
grep "2026-01-23 17:48:" worker.log > exec1.log
grep "2026-01-23 17:53:" worker.log > exec2.log

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

TIMESTAMP="2026-01-23 17:53:3[89]"

echo "=== Host Spans ==="
grep "$TIMESTAMP" host.log \
  | grep "Span started" \
  | sed -E 's/.*\[s_id: ([^,]+), p_id: ([^,]+), t_id: ([^\]]+)\].*OperationName: "([^"]+)".*/\4: s=\1 p=\2 t=\3/'

echo ""
echo "=== Worker Spans ==="
grep "$TIMESTAMP" worker.log \
  | grep "Span started" \
  | sed -E 's/.*\[s_id: ([^,]+), p_id: ([^,]+), t_id: ([^\]]+)\].*OperationName: "([^"]+)".*/\4: s=\1 p=\2 t=\3/'
```

### Find Timing Information
```bash
#!/bin/bash
# span-timing.sh - Extract span durations

TRACE_ID="68e948220000000047fef7bad8bb854e"

# Find spans with start/end times
grep "$TRACE_ID" host.log worker.log \
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
grep "Span started" worker.log | grep -o 'OperationName: "[^"]*"' | sort | uniq -c

# Find all unique trace IDs in logs
grep "Span started" worker.log | grep -o 't_id: [^]]*' | cut -d' ' -f2 | sort -u

# Check for errors during execution
grep "2026-01-23 17:53:" worker.log | grep -i "error\|exception\|fail"

# Verify tracer configuration
grep "2026-01-23 17:53:" worker.log | grep -i "DD_TRACE"

# Find active scope information
grep "2026-01-23 17:53:" worker.log | grep -i "activescope\|internalactivescope"
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

## Common Gotchas

- **Timestamps are UTC** - Adjust for local timezone when correlating
- **Log rotation** - Check for numbered log files (*_001.log, *_002.log)
- **Multiple workers** - Different PIDs may be active simultaneously
- **Async operations** - Spans may interleave unpredictably
- **Case sensitivity** - Use `-i` flag for case-insensitive matching
- **Regex escaping** - Use proper escaping for special characters
- **Process tags** - Added during serialization, not in span creation logs
