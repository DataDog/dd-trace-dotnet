# NativeAOT known issues

Bugs found while adding NativeAOT support and not fixed: NativeAOT behaves as the tracer does under JIT (functional
parity), and the fix is left for a change of its own.

| Issue | Affects | Status |
|---|---|---|
| [Continuations of Task subclasses return a `Task<TResult>`](#continuations-of-task-subclasses-return-a-tasktresult) | JIT and NativeAOT applications | Same behavior in both; the fix changes JIT |
| [Duck typing method selection ignores the binding flags when it adapts the arguments](#duck-typing-method-selection-ignores-the-binding-flags-when-it-adapts-the-arguments) | Duck typing in JIT and NativeAOT applications | Same behavior in both; the fix changes dynamic duck typing |
| [The discovery recorder can lose the mappings of another process](#the-discovery-recorder-can-lose-the-mappings-of-another-process) | Duck typing mappings recording (tests) | Not fixed: negligible window |

## Continuations of Task subclasses return a `Task<TResult>`

An instrumented method (by an integration, `[Trace]` or `DD_TRACE_METHODS`) declares a return type that derives from
`Task<TResult>`, e.g. `class PairTask<TResult, TTag> : Task<TResult>`:

- `EndMethodHandler<TIntegration, TTarget, TReturn>` creates a `TaskContinuationGenerator<TIntegration, TTarget, TReturn, TResult>`,
  with the result type of `ContinuationsHelper.GetResultType` (the generic argument of the first type of the hierarchy
  with one: with several, the one of the base type). NativeAOT computes the same type at build time
  (`CallTargetRegistryGenerator.GetTaskResultType`) and registers the same generator.
- When the task isn't completed when the method returns, the generator returns its continuation, a `Task<TResult>`; when
  `OnAsyncMethodEnd` replaces the result of a completed task, it returns `Task.FromResult(result)`. Both are converted to
  the declared type with `Unsafe.As`, without a type check (`ContinuationGenerator.ToTReturn`).
- The caller gets an object that isn't of the declared type: a cast to the subclass throws, and a member of the subclass
  reads memory of another type.

Under JIT, this is the behavior of every version with continuations; NativeAOT keeps it. Before, NativeAOT created no
continuation for a subclass with several generic arguments (the span of the method wasn't closed).

Fix (changes JIT): for a subclass, return the original task and observe its completion with a continuation of its own
(the span closes when the task completes; `OnAsyncMethodEnd` can't replace the result). Test it with pending, completed,
faulted and canceled tasks, asserting the identity of the returned object.
`CallTargetRegistryGeneratorTests.TaskSubclassContinuationHasTheResultTypeOfItsBaseType` checks the registration and the
value, not the type of the returned object.

## Duck typing method selection ignores the binding flags when it adapts the arguments

A proxy method's `[Duck(BindingFlags = ...)]` restricts the lookup of a target method with the proxy's parameter types, but
not the search that follows when there's none, over every method of the target type: e.g. a proxy method restricted to
public methods selects a private one whose parameters duck chaining adapts. With fallback names, such a method of the first
name wins over a public method of a later name. NativeAOT generates the same selection (the generator asks dynamic duck
typing). No proxy method of Datadog.Trace restricts its binding flags (they add `IgnoreCase` or `NonPublic` to
`DuckAttribute.DefaultFlags`). See
[Known Issues](./DuckTyping.NativeAOT.md#known-issues) of the DuckTyping NativeAOT guide.

## The discovery recorder can lose the mappings of another process

`DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH` recording (test workflows that record the duck typing mappings for the NativeAOT
build, not applications): the exit flush of a process writes the map without its lock when it can't take it within 30
seconds or can't open the lock file; another process that read the map before that write and writes it after (stalled in
between, or writing at the same time without the lock) overwrites it without those mappings. See [Known Issues](./DuckTyping.NativeAOT.md#known-issues) of the DuckTyping NativeAOT guide.
