# Reference-chain benchmark

This benchmark runs `Samples.Computer01 --scenario 31 --param 7`, records process
wall time and CPU consumption, extracts native reference-chain statistics, and
generates a self-contained HTML dashboard.

It does not build anything. Build the profiler and Computer01 before using it.

## Record an implementation

```powershell
.\run.ps1 -Label baseline
```

Results are written to `artifacts\reference-chain\baseline`:

- `dashboard.html` — summary cards, charts, and all heap dumps
- `results.json` — structured input for comparisons
- `results.csv` — flat data for spreadsheets
- `run-NN` — raw logs and managed process metrics

`results.json` schema version 2 records both stable workload metrics and
implementation-specific details:

- observed and traversed roots, including duplicate/interior/weak counters
- object, reference, and tree-node traversal counts
- per-root-category objects, references, duration, and maxima
- `GetObjectSize2` and `GetClassFromObject` call breakdowns
- visited representation and memory details

The original `baseline` label is schema version 1 and remains a valid comparison
input. After changing native benchmark instrumentation, record `baseline-v2` before
changing traversal behavior:

```powershell
.\run.ps1 -Label baseline-v2
.\compare.ps1 `
  -BaselinePath ..\..\..\artifacts\reference-chain\baseline `
  -CandidatePath ..\..\..\artifacts\reference-chain\baseline-v2
```

The runner uses:

- Release, x64, .NET 10 by default
- Server GC
- five independent processes, 50 seconds each
- heap snapshots every 10 seconds
- three completed heap dumps per process with the current startup/cooldown behavior
- `DD_INTERNAL_PROFILING_HEAPSNAPSHOT_REFERENCE_CHAIN_BENCHMARK_ENABLED=1`
- `DD_PROFILING_METRICS_FILEPATH` for process CPU, GC, and memory metrics

A label is immutable: `run.ps1` fails if its output directory already exists.
Use a new label for every implementation.

Delete an existing label and all of its results with:

```powershell
.\run.ps1 -Label baseline --RemoveLabel
```

Useful overrides:

```powershell
.\run.ps1 -Label quick-check -RunCount 1 -DurationSeconds 30
.\run.ps1 -Label baseline -OpenDashboard
```

## Compare implementations

```powershell
.\run.ps1 -Label candidate

.\compare.ps1 `
  -BaselinePath ..\..\..\artifacts\reference-chain\baseline `
  -CandidatePath ..\..\..\artifacts\reference-chain\candidate `
  -OpenDashboard
```

The comparison dashboard is generated in the candidate directory. It shows
baseline and candidate medians, percentage changes, improvements/regressions,
and warnings when surviving objects, parsed roots, or traversed objects differ
enough to make timing comparisons questionable.
