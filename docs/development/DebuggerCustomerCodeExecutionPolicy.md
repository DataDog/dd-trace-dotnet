# Debugger Customer Code Execution Policy

Dynamic Instrumentation snapshots and expressions inspect live customer objects in-process. This document defines when that inspection may execute customer code and when future changes must prefer a `notCapturedReason` instead.

## Default Capture

Default object capture should avoid customer code execution. It may read instance fields, compiler-generated backing fields, and metadata because those operations do not call user methods. It must not call arbitrary instance property getters as part of normal object field capture.

Static member capture is governed by the static member safety policy in code: literal constants are safe to read from metadata, and non-literal static fields/properties are only read when their declaring type has no type initializer.

## Explicitly Allowed Execution

Some debugger features exist to evaluate live runtime values, so they are allowed to execute bounded customer code:

- User-authored probe expressions may read properties or invoke supported collection operations requested by the expression.
- Supported collection and dictionary capture may read `Count`, call `GetEnumerator()`, `MoveNext()`, `Current`, and dictionary entry `Key`/`Value` accessors, subject to configured depth, collection-size, and timeout limits.
- Special system-type selectors may read documented BCL properties, such as selected `System.Exception` and `System.Lazy<T>` properties.
- Safe BCL `ToString()` calls may be used only for types allowed by `Redaction.IsSafeToCallToString()`.

## Not Allowed By Default

Capture paths must not silently broaden default capture to execute customer code. In particular, future changes should not call arbitrary getters, indexers, enumerable methods, or `ToString()` for unknown customer types unless the behavior is made explicit in product policy and covered by tests.

When a value is useful but would require disallowed customer code execution, prefer omitting that value and reporting a `notCapturedReason` over executing the code.

## Review Checklist

For debugger capture, expression, and exception-replay changes, reviewers should identify whether the change introduces any of the following operations:

- `PropertyInfo.GetValue`, expression property access, or indexer access
- `IEnumerable.GetEnumerator`, `MoveNext`, `Current`, `Count`, `Key`, or `Value`
- `Exception.Message`, `Exception.StackTrace`, `Exception.ToString`, or custom exception overrides
- arbitrary `ToString()`, `Equals()`, or operator calls

If the operation can execute customer code, the PR must state whether it is covered by the allowed execution policy above, add or update regression tests, and document any new `notCapturedReason` behavior.
