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

Keep the framework's usual test attribute (`Fact`, `Theory`, `Test`, or `TestMethod`). A method annotation applies to all of that method's parameterized cases. The attributes apply to individual method declarations, including unchanged methods inherited from a base class. They do not apply to classes or propagate to an overriding method; annotate the override explicitly.

`PreventFailureSuppression` prevents Datadog quarantine and disabling from controlling the test. A terminal failure remains blocking. It does not prevent otherwise permitted retries, and during Attempt to Fix any failed attempt keeps the test failing even if another attempt passes.

`NoAutoRetry` excludes the test from Datadog Auto Test Retries, Early Flake Detection, and Attempt to Fix. Explicit framework retry policies remain available. Quarantine and disabling continue to apply unless `PreventFailureSuppression` is also present.

Neither annotation overrides explicit skips, test filters, or Test Impact Analysis selection. Test Impact Analysis can therefore skip a quarantined test with `PreventFailureSuppression` or an Attempt to Fix test with `NoAutoRetry`, because the annotation removes the policy that would force it to run. Unannotated tests keep their existing behavior.

Emitted test executions carry `test.test_management.prevent_failure_suppression: true` and/or `test.test_management.no_auto_retry: true`. Absent annotations omit these tags. Backend-assigned quarantine, disabled, and Attempt to Fix tags remain visible even when annotations override the execution policy.
