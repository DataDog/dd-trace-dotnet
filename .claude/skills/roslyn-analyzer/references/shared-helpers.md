# Shared Helpers

Located in `tracer/src/Datadog.Trace.Tools.Analyzers/Helpers/`.

## Most commonly used

### `IsTypeNullAndReportForDatadogTrace`

Guard for analyzers that depend on internal `Datadog.Trace` types. Reports `DD0009` if the type was renamed/removed, but only when compiling `Datadog.Trace` itself. Returns `true` if the type is null (caller should bail out).

```csharp
var requiredType = context.Compilation.GetTypeByMetadataName("Datadog.Trace.Some.Type");

if (Helpers.Diagnostics.IsTypeNullAndReportForDatadogTrace(
        context, requiredType, nameof(MyAnalyzer), "Datadog.Trace.Some.Type"))
{
    return; // Type missing, diagnostic already reported
}
// requiredType guaranteed non-null here (via [NotNullWhen(false)])
```

### `WellKnownTypeProvider`

Compilation-scoped cache for type lookups. Use instead of calling `GetTypeByMetadataName` repeatedly — especially in callbacks that fire per-node or per-symbol.

```csharp
var typeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
var type = typeProvider.GetOrCreateTypeByMetadataName("Datadog.Trace.Some.Type");
```

### `PooledConcurrentSet<T>` / `PooledConcurrentDictionary<K,V>`

Thread-safe, memory-efficient collections for cross-file analysis (Pattern 3). Always dispose in `CompilationEndAction` to return them to the pool.

```csharp
var candidates = PooledConcurrentSet<INamedTypeSymbol>.GetInstance(SymbolEqualityComparer.Default);
// ... use in RegisterSymbolAction callbacks ...
context.RegisterCompilationEndAction(ctx =>
{
    // ... report diagnostics ...
    candidates.Dispose();
});
```

## Full reference

| Helper | Purpose |
|--------|---------|
| `Diagnostics` | `DD0009` reporting + `IsTypeNullAndReportForDatadogTrace` guard |
| `RoslynHelper` | Parameter determination, expression naming utilities |
| `WellKnownTypeProvider` | Compilation-scoped type cache |
| `ISymbolExtensions` | Symbol analysis extensions (`HasAnyAttribute`, `IsTopLevelStatementsEntryPointType`, etc.) |
| `OperationExtensions` | `IOperation` analysis extensions |
| `PooledConcurrentSet<T>` | Thread-safe pooled set for cross-file analysis |
| `PooledConcurrentDictionary<K,V>` | Thread-safe pooled dictionary |
| `PooledHashSet<T>` | Pooled hash set |
| `ConcurrentCache<K,V>` | Thread-safe cache |
| `BoundedCacheWithFactory<K,V>` | Bounded cache with factory pattern |

Additional utilities in the directory include `ArrayBuilder`, `CompilationExtensions`, `ObjectPool`, and others. Browse `tracer/src/Datadog.Trace.Tools.Analyzers/Helpers/` for the full set.
