# Benchmarks

GitLab CI configuration for the benchmarks that run on the
[Benchmarking Platform](https://datadoghq.atlassian.net/wiki/spaces/APMINT/pages/2419261562/Benchmarking+Platform).

## Layout

- `microbenchmarks.yml`: BenchmarkDotNet microbenchmarks.
    - `run-benchmarks` runs them on an ephemeral Windows instance via `bp-infra`, then converts,
      uploads and comments on the PR.
    - `check-big-regressions` fails on regressions above the threshold defined on
      `bp-runner.fail-on-regression.yml`.
- `microbenchmarks/`: files used by `run-benchmarks`.
    - `infrastructure/`: `bp-infra` provision files.
    - `scripts/`: fetch, convert, upload and PR comment steps.
- `macrobenchmarks.yml`: k6 load tests against a sample ASP.NET app on x86, arm64 and Windows.
    - Runs on every pipeline, non-interruptible on `master`.
    - `check-slo-breaches` gates releases based on SLOs defined on `bp-runner.fail-on-breach.yml`.
    - Steps, k6 script, and bp-infra provisioning live in `macrobenchmarks/`; scripts shared
      with other suites (Azure pipeline wait) live in `shared/`.
- `shared/`: scripts shared by multiple suites (`wait-for-pipeline.sh` and its
  `steps/retry.sh` dependency).
- `dsm-throughput.yml`: Data Streams Monitoring throughput benchmark.
    - Runs on every pipeline, non-interruptible on `master`.
    - `dsm-throughput/`: files used by the suite (`bp-runner.yml`, steps, container
      for the CI image; see its README).
    - `check_azure_pipeline` waits for the Azure DevOps build via
      `shared/wait-for-pipeline.sh`, which the macrobenchmarks suite shares.
- `dotnet-aspnet-realworld-parallel` stages: included in the root `.gitlab-ci.yml` from
  [apm-sdks-benchmarks](https://github.com/DataDog/apm-sdks-benchmarks/tree/main/.gitlab).
    - Change them there.

## Marking a benchmark as flaky

Add it to `FLAKY_BENCHMARKS_REGEX` in the suite's file:

- Microbenchmarks: `run-benchmarks` in `microbenchmarks.yml`.
- Macrobenchmarks: top-level `variables` in `macrobenchmarks.yml`.

The benchmark still runs and reports, but doesn't fail performance quality gates:
`check-big-regressions` (percentage-based) and `check-slo-breaches` (SLO-based).

- The regex matches anywhere in the scenario name.
    - `SpanBenchmark` quarantines every `SpanBenchmark` method and framework.
    - Anchor with `^...$` to target one scenario.

```yaml
FLAKY_BENCHMARKS_REGEX: "SpanBenchmark|^Benchmarks\\.Trace\\.RedisBenchmark\\.SendReceive netcoreapp3\\.1$"
```

Open a ticket to fix or remove it. See
[Flaky Benchmarks Monitoring](https://datadoghq.atlassian.net/wiki/spaces/APMINT/pages/7223313012/Flaky+Benchmarks+Monitoring).
