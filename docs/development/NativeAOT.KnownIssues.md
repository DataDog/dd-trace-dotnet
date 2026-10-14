# NativeAOT known issues

Bugs found while adding NativeAOT support and not fixed: NativeAOT behaves as the tracer does under JIT (functional
parity), and the fix is left for a change of its own.

| Issue | Affects | Status |
|---|---|---|
| [Continuations of Task subclasses return a `Task<TResult>`](#continuations-of-task-subclasses-return-a-tasktresult) | JIT and NativeAOT applications | Same behavior in both; the fix changes JIT |
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

## The discovery recorder can lose the mappings of another process

`DD_DUCKTYPE_DISCOVERY_OUTPUT_PATH` recording (test workflows that record the duck typing mappings for the NativeAOT
build, not applications): when the process holding the map's lock stalls more than 30 seconds between reading the map and
writing it, the exit flush of another process writes the map without the lock, and the first process then overwrites it
without those mappings. See [Known Issues](./DuckTyping.NativeAOT.md#known-issues) of the DuckTyping NativeAOT guide.
