#!/usr/bin/env python3
"""
Extract warmup and actual iteration counts from benchmark JSON files.
Also extracts operations per iteration.
"""

import json
import csv
from pathlib import Path
from collections import defaultdict

DATA_DIR = Path("/Users/augusto.deoliveira/go/src/github.com/DataDog/benchmarking-platform-tools/scripts/stability/data-dd-trace-dotnet-2cpus10launches500ms")
OUTPUT_DIR = Path(__file__).parent

def main():
    # Use first pipeline as sample
    pipeline_dir = DATA_DIR / "pipeline_103203603" / "job_run-benchmarks_1515997732" / "artifacts"

    results = []

    for json_file in sorted(pipeline_dir.glob("candidate.*.json")):
        if "converted" in json_file.name:
            continue

        with open(json_file) as f:
            data = json.load(f)

        for bench in data.get("Benchmarks", []):
            full_name = bench.get("FullName", "Unknown")
            measurements = bench.get("Measurements", [])

            # Count by stage and launch
            warmup_count = 0
            actual_count = 0
            launches = set()

            # Track operations for Workload/Actual (the real measurement iterations)
            workload_actual_ops = []

            for m in measurements:
                stage = m.get("IterationStage", "")
                mode = m.get("IterationMode", "")
                launch = m.get("LaunchIndex", 0)
                ops = m.get("Operations", 0)
                launches.add(launch)

                if stage == "Warmup":
                    warmup_count += 1
                elif stage == "Actual":
                    actual_count += 1
                    if mode == "Workload":
                        workload_actual_ops.append(ops)

            num_launches = len(launches)
            warmup_per_launch = warmup_count / num_launches if num_launches > 0 else 0
            actual_per_launch = actual_count / num_launches if num_launches > 0 else 0

            # Operations stats
            min_ops = min(workload_actual_ops) if workload_actual_ops else 0
            max_ops = max(workload_actual_ops) if workload_actual_ops else 0
            avg_ops = sum(workload_actual_ops) / len(workload_actual_ops) if workload_actual_ops else 0

            results.append({
                "benchmark": full_name,
                "launches": num_launches,
                "total_warmup": warmup_count,
                "total_actual": actual_count,
                "warmup_per_launch": round(warmup_per_launch, 1),
                "actual_per_launch": round(actual_per_launch, 1),
                "min_ops_per_iter": min_ops,
                "max_ops_per_iter": max_ops,
                "avg_ops_per_iter": round(avg_ops, 0),
            })

    # Write CSV
    output_path = OUTPUT_DIR / "iterations_500ms_10launches.csv"
    with open(output_path, "w", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=[
            "benchmark", "launches", "total_warmup", "total_actual",
            "warmup_per_launch", "actual_per_launch",
            "min_ops_per_iter", "max_ops_per_iter", "avg_ops_per_iter"
        ])
        writer.writeheader()
        writer.writerows(results)

    print(f"Wrote {len(results)} benchmarks to {output_path}")

    # Summary stats
    total_warmup = sum(r["total_warmup"] for r in results)
    total_actual = sum(r["total_actual"] for r in results)
    print(f"\nSummary:")
    print(f"  Total warmup iterations: {total_warmup}")
    print(f"  Total actual iterations: {total_actual}")
    print(f"  Avg warmup per benchmark: {total_warmup / len(results):.1f}")
    print(f"  Avg actual per benchmark: {total_actual / len(results):.1f}")

    # Operations summary
    all_avg_ops = [r["avg_ops_per_iter"] for r in results if r["avg_ops_per_iter"] > 0]
    if all_avg_ops:
        print(f"\nOperations per iteration:")
        print(f"  Min avg: {min(all_avg_ops):,.0f}")
        print(f"  Max avg: {max(all_avg_ops):,.0f}")
        print(f"  Overall avg: {sum(all_avg_ops)/len(all_avg_ops):,.0f}")

if __name__ == "__main__":
    main()
