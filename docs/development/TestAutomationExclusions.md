# Test automation exclusions

Reference the `Datadog.Trace` NuGet package and annotate individual xUnit, NUnit, or MSTest test methods:

```csharp
using Datadog.Trace.Ci;

[PreventFailureSuppression]
[NoAutoRetry]
public void CriticalTest()
{
    // Framework test assertions.
}
```

Keep the framework's usual test attribute (`Fact`, `Theory`, `Test`, or `TestMethod`). A method annotation applies to all of that method's parameterized cases. The attributes do not propagate from classes or base methods.

`PreventFailureSuppression` prevents Datadog quarantine and disabling from controlling the test. A terminal failure remains blocking. It does not prevent otherwise permitted retries.

`NoAutoRetry` excludes the test from Datadog Auto Test Retries, Early Flake Detection, and Attempt to Fix. Explicit framework retry policies remain available. Quarantine and disabling continue to apply unless `PreventFailureSuppression` is also present.

Neither annotation overrides explicit skips, test filters, or Test Impact Analysis selection. Unannotated tests keep their existing behavior.

Emitted test executions carry `test.test_management.prevent_failure_suppression: true` and/or `test.test_management.no_auto_retry: true`. Absent annotations omit these tags. Backend-assigned quarantine, disabled, and Attempt to Fix tags remain visible even when annotations override the execution policy.
