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
    - Runs on `master`, manual elsewhere.
    - `check-slo-breaches` gates releases based on SLOs defined on `bp-runner.fail-on-breach.yml`.
    - Steps live in the `dd-trace-dotnet/macro` branch of
      [benchmarking-platform](https://github.com/DataDog/benchmarking-platform).
- `dsm-throughput.yml`: Data Streams Monitoring throughput benchmark.
    - Runs on `master`, manual elsewhere.
    - Steps live in the `dd-trace-dotnet/data-streams-monitoring` branch of
      [benchmarking-platform](https://github.com/DataDog/benchmarking-platform).
- `dotnet-aspnet-realworld-parallel` stages: included in the root `.gitlab-ci.yml` from
  [apm-sdks-benchmarks](https://gitlab.ddbuild.io/DataDog/apm-reliability/apm-sdks-benchmarks).
    - Change them there.

## Marking a benchmark as flaky

Add it to `FLAKY_BENCHMARKS_REGEX` in the suite's file:

- Microbenchmarks: `run-benchmarks` in `microbenchmarks.yml`.
- Macrobenchmarks: top-level `variables` in `macrobenchmarks.yml`.

The benchmark still runs and reports, but doesn't fail the gate.

- The regex matches anywhere in the scenario name.
    - `SpanBenchmark` quarantines every `SpanBenchmark` method and framework.
    - Anchor with `^...$` to target one scenario.

```yaml
FLAKY_BENCHMARKS_REGEX: "SpanBenchmark|^Benchmarks\\.Trace\\.RedisBenchmark\\.SendReceive netcoreapp3\\.1$"
```

Open a ticket to fix or remove it. See
[Flaky Benchmarks Monitoring](https://datadoghq.atlassian.net/wiki/spaces/APMINT/pages/7223313012/Flaky+Benchmarks+Monitoring).
