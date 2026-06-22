# Debugger Safety Boundaries

Debugger and Dynamic Instrumentation code runs inside customer processes while inspecting live customer objects. This document defines the main safety boundaries for reflection, metadata inspection, static member capture, and customer-code execution. It is intended as review guidance for future debugger changes and as a record of the boundaries established after the `InstanceOf` early-load fix in PR #8785.

## Risk Model

Keep these risks separate when auditing or changing debugger code:

- Early runtime resolution: resolving assemblies, types, members, generic signatures, or method tokens earlier than customer code would.
- Static constructor execution: reading static members or otherwise using a type in a way that can run its type initializer.
- Customer-code execution: invoking getters, enumerators, `ToString()`, exception overrides, attribute constructors, or similar user-controlled code.

## Reflection Classification

| Area | Classification | Current status |
| --- | --- | --- |
| Code Origin endpoint discovery | Metadata-only scan through `EndpointDetector` and `System.Reflection.Metadata`; no runtime member resolution required. | No follow-up from this audit. |
| Line probe resolution | Scans already loaded assemblies and symbol/PDB metadata. | No follow-up from this audit. |
| SymDB symbol extraction/upload | Processes already loaded assemblies plus metadata/PDB information; dnlib paths may resolve metadata definitions but do not intentionally load new customer assemblies. | No follow-up from this audit. |
| Debugger static member capture and expressions | Reading static fields/properties can trigger type initializers. | Fixed by PR #8814: guard static member capture and preserve literal/cctor-free statics. |
| Exception Replay IL call scanning | `Module.ResolveMethod(token)` can resolve arbitrary call operands and load unavailable dependencies while looking for `ExceptionDispatchInfo.Throw`. | Fixed by PR #8815: inspect call operands with metadata instead of runtime resolution. |
| Debugger state-machine attribute resolution | `GetCustomAttributes()` / typed `GetCustomAttribute<T>()` can instantiate attributes while resolving async/iterator methods. | Fixed by PR #8816: use metadata-only `CustomAttributeData` for state-machine attributes. |
| Snapshot capture and expression evaluation | Some paths intentionally execute bounded customer code, such as expression property access and supported collection enumeration. | Governed by the customer-code execution policy below; focused regression tests added by PR #8817. |

## Customer-Code Execution Policy

Default object capture should avoid customer-code execution. It may read instance fields, compiler-generated backing fields, and metadata because those operations do not call user methods. It must not call arbitrary instance property getters as part of normal object field capture.

Static member capture is governed by the static member safety policy in code: literal constants are safe to read from metadata, and non-literal static fields/properties are only read when their declaring type has no type initializer.

Some debugger features exist to evaluate live runtime values, so they are allowed to execute bounded customer code:

- User-authored probe expressions may read properties or invoke supported collection operations requested by the expression.
- Supported collection and dictionary capture may read `Count`, call `GetEnumerator()`, `MoveNext()`, `Current`, and dictionary entry `Key`/`Value` accessors, subject to configured depth, collection-size, and timeout limits.
- Special system-type selectors may read documented BCL properties, such as selected `System.Exception` and `System.Lazy<T>` properties.
- Safe BCL `ToString()` calls may be used only for types allowed by `Redaction.IsSafeToCallToString()`.

Capture paths must not silently broaden default capture to execute customer code. In particular, future changes should not call arbitrary getters, indexers, enumerable methods, or `ToString()` for unknown customer types unless the behavior is made explicit in product policy and covered by tests.

When a value is useful but would require disallowed customer-code execution, prefer omitting that value and reporting a `notCapturedReason` over executing the code.

## Review Checklist

When reviewing debugger capture, expression, exception-replay, Code Origin, or symbol changes, classify each reflection operation before accepting it:

- Metadata-only APIs are preferred for discovery and filtering.
- Runtime `Type`, `MemberInfo`, or signature resolution should be justified and covered by a focused test when it can touch customer assemblies.
- Static member reads must be guarded so they do not run customer type initializers unless explicitly allowed by policy.
- Customer-code execution must be covered by the policy above and regression tests.

Reviewers should identify whether a change introduces any of the following operations:

- `PropertyInfo.GetValue`, expression property access, or indexer access
- `IEnumerable.GetEnumerator`, `MoveNext`, `Current`, `Count`, `Key`, or `Value`
- `Exception.Message`, `Exception.StackTrace`, `Exception.ToString`, or custom exception overrides
- arbitrary `ToString()`, `Equals()`, or operator calls

If a change intentionally crosses one of these boundaries, document why the runtime behavior is required, state whether it is covered by the allowed execution policy, and add a regression test that would fail if the boundary were crossed accidentally.
