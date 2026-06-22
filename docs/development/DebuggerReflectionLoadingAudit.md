# Debugger Reflection Loading Audit

This document tracks debugger reflection paths that can resolve metadata into runtime objects, trigger static initialization, or execute customer code. It is intended as review guidance for future debugger changes and as a record of the safety boundaries established after the `InstanceOf` early-load fix in PR #8785.

Keep these risks separate when auditing or changing debugger code:

- Early runtime resolution: resolving assemblies, types, members, generic signatures, or method tokens earlier than customer code would.
- Static constructor execution: reading static members or otherwise using a type in a way that can run its type initializer.
- Customer code execution: invoking getters, enumerators, `ToString()`, exception overrides, attribute constructors, or similar user-controlled code.

## Classification

| Area | Classification | Current status |
| --- | --- | --- |
| Code Origin endpoint discovery | Metadata-only scan through `EndpointDetector` and `System.Reflection.Metadata`; no runtime member resolution required. | No follow-up from this audit. |
| Line probe resolution | Scans already loaded assemblies and symbol/PDB metadata. | No follow-up from this audit. |
| SymDB symbol extraction/upload | Processes already loaded assemblies plus metadata/PDB information; dnlib paths may resolve metadata definitions but do not intentionally load new customer assemblies. | No follow-up from this audit. |
| Debugger static member capture and expressions | Reading static fields/properties can trigger type initializers. | Fixed by PR #8814: guard static member capture and preserve literal/cctor-free statics. |
| Exception Replay IL call scanning | `Module.ResolveMethod(token)` can resolve arbitrary call operands and load unavailable dependencies while looking for `ExceptionDispatchInfo.Throw`. | Fixed by PR #8815: inspect call operands with metadata instead of runtime resolution. |
| Debugger state-machine attribute resolution | `GetCustomAttributes()` / typed `GetCustomAttribute<T>()` can instantiate attributes while resolving async/iterator methods. | Fixed by PR #8816: use metadata-only `CustomAttributeData` for state-machine attributes. |
| Snapshot capture and expression evaluation | Some paths intentionally execute bounded customer code, such as expression property access and supported collection enumeration. | Governed by `DebuggerCustomerCodeExecutionPolicy.md`; focused regression tests added by PR #8817. |

## Review Guidance

When reviewing debugger capture, expression, exception-replay, Code Origin, or symbol changes, classify each reflection operation before accepting it:

- Metadata-only APIs are preferred for discovery and filtering.
- Runtime `Type`, `MemberInfo`, or signature resolution should be justified and covered by a focused test when it can touch customer assemblies.
- Static member reads must be guarded so they do not run customer type initializers unless explicitly allowed by policy.
- Customer-code execution must be covered by `DebuggerCustomerCodeExecutionPolicy.md` and regression tests.

If a change intentionally crosses one of these boundaries, document why the runtime behavior is required and add a regression test that would fail if the boundary were crossed accidentally.
