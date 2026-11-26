# Data Streams Monitoring throughput benchmark

Measures the overhead of Data Streams Monitoring (DSM) on a Kafka
producer/consumer workload. Two experiments run the same
`Samples.KafkaBenchmark` app from this repo (see
`tracer/test/test-applications/throughput/Samples.KafkaBenchmark`):

- `benchmark-tracer-no-dsm`: tracer on, `DD_DATA_STREAMS_ENABLED=false` (baseline).
- `benchmark-dsm-enabled`: tracer on, `DD_DATA_STREAMS_ENABLED=true` (candidate).

`steps/compare-results.py` gates the pipeline on the duration and memory
overhead between the two; both must stay under the threshold defined in that
script (currently 10%). Results are also converted and uploaded to the
Benchmarking Platform UI under the project `dd-trace-dotnet`.

## Layout

- `bp-runner.yml`: the bp-runner experiment config. Services (Datadog agent,
  Zookeeper, Kafka) are pinned to CPUs 14-19; the benchmark runs on CPUs 20-23.
- `steps/`: the scripts the config invokes (`setup-sut.sh` fetches the Azure
  consolidated-pipeline build artifacts, `start-benchmark.sh` runs the app,
  `compare-results.*` gate the run, `convert-to-common-format.sh` prepares the
  uploads).
- `container/`: the Dockerfile for the CI image (and for local runs).
- `.env.example`: template for the `.env` needed by local runs.
- `../shared/`: scripts shared with the macrobenchmarks suite
  (`wait-for-pipeline.sh` and its `retry.sh` dependency).

## How CI runs it

The child pipeline in `../dsm-throughput.yml` has two jobs:

- `check_azure_pipeline` waits for the Azure DevOps `consolidated-pipeline`
  build of this branch and writes `build-id.txt`.
- `dsm_throughput` needs that job, runs `steps/setup-sut.sh` (which clones
  dd-trace-dotnet on the benchmarked branch and downloads its build
  artifacts), then runs `bp-runner bp-runner.yml --debug` from this directory.
  Artifacts land in `artifacts/` at the repo root.

The CI image is built in-repo: the manual `build-benchmark-ci-images` job (in
the `benchmarks` stage) builds `container/` and pushes
`registry.ddbuild.io/ci/benchmarking-platform:dd-trace-dotnet-dsm-throughput-<pipeline id>`.
After a successful build, repin `DSM_THROUGHPUT_CI_IMAGE` in `../dsm-throughput.yml`
to that tag.

## Adding a benchmark

1. Add an experiment to `bp-runner.yml`: copy one of the two existing blocks,
   give it a unique `EXPERIMENT_NAME`, `KAFKA_TOPIC` and `DD_SERVICE`, and
   point its step at `steps/start-benchmark.sh` (it reads its configuration
   from the experiment env).
2. The raw results land in `artifacts/<experiment>.json`.
3. To include the new experiment in the overhead gate, extend
   `steps/compare-results.sh`/`compare-results.py`.
4. To upload it, add a `scenario` case for it in
   `steps/convert-to-common-format.sh` (unknown experiment names upload as
   candidates under a generic scenario name).

## Updating a benchmark

- Workload knobs (message count, thread count, GC limits, topics) are
  environment variables on the experiments in `bp-runner.yml`.
- The benchmarked application itself lives in this repo under
  `tracer/test/test-applications/throughput/Samples.KafkaBenchmark`; changes
  there are picked up when `setup-sut.sh` clones and builds the branch.
- Service pinning and CPU allocation are edited in `bp-runner.yml`
  (`global_setup` entries and per-step `cpus:`).

## Marking a benchmark as flaky

This suite has no per-benchmark quarantine: it always runs the same two
experiments and gates on their comparison. Transient infrastructure failures
are covered by the `dsm_throughput` job retry (`max: 2` on unknown, data
integrity, runner system, scheduler and API failures). If an experiment
becomes chronically noisy, raise the threshold in `steps/compare-results.py`
(precedent: platform commit `chore: bump threshold to avoid failing
pipelines` bumped it from 5 to 10) and mention the reasoning in the PR.

## Running locally

Install [bp-runner](https://github.com/DataDog/benchmarking-platform-tools/blob/main/bp-runner/INSTALL.md),
then from this directory:

```bash
cp .env.example .env   # set DD_API_KEY in .env
bp-runner bp-runner.yml --debug --local
```

`--local` builds the Docker image from `container/` and runs the Datadog
agent, Zookeeper, Kafka and the benchmark experiments inside it; results
land in `artifacts/` here.

Caveat: `steps/setup-sut.sh`, which clones dd-trace-dotnet and downloads the
Azure build artifacts, is driven by the `dsm_throughput` CI job, not by
`bp-runner.yml`, so a local run starts without the SUT checkout that
`start-benchmark.sh` expects (it `cd`s into `./dd-trace-dotnet/...`).
`start-benchmark.sh` falls back to the tracer installed in the image
(`/opt/datadog`) when `IS_LOCAL_RUN=true`, but the sample app still needs the
checkout; provide it inside the container or run the suite through CI when
you need the full flow.
