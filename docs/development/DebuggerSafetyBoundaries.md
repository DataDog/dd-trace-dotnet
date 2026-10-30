# Debugger Safety Boundaries

Debugger and Dynamic Instrumentation code runs inside customer processes while inspecting live customer objects. Treat every reflection call, metadata lookup, and value read as potentially observable by the customer application unless the API is known to be metadata-only or limited to instance fields.

Use this document when changing debugger capture, expression evaluation, Exception Replay, Code Origin, symbol extraction, async or iterator resolution, or any helper used by those paths.

Most capture and expression-evaluation code runs synchronously on customer application threads. Keep work bounded, avoid unnecessary allocation, and assume any added latency is paid directly by the instrumented request or background operation.

## Risk Model

Keep these risks separate when auditing or changing debugger code:

- Early runtime resolution: resolving assemblies, types, members, generic signatures, or method tokens earlier than customer code would.
- Static constructor execution: reading static members or otherwise using a type in a way that can run its type initializer.
- Customer-code execution: invoking getters, enumerators, `ToString()`, exception overrides, attribute constructors, operators, or similar user-controlled code.

## Area Guidance

| Area | Main risk | Preferred handling |
| --- | --- | --- |
| Code Origin endpoint discovery | Accidentally resolving runtime types or attributes while identifying endpoints. | Prefer `System.Reflection.Metadata` and token/name inspection. Avoid runtime member resolution unless the change explains why it is required. |
| Line probe resolution | Loading or resolving customer code while matching probes to source locations. | Restrict discovery to already loaded assemblies and symbol/PDB metadata. Treat new runtime resolution as a behavior change that needs focused tests. |
| SymDB symbol extraction/upload | Resolving metadata in a way that loads unavailable dependencies or executes customer code. | Prefer metadata/PDB readers. If dnlib or reflection is used, verify it does not intentionally load new customer assemblies or instantiate attributes. |
| Static member capture and expressions | Static field/property reads can trigger type initializers. | Read literals with `StaticMemberSafety.GetRawConstantValue`. Read other static fields, including static auto-property backing fields, only when `StaticMemberSafety.CanReadStaticMember` allows it (the declaring type has no type initializer); otherwise skip capture. Static property getters run code, so they also fall under the customer-code execution policy below. |
| Exception Replay IL call scanning | `Module.ResolveMethod(token)` and similar APIs can resolve arbitrary call operands and load unavailable dependencies. | Prefer metadata inspection of call operands. If runtime resolution remains necessary, justify why metadata is insufficient, catch failures locally, and add tests for missing dependency and generic-signature cases. |
| Async and iterator state-machine resolution | `GetCustomAttributes()` and typed `GetCustomAttribute<T>()` can instantiate customer attributes. | Prefer `CustomAttributeData` or metadata-only attribute inspection when matching state-machine attributes. Do not instantiate attributes just to identify compiler-generated methods. |
| Snapshot capture and expression evaluation | Some paths intentionally execute bounded customer code, while default object capture should not. | Follow the customer-code execution policy below and add regression tests for any intentional boundary crossing. |

## Customer-Code Execution Policy

Default object capture should avoid customer-code execution. It may read instance fields, compiler-generated backing fields, and metadata because those operations do not call user methods. It must not call arbitrary instance property getters, indexers, enumerators, or `ToString()` as part of normal object field capture.

Some debugger features exist to evaluate live runtime values, so they may execute customer code when that behavior is explicit and constrained:

- User-authored probe expressions may invoke the supported collection operations the expression requests, such as `len`, `any`, `filter`, and `index`, which can call `Count`, enumerators, and `IList`/`IDictionary` indexers.
- Probe expression member access (`getmember`, `ref`) goes through `TryResolveSafeMemberExpression`, which reads fields and verified compiler-generated auto-property backing fields only. It does not call property getters, apart from `Key`/`Value` on BCL `KeyValuePair<,>` and `DictionaryEntry` entries. A property that can't be read this way evaluates to `UndefinedValue`. Static fields follow the static member capture row in the Area Guidance table.
- Supported collection and dictionary capture may read `Count`, call `GetEnumerator()`, `MoveNext()`, `Current`, and dictionary entry `Key`/`Value` accessors, subject to configured depth, collection-size, and cooperative timeout checks.
- Special system-type selectors may read documented BCL properties, such as selected `System.Exception` and `System.Lazy<T>` properties.
- Safe BCL `ToString()` calls may be used only for types allowed by `Redaction.IsSafeToCallToString()`.

Probe expression evaluation runs under a cooperative `EvaluationBudget`, configured by `DD_DYNAMIC_INSTRUMENTATION_EVALUATION_TIMEOUT_MS` (default and accepted range in `supported-configurations.yaml`). The parser inserts budget checkpoints into loops and selected string, regex, and type-check operations, and `matches` also passes the remaining budget to `Regex` as a hard timeout. This budget is separate from the cancellation timeout `DebuggerSnapshotSerializer` uses for snapshot capture, including the collection capture above.

Both the expression budget and the serializer timeout are best-effort around synchronous customer callbacks; they cannot preempt a blocking or long-running `Count`, enumerator, `MoveNext()`, `Current`, indexer, `Key`, or `Value` call.

Capture paths must not silently broaden default capture to execute customer code. When a value is useful but would require disallowed customer-code execution, prefer omitting that value and reporting a `notCapturedReason` over executing the code.

## Bounds And Overhead

Capture and expression-evaluation paths must have clear bounds for depth, collection size, field count, string length, and elapsed work where applicable. New traversal, filtering, formatting, or evaluation logic should either use existing capture limits or introduce an explicit limit with tests. New loops or other open-ended work in expression evaluation, including helpers called from generated expressions, should check the evaluation budget described above: `BudgetCheck()` in generated code, `EvaluationBudget.ThrowIfExceeded` in helpers.

Avoid avoidable allocations on capture hot paths. Prefer existing caches, pooled helpers, metadata handles, and streaming writers over building intermediate object graphs or strings. Do not add reflection scans, expression compilation, LINQ-heavy traversal, or broad allocations to per-capture paths without a clear reason and focused performance consideration.

## Review Checklist

When reviewing debugger changes, classify each reflection operation before accepting it:

- Prefer metadata-only APIs for discovery and filtering.
- Justify runtime `Type`, `MemberInfo`, signature, method-token, or attribute resolution when it can touch customer assemblies.
- Guard static member reads so they do not run customer type initializers unless the behavior is explicitly allowed.
- Cover intentional customer-code execution with policy text and regression tests.
- Verify capture and expression-evaluation work is bounded and appropriate for synchronous execution on customer threads.
- Check allocation behavior for new hot-path code, especially reflection, string formatting, LINQ, collection materialization, and expression compilation.

Look specifically for newly introduced calls or expression-tree access to:

- `PropertyInfo.GetValue`, property access, or indexer access, including `Expression.Property` or `Expression.PropertyOrField` in generated expression trees. Probe expression member access on customer types should go through `TryResolveSafeMemberExpression`.
- `FieldInfo.GetValue` on static fields.
- `Module.ResolveMethod`, `Module.ResolveMember`, `Type.GetType`, `Assembly.Load`, or APIs that can resolve missing dependencies.
- `GetCustomAttributes()`, typed `GetCustomAttribute<T>()`, or other APIs that instantiate attributes.
- `IEnumerable.GetEnumerator`, `MoveNext`, `Current`, `Count`, `Key`, or `Value`.
- `Exception.Message`, `Exception.StackTrace`, `Exception.ToString`, or custom exception overrides.
- arbitrary `ToString()`, `Equals()`, operators, delegates, callbacks, or expression-compiled accessors.

If a change intentionally crosses one of these boundaries, document why the runtime behavior is required, state whether it is covered by the allowed execution policy, and add a regression test that would fail if the boundary were crossed accidentally.
