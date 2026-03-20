#!/usr/bin/env python3
"""
Compare BytesAllocatedPerOperation across pipelines for each benchmark.
"""

import json
from pathlib import Path
from collections import defaultdict
import statistics

DATA_DIR = Path("/Users/augusto.deoliveira/go/src/github.com/DataDog/benchmarking-platform-tools/scripts/stability/data-dd-trace-dotnet-2cpus10launches200msfixedcounts")

def main():
    # Collect BytesAllocatedPerOperation for each benchmark across pipelines
    # Key: (benchmark_name, runtime) -> list of values from different pipelines
    bytes_per_op = defaultdict(list)

    for pipeline_dir in sorted(DATA_DIR.glob("pipeline_*")):
        for job_dir in pipeline_dir.glob("job_*"):
            artifacts_dir = job_dir / "artifacts"
            for json_file in artifacts_dir.glob("candidate.*.json"):
                if "converted" in json_file.name:
                    continue

                try:
                    with open(json_file) as f:
                        data = json.load(f)

                    for bench in data.get("Benchmarks", []):
                        full_name = bench.get("FullName", "Unknown")
                        memory = bench.get("Memory", {})
                        bytes_alloc = memory.get("BytesAllocatedPerOperation")

                        if bytes_alloc is not None:
                            bytes_per_op[full_name].append(bytes_alloc)
                except Exception as e:
                    pass

    # Calculate stats and find inconsistent ones
    print("BytesAllocatedPerOperation Comparison Across Pipelines")
    print("=" * 80)
    print()

    inconsistent = []
    consistent = []

    for bench_name in sorted(bytes_per_op.keys()):
        values = bytes_per_op[bench_name]
        if len(values) < 2:
            continue

        min_val = min(values)
        max_val = max(values)
        mean_val = statistics.mean(values)

        # Check if all values are the same
        if min_val == max_val:
            consistent.append((bench_name, values[0], len(values)))
        else:
            # Calculate coefficient of variation
            stdev = statistics.stdev(values) if len(values) > 1 else 0
            cv = (stdev / mean_val * 100) if mean_val > 0 else 0
            inconsistent.append((bench_name, min_val, max_val, mean_val, cv, len(values), values))

    print(f"CONSISTENT ({len(consistent)} benchmarks - same value across all pipelines):")
    print("-" * 80)
    for bench_name, value, count in consistent[:10]:
        short_name = bench_name.split(".")[-1] if "." in bench_name else bench_name
        print(f"  {short_name}: {value} bytes ({count} pipelines)")
    if len(consistent) > 10:
        print(f"  ... and {len(consistent) - 10} more")

    print()
    print(f"INCONSISTENT ({len(inconsistent)} benchmarks - varying values):")
    print("-" * 80)

    # Sort by coefficient of variation (most variable first)
    inconsistent.sort(key=lambda x: x[4], reverse=True)

    for bench_name, min_val, max_val, mean_val, cv, count, values in inconsistent:
        short_name = bench_name.split(".")[-1] if "." in bench_name else bench_name
        # Extract runtime from name
        runtime = ""
        for r in ["net472", "net6.0", "netcoreapp3.1"]:
            if r in bench_name:
                runtime = r
                break

        print(f"  {short_name} ({runtime}):")
        print(f"    Range: {min_val} - {max_val} bytes (CV: {cv:.1f}%)")
        print(f"    Values: {sorted(set(values))}")
        print()

if __name__ == "__main__":
    main()
