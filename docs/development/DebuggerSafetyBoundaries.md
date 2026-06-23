# Debugger Safety Boundaries

Debugger and Dynamic Instrumentation code runs inside customer processes while inspecting live customer objects. Treat every reflection call, metadata lookup, and value read as potentially observable by the customer application unless the API is known to be metadata-only or limited to instance fields.

Use this document when changing debugger capture, expression evaluation, Exception Replay, Code Origin, symbol extraction, async or iterator resolution, or any helper used by those paths.

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
| Static member capture and expressions | Static field/property reads can trigger type initializers. | Read static literals from metadata when possible. For non-literal static fields or properties, verify the implementation cannot run a customer type initializer, or skip capture. |
| Exception Replay IL call scanning | `Module.ResolveMethod(token)` and similar APIs can resolve arbitrary call operands and load unavailable dependencies. | Prefer metadata inspection of call operands. If runtime resolution remains necessary, justify why metadata is insufficient, catch failures locally, and add tests for missing dependency and generic-signature cases. |
| Async and iterator state-machine resolution | `GetCustomAttributes()` and typed `GetCustomAttribute<T>()` can instantiate customer attributes. | Prefer `CustomAttributeData` or metadata-only attribute inspection when matching state-machine attributes. Do not instantiate attributes just to identify compiler-generated methods. |
| Snapshot capture and expression evaluation | Some paths intentionally execute bounded customer code, while default object capture should not. | Follow the customer-code execution policy below and add regression tests for any intentional boundary crossing. |

## Customer-Code Execution Policy

Default object capture should avoid customer-code execution. It may read instance fields, compiler-generated backing fields, and metadata because those operations do not call user methods. It must not call arbitrary instance property getters, indexers, enumerators, or `ToString()` as part of normal object field capture.

Some debugger features exist to evaluate live runtime values, so they may execute customer code when that behavior is explicit and constrained:

- User-authored probe expressions may read properties or invoke supported collection operations requested by the expression.
- Supported collection and dictionary capture may read `Count`, call `GetEnumerator()`, `MoveNext()`, `Current`, and dictionary entry `Key`/`Value` accessors, subject to configured depth, collection-size, and cooperative timeout checks.
- Special system-type selectors may read documented BCL properties, such as selected `System.Exception` and `System.Lazy<T>` properties.
- Safe BCL `ToString()` calls may be used only for types allowed by `Redaction.IsSafeToCallToString()`.

Timeouts are best-effort around synchronous customer callbacks; they cannot preempt a blocking or long-running `Count`, enumerator, `MoveNext()`, `Current`, `Key`, or `Value` call.

Capture paths must not silently broaden default capture to execute customer code. When a value is useful but would require disallowed customer-code execution, prefer omitting that value and reporting a `notCapturedReason` over executing the code.

## Review Checklist

When reviewing debugger changes, classify each reflection operation before accepting it:

- Prefer metadata-only APIs for discovery and filtering.
- Justify runtime `Type`, `MemberInfo`, signature, method-token, or attribute resolution when it can touch customer assemblies.
- Guard static member reads so they do not run customer type initializers unless the behavior is explicitly allowed.
- Cover intentional customer-code execution with policy text and regression tests.

Look specifically for newly introduced calls or expression-tree access to:

- `PropertyInfo.GetValue`, property access, or indexer access.
- `FieldInfo.GetValue` on static fields.
- `Module.ResolveMethod`, `Module.ResolveMember`, `Type.GetType`, `Assembly.Load`, or APIs that can resolve missing dependencies.
- `GetCustomAttributes()`, typed `GetCustomAttribute<T>()`, or other APIs that instantiate attributes.
- `IEnumerable.GetEnumerator`, `MoveNext`, `Current`, `Count`, `Key`, or `Value`.
- `Exception.Message`, `Exception.StackTrace`, `Exception.ToString`, or custom exception overrides.
- arbitrary `ToString()`, `Equals()`, operators, delegates, callbacks, or expression-compiled accessors.

If a change intentionally crosses one of these boundaries, document why the runtime behavior is required, state whether it is covered by the allowed execution policy, and add a regression test that would fail if the boundary were crossed accidentally.
