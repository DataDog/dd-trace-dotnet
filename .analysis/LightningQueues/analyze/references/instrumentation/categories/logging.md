# Logging Instrumentation

Logging integrations enable trace-log correlation by injecting trace context into log records. This is fundamentally different from other categories - it does NOT create spans.

## What Logging Instrumentation Does

### Context Injection
Inject trace identifiers into log records:
- `trace_id` - Links log to distributed trace
- `span_id` - Links log to specific span
- `service` - Service name
- `env` - Environment
- `version` - Service version

This enables correlation between traces and logs in observability platforms.

## What to Instrument

### Log Emission Methods
The internal method that formats/emits log records:
- Logger write methods: `_emit()`, `write()`, `log()`
- The point where the log record is finalized before output

### Transport/Formatter Integration
- Integration points where trace context can be added to log format
- Mixin functions that enhance log records

## What NOT to Do

### No Span Creation
Logging instrumentation does NOT create spans. It only:
- Reads the current active span from async context
- Decorates log records with trace identifiers

### Skip These
- Logger instantiation
- Log level configuration
- Transport setup
- Formatter registration

## Implementation Pattern

1. Hook into log emission method
2. Check for active span in async context
3. If span exists: inject `trace_id` and `span_id`
4. Always inject: `service`, `env`, `version` (even without active span)
5. Avoid mutating original log record (use proxy or copy)
