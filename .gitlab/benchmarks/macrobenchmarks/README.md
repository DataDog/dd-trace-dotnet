# Macrobenchmarks

k6 load tests against the `Samples.AspNetCoreSimpleController` sample app,
measuring tracer throughput on x86, arm64, and Windows. The suite is driven by
`../macrobenchmarks.yml` and runs on every pipeline (non-interruptible on
`master`). `check-slo-breaches` gates releases on the SLOs defined in
`../bp-runner.fail-on-breach.yml`.

## Layout

- `bp-runner.yml`, `bp-runner.arm.yml`: Linux experiment configs. Each job
  `cd`s into this directory and runs `bp-runner <config> --debug`.
- `bp-runner.win.yml`, `run-benchmarks.ps1`: Windows entry points, executed on
  the ephemeral instance provisioned by `infrastructure/instance.yaml`.
- `steps/`: SUT, k6, agent, and result-handling scripts for Linux and Windows.
- `k6/script.js`: the load-test scenarios (warmup, normal_operation, high_load).
- `infrastructure/`: bp-infra provisions: `instance.yaml` (Windows ephemeral
  instance) and `ami.yaml` (manual base AMI build).
- `container/`: provenance of the CI image, built in-repo by the
  `build-benchmark-ci-images` job.
- `tests/` and `test.sh`: offline regression tests for the setup and retry
  scripts (also run by the `test-scripts` CI job).

## Adding a benchmark variant

Each x86/arm64/win job in `../macrobenchmarks.yml` is one tracer
configuration of the same suite. To add one:

1. Add a job extending `.benchmarks-x86`, `.benchmarks-arm64`, or
   `.benchmarks-win`, overriding `variables:` only.
2. Set `DD_SERVICE` to a unique scenario name (hyphens, not underscores; this
   is the name that shows up in the Benchmarking Platform UI).
3. Add the variant's tracer flags (profiling, trace stats, manual
   instrumentation, and so on) and `ENDPOINT` (`hello` or `hello/Exception`).

No wiring is needed beyond that: the templates run the scripts and configs in
this directory, so a new variant only differs in environment.

## Updating a benchmark

- k6 load profile: the `K6_OPTIONS_*` variables in `../macrobenchmarks.yml`
  (per template, or per job to override) and the scenarios in `k6/script.js`.
- Experiment layout, CPU pinning, agent setup: `bp-runner*.yml` and
  `steps/`.
- Windows instance and AMI settings: `infrastructure/`.
- The tracer build being benchmarked is picked by
  `../shared/wait-for-pipeline.sh`, which waits for the Azure
  `consolidated-pipeline` stages and writes `build-id.txt`.

## Marking a benchmark as flaky

Add its scenario name to `FLAKY_BENCHMARKS_REGEX` in the top-level
`variables:` of `../macrobenchmarks.yml` (details in `../README.md`). The
benchmark still runs and reports, but `check-slo-breaches` stops failing on it.

## Running locally

Linux, via bp-runner's local mode (requires Docker and
[bp-runner](https://github.com/DataDog/benchmarking-platform-tools/blob/main/bp-runner/INSTALL.md)):

```bash
cd .gitlab/benchmarks/macrobenchmarks
cp .env.example .env        # set DD_API_KEY and the k6 options you want
ssh-add ~/.ssh/your-key     # needed to fetch private deps during image build
export buildId=123456       # an Azure consolidated-pipeline build id
set -a; source .env; set +a
./steps/setup-sut.sh        # clones dd-trace-dotnet and fetches build artifacts
bp-runner bp-runner.yml --debug --local
```

Windows, on an ephemeral instance: run a `-win` job manually in the pipeline
(set `CLEANUP=false` to keep the instance for debugging), or follow the
[bp-infra local runs guide](https://github.com/DataDog/benchmarking-platform-tools/blob/main/infra/README.md#local-runs)
with `infrastructure/instance.yaml`.

To run only the offline script tests: `./test.sh` (Docker) or
`python3 -m unittest discover -s tests -v` on Linux.

## Building the CI image and the Windows AMI

- `build-benchmark-ci-images` (manual) builds and pushes
  `registry.ddbuild.io/ci/benchmarking-platform:dd-trace-dotnet-macrobenchmarks-<pipeline id>`
  from `container/`. After the build, repin `MACROBENCHMARKS_CI_IMAGE` in
  `../macrobenchmarks.yml` to the produced tag.
- `build-dd-trace-dotnet-macrobenchmarks-ami` (manual) rebuilds the Windows
  base AMI from `infrastructure/ami.yaml`.
