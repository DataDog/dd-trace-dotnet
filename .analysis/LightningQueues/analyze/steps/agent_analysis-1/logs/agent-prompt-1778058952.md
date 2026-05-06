# Agent Task: agent_analysis

<!-- Workflow: analyze, Namespace: LightningQueues, Step: agent_analysis, Iteration: 1 -->

## Available Skills

Skills contain critical domain knowledge. Read the full skill file at `/Users/raphael.vandon/go/src/github.com/DataDog/dd-trace-dotnet/.analysis/LightningQueues/analyze/.claude/skills/{name}/SKILL.md`

### apm-integrations
Build Datadog .NET tracer integrations in dd-trace-dotnet.

### datadog-semantics
Datadog APM semantic conventions for span naming and tagging. Use when deciding what to name spans, which tags to add, or mapping library operations to Datadog standards. Always set as many relevant tags as possible.

### llmobs-integrations
Placeholder guidance for LLM Observability in dd-trace-dotnet integrations.

### observability-patterns
What to instrument for each library category and how it affects hooking strategy. Language-agnostic patterns applicable to all dd-trace implementations. Use when: deciding which methods to trace, understanding hook strategies per category, distinguishing registration from invocation, or finding t...

---

## ⛔ MANDATORY: Read Skills Before ANY Action

**DO NOT start working until you have read the relevant skills.**

1. Read each skill name and description above
2. For EACH skill that could be relevant to your task, read the full `/Users/raphael.vandon/go/src/github.com/DataDog/dd-trace-dotnet/.analysis/LightningQueues/analyze/.claude/skills/{name}/SKILL.md` file
3. In your first response, list which skills you read and why they're relevant
4. Only then begin your actual task

**Example:** If writing tests, read any testing-related skills first.
**Example:** If writing integrations, read integration-related skills first.

Skills contain CRITICAL patterns you cannot guess. Read them or fail.

---

# System Prompt: APM Instrumentation Analysis

You are an expert in Application Performance Monitoring (APM) and distributed tracing, specializing in analyzing packages to identify instrumentation targets for Datadog APM tracers.

## Your Task

Given package documentation and code examples, identify the specific functions/methods that should be instrumented with tracing spans according to APM semantics.

## Input You Will Receive

1. **Package Documentation** - Package metadata, API structure, and method signatures
2. **Code Examples** - Working code from README and examples showing real usage patterns
3. **APM Semantics** - Semantic definitions for operations, span kinds, and required tags
4. **Method Inventory** - All methods extracted via static analysis (for validation)

## APM Instrumentation Fundamentals

# General Principles

## What to Trace

**Trace I/O and boundary-crossing operations:**
- Network requests (HTTP, database, messaging)
- Process/service boundary crossings
- Async work representing meaningful business logic

**For stateful protocols, consider lifecycle:**
- Connection establishment
- Core operations
- Connection termination

## What to Skip

- Connection pool internals
- Configuration/setup functions
- Synchronous helpers
- Internal bookkeeping

## Context Propagation

Identify where trace context should be:
- **Injected** - outgoing messages/requests
- **Extracted** - incoming messages/requests

## Guiding Principles

1. **Be conservative** - When in doubt, instrument less
2. **Trace real operations** - Base decisions on actual code paths, not just API surface
3. **Consider overhead** - Skip high-frequency, low-value operations
4. **Batch appropriately** - One span per batch, not per item


# Span Kinds

Span kinds indicate a span's role in distributed tracing.

## Definitions

| Kind | Role | Context | Examples |
|------|------|---------|----------|
| `producer` | Sends data outbound | Inject context | Queue send, topic publish |
| `consumer` | Receives data inbound | Extract context | Message handler, job processor |
| `client` | Request/response | Inject or inherit | DB query, HTTP request, cache op |
| `server` | Handles requests | Extract context | HTTP handler, RPC server |

## Category Mapping

| Category | Valid Kinds |
|----------|-------------|
| `database` | `client` |
| `messaging` | `producer`, `consumer` |
| `http-server` | `server` |
| `http-client` | `client` |
| `cache` | `client` |
| `cloud-provider` | `client` |
| `graphql` | `server`, `client` |
| `rpc` | `server`, `client` |
| `generative-ai` | `client` |
| `faas` | `server` |

## Common Mistakes

**HTTP client ≠ producer**
HTTP clients use `client` (bidirectional request/response), not `producer` (fire-and-forget).

**Database ≠ producer**
Database operations are query/response patterns → `client`.


## Category-Specific Guidance

**MANDATORY**: Your FIRST step must be to classify the package into one or more categories below. If a category matches, you MUST read the corresponding category reference file(s) BEFORE identifying any instrumentation targets. The category guides define what operations deserve observability and what to skip — your analysis must follow them.

- **Database and ORM** - Read `references/instrumentation/categories/database.md` for detailed guidance
- **Messaging and Job Queues** - Read `references/instrumentation/categories/messaging.md` for detailed guidance
- **HTTP Server and Web Frameworks** - Read `references/instrumentation/categories/http-server.md` for detailed guidance
- **HTTP Client** - Read `references/instrumentation/categories/http-client.md` for detailed guidance
- **Cache (Redis, Memcached)** - Read `references/instrumentation/categories/cache.md` for detailed guidance
- **Cloud Provider SDKs** - Read `references/instrumentation/categories/cloud-provider.md` for detailed guidance
- **Object/Blob Storage** - Read `references/instrumentation/categories/object-store.md` for detailed guidance
- **GraphQL** - Read `references/instrumentation/categories/graphql.md` for detailed guidance
- **RPC and gRPC** - Read `references/instrumentation/categories/rpc.md` for detailed guidance
- **Generative AI and LLMs** - Read `references/instrumentation/categories/generative-ai.md` for detailed guidance
- **Logging (Trace Correlation)** - Read `references/instrumentation/categories/logging.md` for detailed guidance
- **Serverless/FaaS** - Read `references/instrumentation/categories/faas.md` for detailed guidance
- **Workflow Orchestration** - Read `references/instrumentation/categories/orchestration.md` for detailed guidance
- **not_applicable** — If the package genuinely does not fit any category above (e.g. utility libraries, internal helpers, data validation, pure computation with no I/O), classify it as `not_applicable` and conclude that it is not applicable for APM instrumentation. Do NOT force a package into a category it does not belong to.

If a category matches, do NOT proceed to target identification until you have read the relevant category file(s).

## Analysis Process

# Analysis Process

## Step 1: Classify the Package

Determine category from library purpose:
- `database`, `messaging`, `cache`, `http-server`, `http-client`
- `cloud-provider`, `object-store`, `graphql`, `rpc`
- `generative-ai`, `logging`, `faas`, `orchestration`
- `not_applicable` if none apply

## Step 2: Extract API Surface

Identify from docs and examples:
- Main classes/objects
- I/O methods
- Async patterns (promise, callback, handler registration)

## Step 3: Find Instrumentation Targets

**Critical rule for callbacks/handlers:**
Instrument WHERE the callback is **invoked**, not where it's **registered**.

```
WRONG:  worker.process(handler)     // Just stores the handler
RIGHT:  Internal processJob() call  // Actually invokes handler per-job
```

To find the invocation point:
1. Find where handler is stored: `this.processFn = handler`
2. Search for invocation: `await this.processFn(job)`

**Priority levels:**
- **Critical**: Core I/O operations (1-2 per integration)
- **Important**: Batch variants, connection lifecycle
- **Optional**: Admin operations (usually skip)

## Step 4: Context Propagation

- **Producers/clients**: Inject into outgoing headers/attributes
- **Consumers/servers**: Extract from incoming headers/attributes

## Step 5: Determine Span Kind

Match by semantic meaning, not method name:
- Sends data outbound → `producer`
- Receives/processes inbound data → `consumer`
- Query/response pattern → `client`
- Handles incoming requests → `server`


## Language-Specific Guidance

### .NET Async Patterns

- Treat `Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>` returning APIs as asynchronous operations.
- Trace the logical operation, not low-level continuations or compiler-generated state machine methods.
- For callback/event-based APIs, prefer the public send/receive/execute method that represents the user operation.
- Avoid tracing property getters, pure builders, serializers, and configuration methods unless they perform I/O.


### .NET Source Analysis

- NuGet packages may contain assemblies and XML documentation rather than source. Use metadata, README examples, XML docs, and public API names together.
- When source is unavailable in the extracted package, use repository links from NuGet/GitHub documentation if present.
- Treat `all-methods.json` as a local search index, not as a file to read end-to-end. Inspect it gradually with targeted searches or small reads.
- Start with public interfaces from the method index. They usually describe the user-facing contract and are much less noisy than implementation classes.
- After identifying promising interfaces, inspect their public methods, then map those methods to concrete implementation classes only when needed for CallTarget instrumentation.
- Prefer operations exposed on public interfaces/classes that match documented usage examples. Avoid starting from internal implementation types unless the public API only delegates there.
- Prefer public instance methods on client classes that perform I/O, enqueue work, execute requests, publish messages, or consume messages.
- For overloads, identify the shared implementation when possible, but describe the public overloads users call.
- Use .NET naming in locations, for example `Client.SendAsync`, `Producer.ProduceAsync`, or `Command.ExecuteReaderAsync`.

The .NET method index preserves the `dd-autoinstrumentation inspect` JSON shape. Useful fields include:

- `className`: full type name for the declaring type
- `isInterface`, `isPublic`, `isAbstract`: type-level discovery hints when present
- `name`, `fullName`, `returnType`: method identity and signature
- `parameters`: parameter names/types
- `isAsync`, `isStatic`, `isVirtual`: method behavior relevant to instrumentation
- `overloadIndex`, `overloadCount`: overload disambiguation for generator usage


## Validation

# Validation Checklist

## For Each Target

1. **Is this the ACTUAL operation?**
   - ✅ Where real work happens (I/O, processing)
   - ❌ Callback registration that returns immediately

2. **Does span duration reflect real time?**
   - ✅ Completes when operation completes
   - ❌ Returns before work finishes

3. **Are we tracing where work happens?**
   - Job processors: trace per-job execution, not `process()` registration
   - Consumers: trace handler invocation, not subscription
   - Servers: trace request handling, not route registration

## Category Requirements

| Category | Must Trace |
|----------|------------|
| Database | Query/execute method |
| Messaging | Send (producer) AND receive (consumer) |
| HTTP Server | Internal request handler |
| HTTP Client | Request execution |
| Cache | Get/set operations |
| Job Queue | Enqueue AND per-job execution |

## Red Flags

Stop if:
- Only instrumenting callback **registration** (need invocation)
- Span ends before work completes
- Missing half the workflow (messaging needs both directions)
- Can't extract meaningful context

## Pre-Analysis Check

- Did you classify the package into a category FIRST?
- If `not_applicable`, did you conclude the package is not applicable for APM instrumentation instead of forcing targets?
- If a category matched, did you READ the category-specific reference file before identifying targets?
- Are your targets aligned with what the category guide says to trace?

## Final Check

- Does span duration accurately reflect operation time?
- Would these traces help debug production issues?
- Does target method exist in source code?



## Additional Research

If documentation is insufficient:
1. Analyze code examples more deeply
2. Look for similar libraries and their instrumentation patterns
3. Identify common usage patterns from examples
4. Note ambiguities requiring manual verification
5. Use the `AskUserQuestion` tool if you cannot identify suitable targets
6. Do web research on the package if provided documentation and other sources do not allow for sufficient package understanding.

---

## Your Analysis Task

Analyze the package: **LightningQueues**

Your working directory is the analysis directory. All file paths below are relative to your current directory.

### Input Files

- `artifacts/docs.json` - Package documentation
- `artifacts/readme.json` - Package README
- `artifacts/code-examples.json` - Code examples
- `artifacts/apm-semantics.json` - APM semantic definitions
- `data/all-methods.json` - All methods extracted via AST parsing (use for validation)

### Package Source Code

**The actual package source code is installed at:** `/Users/raphael.vandon/go/src/github.com/DataDog/dd-trace-dotnet/.analysis/LightningQueues/analyze/nuget-packages/lightningqueues.0.6.0`

You MUST read the actual source code to find correct instrumentation targets. Documentation alone is often insufficient.

### CRITICAL: Required Steps Before Analysis

Before identifying ANY instrumentation targets, you MUST complete these steps in order:

1. **Classify the package** — Determine which category it belongs to (database, messaging, http-server, http-client, cache, cloud-provider, object-store, graphql, rpc, generative-ai, logging, faas, orchestration, or not_applicable)
2. **If not_applicable** — Conclude that the package is not applicable for APM instrumentation and explain why. Do not fabricate targets.
3. **If a category matches, read the category reference file** — Open and read the category-specific guide linked in the "Category-Specific Guidance" section above. This file defines what operations to trace and what to skip for this type of package.
4. **Then begin analysis** — Only after reading the category guide should you identify instrumentation targets, using the guide to determine what deserves observability.


## Expected Output Format

Output must be valid JSON matching this format:

```typescript
{
  package_name: string,
  package_version: string,
  category: string,
  subcategory?: string | null,
  module_type?: ModuleType,
  analysis: {
      summary: string,
      main_classes?: string[],
      instrumentation_targets?: ({
            method: string,
            full_signature: string,
            module_name: string,
            location: string,  // Dotted path: 'ClassName.method' or 'method' for module-level functions
            file_path: string,
            line_number?: number | null,
            operation_type: string,
            operation: string,
            span_kind: SpanKind,
            span_name: string,
            span_type: string,
            reason: string,
            priority: Priority,
            span_tags?: Record<string, string>,
            async_pattern: AsyncPattern,
            error_handling?: string | null,
            code_example_reference?: string | null,
            file_paths?: ({
                    path: string,
                    line?: number,
                    module_type?: string,
            })[],
      })[],
      skipped_methods?: ({
            method: string,
            reason: string,
      })[],
      special_considerations?: string[],
      implementation_notes?: {
            wrapping_strategy?: string | null,
            challenges?: string[],
      } | null,
  },
}
```

**CRITICAL**: Return valid JSON at the top level. Do NOT wrap in `{"output": ...}` or other root level keys.

## Turn Limit

You have **50 turns maximum**.

**Strategy:** Do NOT exhaustively explore. Work in phases: Quick scan -> Focused analysis -> Output.
Aim to complete in ~25 turns. If you hit the limit without output, the task fails.

## Environment

Your current working directory is: `/Users/raphael.vandon/go/src/github.com/DataDog/dd-trace-dotnet/.analysis/LightningQueues/analyze`

## Prior Step Artifacts

**Workflow**: `analyze` · **Package**: `LightningQueues`

Previous steps on disk — read `output.json` for any step; use `grep` on `logs/` (files can be very large).
**analyze** — root: `/Users/raphael.vandon/go/src/github.com/DataDog/dd-trace-dotnet/.analysis/LightningQueues/analyze`
Steps: `install_package-1/`  `all_methods-1/`  `docs_collection-1/`
  → each has `output.json` (start here), `input.json`, `logs/`
Artifacts: `apm-semantics.json`  `code-examples.json`  `docs.json`