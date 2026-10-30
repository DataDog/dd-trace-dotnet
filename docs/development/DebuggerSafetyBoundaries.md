# Debugger Safety Boundaries

Debugger and Dynamic Instrumentation code runs inside customer processes while inspecting live customer objects. Treat every reflection call, metadata lookup, and value read as potentially observable by the customer application unless the API is known to be metadata-only or limited to instance fields.

Use this document when changing debugger capture, expression evaluation, redaction, Exception Replay, Code Origin, symbol extraction, async or iterator resolution, or any helper used by those paths.

Most capture and expression-evaluation code runs synchronously on customer application threads. Keep work bounded, avoid unnecessary allocation, and assume any added latency is paid directly by the instrumented request or background operation.

The rules below are requirements for new and changed code. Some existing paths don't meet the newer ones yet, including the redaction, runtime-type, failure-path, and per-hit lock rules; don't widen those gaps.

## Risk Model

Keep these risks separate when auditing or changing debugger code:

- Early runtime resolution: resolving assemblies, types, members, generic signatures, or method tokens earlier than customer code would.
- Static constructor execution: reading static members or otherwise using a type in a way that can run its type initializer.
- Customer-code execution: invoking getters, enumerators, `ToString()`, exception overrides, attribute constructors, operators, or similar user-controlled code.
- Data exposure: sending a sensitive value, or a result computed from one, out of the process.

## Area Guidance

| Area | Main risk | Preferred handling |
| --- | --- | --- |
| Code Origin endpoint discovery | Accidentally resolving runtime types or attributes while identifying endpoints. | Prefer `System.Reflection.Metadata` and token/name inspection. Avoid runtime member resolution unless the change explains why it is required. |
| Line probe resolution | Loading or resolving customer code while matching probes to source locations. | Restrict discovery to already loaded assemblies and symbol/PDB metadata. Treat new runtime resolution as a behavior change that needs focused tests. |
| SymDB symbol extraction/upload | Resolving metadata in a way that loads unavailable dependencies or executes customer code. | Prefer metadata/PDB readers. If dnlib or reflection is used, verify it does not intentionally load new customer assemblies or instantiate attributes. |
| Static member capture and expressions | Static field/property reads can trigger type initializers. | Read literals with `StaticMemberSafety.GetRawConstantValue`. Read other static fields, including static auto-property backing fields, only when `StaticMemberSafety.CanReadStaticMember` allows it (the declaring type has no type initializer); otherwise skip capture. Static property getters run code, so don't call them outside the customer-code execution policy below; `CanReadStaticMember` only rules out the declaring type's initializer, not what the getter body runs. |
| Exception Replay IL call scanning | `Module.ResolveMethod(token)` and similar APIs can resolve arbitrary call operands and load unavailable dependencies. | Prefer metadata inspection of call operands. If runtime resolution remains necessary, justify why metadata is insufficient, catch failures locally, and add tests for missing dependency and generic-signature cases. |
| Async and iterator state-machine resolution | `GetCustomAttributes()` and typed `GetCustomAttribute<T>()` can instantiate customer attributes. | Prefer `CustomAttributeData` or metadata-only attribute inspection when matching state-machine attributes. Do not instantiate attributes just to identify compiler-generated methods. |
| Snapshot capture and expression evaluation | Some paths intentionally execute bounded customer code, while default object capture should not. | Follow the customer-code execution policy below and add regression tests for any intentional boundary crossing. |

## Customer-Code Execution Policy

Default object capture should avoid customer-code execution. It may read instance fields, compiler-generated backing fields, and metadata because those operations do not call user methods. It must not call arbitrary property getters (instance or static), indexers, enumerators, or `ToString()` as part of normal object field capture.

Some debugger features exist to evaluate live runtime values, so they may execute customer code when that behavior is explicit and constrained:

- User-authored probe expressions may invoke the supported probe expression operations the expression requests, such as `len`, `any`, `filter`, and `index`, which can call `Count`, enumerators, and `IList`/`IDictionary` indexers.
- Probe expression member access (the `getmember` and `ref` operations) goes through `TryResolveSafeMemberExpression`, which reads fields and verified compiler-generated auto-property backing fields only. It does not call property getters, apart from `Key`/`Value` on BCL `KeyValuePair<,>` and `DictionaryEntry` entries. A property that can't be read this way evaluates to `UndefinedValue`. Static fields follow the static member capture row in the Area Guidance table.
- Supported collection and dictionary capture may read `Count`, call `GetEnumerator()`, `MoveNext()`, `Current`, and dictionary entry `Key`/`Value` accessors, subject to depth and collection-size limits and to the serializer timeout (snapshot capture) or the evaluation budget (expression dumps).
- Special system-type selectors may read documented BCL properties, such as selected `System.Exception` and `System.Lazy<T>` properties.
- Safe BCL `ToString()` calls may be used only for types allowed by `Redaction.IsSafeToCallToString()`.

**Runtime type, not declared type.** Before enumerating a value, formatting it with `ToString()`, or reading `Exception.Message` or `Exception.StackTrace`, check its runtime type against the existing allowlists, such as `Redaction.IsSupportedCollection` and `Redaction.IsSafeToCallToString`. Values read through members, fields, indexes, or interfaces keep their declared type, which can hide a customer subclass or implementation. The declared type settles the question only when it is sealed or a value type.

Capture paths must not silently broaden default capture to execute customer code. When a value is useful but would require disallowed customer-code execution, prefer omitting that value and reporting a `notCapturedReason` over executing the code.

## Redaction

Everything the debugger sends out of the process must be redacted: snapshots, rendered log messages, span decoration tags, metric values, evaluation errors, and tracer log lines. `Redaction` decides by configured identifier keywords, redacted types, and sensitive dictionary keys. It classifies names and types; it can't scrub free text such as exception messages. The `Redaction.IsSafeToCallToString` list and the exception selectors say which code may run, not that the resulting text is free of sensitive data.

- Redact wherever a value is read, not only where it is serialized whole: member and index access, iteration items, `this`, `@return`, `@exception`, `@key`, and `@value`.
- A result computed from a redacted value is redacted too. Operators and conditions must not return anything computed from the value or from the `{redacted}` placeholder, because a condition such as `eq(secret, "guess")` leaks the value one guess at a time. Render `{redacted}`, make the condition false with an error, and send no metric value.
- Check dictionary keys however they are produced: literal, computed from constants, or read at runtime.
- Redact by runtime type, under the runtime-type rule above: a redacted type can sit behind `object`, an interface, or a base class.
- Match keywords against source names, so compiler-generated names such as `<password>k__BackingField` can't bypass them.
- Exception text isn't redacted, so it must not carry a value the expression redacted. In evaluation errors, use a fixed message instead of the exception's message when an input was redacted or the exception may come from customer code or carry customer data; for example, a `KeyNotFoundException` message includes its key.
- Logs and errors about a redaction may name the expression the user wrote, but must not include the redacted value or data read at runtime, such as a runtime dictionary key or runtime type name.

## Bounds And Overhead

Capture and expression-evaluation paths must have clear bounds for depth, collection size, field count, string length, and elapsed work where applicable. New traversal, filtering, formatting, or evaluation logic should either use existing capture limits or introduce an explicit limit with tests.

Probe expression evaluation runs under a cooperative `EvaluationBudget`, configured by `DD_DYNAMIC_INSTRUMENTATION_EVALUATION_TIMEOUT_MS` (default and accepted range in `supported-configurations.yaml`). The parser inserts budget checkpoints into loops and selected string, regex, and type-check operations, and `matches` also passes the remaining budget to `Regex` as a hard timeout. New loops or other open-ended work in expression evaluation should check the budget too: emit `BudgetCheck()` while building the expression tree, and call `EvaluationBudget.ThrowIfExceeded` from helpers called by generated expressions, or `ThrowIfExceededImmediately` before a single expensive call. The budget is separate from the cancellation timeout `DebuggerSnapshotSerializer` uses for snapshot capture.

Both the expression budget and the serializer timeout are best-effort around synchronous customer callbacks; they cannot preempt a blocking or long-running `Count`, enumerator, `MoveNext()`, `Current`, indexer, `Key`, or `Value` call.

Avoid avoidable allocations on capture hot paths. Prefer existing caches, pooled helpers, metadata handles, and streaming writers over building intermediate object graphs or strings. Do not add reflection scans, expression compilation, LINQ-heavy traversal, or broad allocations to per-capture paths without a clear reason and focused performance consideration.

Failure paths need the same limits as success paths. An expression or capture that fails on every hit must not build an error snapshot or write a log line on every hit: sample or rate-limit error snapshots and log once per probe. When a failure is already known when the expression compiles, report it without evaluating the expression on every hit.

Helpers on capture and evaluation paths must not take a shared or process-wide lock on every hit, and their caches must be bounded. In new and changed caches, keep entries keyed by customer data separate so that data can't evict other entries, drop cached safety decisions when the configuration they depend on changes, and don't keep collectible types or customer objects alive.

## Review Checklist

When reviewing debugger changes, check each of these before accepting the change:

- Prefer metadata-only APIs for discovery and filtering.
- Justify runtime `Type`, `MemberInfo`, signature, method-token, or attribute resolution when it can touch customer assemblies.
- Guard static member reads so they do not run customer type initializers unless the behavior is explicitly allowed.
- Cover intentional customer-code execution with policy text and regression tests.
- Verify capture and expression-evaluation work is bounded and appropriate for synchronous execution on customer threads.
- Check allocation and locking behavior for new hot-path code, especially reflection, string formatting, LINQ, collection materialization, expression compilation, and shared caches.
- Verify every new output path (snapshot field, message segment, tag, metric, error, or log text) is redacted, and that results derived from a redacted value stay redacted.
- Check that decisions to enumerate, format, or redact a value use its runtime type when the declared type isn't sealed or a value type.

Look specifically for newly introduced calls or expression-tree access to:

- `PropertyInfo.GetValue`, property access, or indexer access, including `Expression.Property` or `Expression.PropertyOrField` in generated expression trees. Probe expression member access on customer types should go through `TryResolveSafeMemberExpression`.
- `FieldInfo.GetValue` on static fields.
- `Module.ResolveMethod`, `Module.ResolveMember`, `Type.GetType`, `Assembly.Load`, or APIs that can resolve missing dependencies.
- `GetCustomAttributes()`, typed `GetCustomAttribute<T>()`, or other APIs that instantiate attributes.
- `IEnumerable.GetEnumerator`, `MoveNext`, `Current`, `Count`, `Key`, or `Value`.
- `Exception.Message`, `Exception.StackTrace`, `Exception.ToString`, or custom exception overrides.
- arbitrary `ToString()`, `Equals()`, operators, delegates, callbacks, or expression-compiled accessors.
- `Expression.Equal` or `Expression.NotEqual` used as a null check in generated expression trees, which binds to a user-defined `operator ==`. Use `Expression.ReferenceEqual`, or `HasValue` for `Nullable<T>`.

If a change intentionally crosses one of these boundaries, document why the runtime behavior is required, state whether it is covered by the allowed execution policy, and add a regression test that would fail if the boundary were crossed accidentally.
