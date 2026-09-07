# APMS-20314 Ninject MVC cache reproduction

This is an investigation asset for APMS-20314, not a tracer regression test.
It reproduces the old Ninject MVC validation path observed in the customer
dumps:

- .NET Framework 4.6;
- ASP.NET MVC 4.0.0.1;
- Ninject 3.0;
- `NinjectDataAnnotationsModelValidatorProvider`;
- `AddImplicitRequiredAttributeForValueTypes = true`;
- many implicit `RequiredAttribute` instances passing through Ninject's global
  activation cache.

## Modes

Set `ReproductionMode` in `Web.config`, then restart IIS Express:

- `NinjectValidator`: affected baseline using the Ninject validator and cache.
- `DefaultValidator`: targeted mitigation using MVC's default data-annotations
  validator.
- `CacheDisabled`: broader mitigation using a no-op `IActivationCache`.

## Run

Restore packages and build the project with Visual Studio/MSBuild, then launch
it with 64-bit IIS Express. The project URL defaults to
`http://localhost:61914/`.

The package versions are deliberately pinned to the versions found in the
customer process. Do not upgrade Ninject for this experiment: the historical
activation-cache implementation is the behavior under test.

From the repository root, the equivalent command-line build is:

```powershell
msbuild .\tracer\test\test-applications\aspnet\Samples.NinjectMvcCache\Samples.NinjectMvcCache.csproj /restore /p:RestorePackagesConfig=true /p:SolutionDir="$PWD\" /p:RestorePackagesPath="$PWD\packages" /p:Platform=x64
```

From PowerShell 7, generate concurrent validation traffic:

```powershell
.\load.ps1 -Concurrency 32 -Requests 1000 -Iterations 250
```

Use enough requests to run beyond Ninject's default 30-second pruning period.
The script reports request latency percentiles and the final cache state.

Useful endpoints:

- `GET /Stress/Run?iterations=250` performs real MVC validator resolution and
  returns timing. Add `includeCache=true` for an optional snapshot during
  manual probing; the load script leaves it disabled to avoid measurement
  interference.
- `GET /Stress/Cache` reports live entries, backing capacity, mutation version,
  distinct hashes, largest collision group, and live target types.
- `POST /Stress/Collect` forces a test-only full collection so the next Ninject
  prune cycle has work to do. It should not be used as a production pattern.

Compare all three modes under the same load, both with and without Datadog
attached. The important outcome is whether the affected mode develops a large
single-hash collision group dominated by `RequiredAttribute`, and whether the
two mitigations prevent that state and its latency spikes.

## Instrumented integration test

`NinjectMvcCacheTests` uses the repository's `IisFixture` to launch this app in
64-bit IIS Express with the Datadog profiler attached. It creates approximately
the same number of implicit `RequiredAttribute` instances seen in the customer
dump, asserts the resulting collision-heavy cache shape, and confirms that the
request emitted the expected ASP.NET and MVC spans. The test deliberately does
not assert latency because host load would make that threshold unreliable.
